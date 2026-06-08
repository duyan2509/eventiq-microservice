using Eventiq.PaymentService.Application.Service.Interface;
using Eventiq.PaymentService.Domain.Enums;
using Eventiq.PaymentService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace Eventiq.PaymentService.Infrastructure.BackgroundServices;

/// <summary>
/// Safety net for lost or failed Stripe webhooks: periodically asks Stripe directly about
/// orders stuck in <see cref="OrderStatus.Pending"/> past the checkout window and settles them
/// through the same <see cref="IOrderSettlementService"/> path the webhook uses.
/// </summary>
/// <remarks>
/// Single-instance assumption: with more than one running instance, two reconcilers could pick the
/// same order — the Order xmin concurrency token makes a double-settle safe, but a distributed lock
/// would still be needed to avoid redundant Stripe calls (noted in the thesis report).
/// </remarks>
public class PaymentReconciliationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<PaymentReconciliationService> _logger;

    private readonly TimeSpan _interval;
    private readonly TimeSpan _grace;
    private readonly TimeSpan _lookback;
    private readonly int _batchSize;

    public PaymentReconciliationService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<PaymentReconciliationService> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;

        var section = config.GetSection("Reconciliation");
        _interval = TimeSpan.FromSeconds(section.GetValue("IntervalSeconds", 120));
        _grace = TimeSpan.FromMinutes(section.GetValue("GraceMinutes", 15));
        _lookback = TimeSpan.FromHours(section.GetValue("LookbackHours", 24));
        _batchSize = section.GetValue("BatchSize", 50);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_interval, stoppingToken);
            await ReconcileAsync(stoppingToken);
        }
    }

    private async Task ReconcileAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            var settlement = scope.ServiceProvider.GetRequiredService<IOrderSettlementService>();

            var now = DateTime.UtcNow;
            var cutoff = now - _grace;        // old enough that the checkout window has closed
            var lookback = now - _lookback;   // don't chase ancient abandoned orders forever

            var stuck = await db.Orders
                .Include(o => o.Items)
                .Where(o => o.Status == OrderStatus.Pending
                            && o.CreatedAt < cutoff
                            && o.CreatedAt > lookback
                            && o.StripeSessionId != string.Empty)
                .OrderBy(o => o.CreatedAt)
                .Take(_batchSize)
                .ToListAsync(ct);

            if (stuck.Count == 0) return;

            StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];
            var sessionService = new SessionService();
            int settled = 0, expired = 0, skipped = 0;

            foreach (var order in stuck)
            {
                ct.ThrowIfCancellationRequested();

                Session session;
                try
                {
                    session = await sessionService.GetAsync(order.StripeSessionId, cancellationToken: ct);
                }
                catch (StripeException ex)
                {
                    _logger.LogWarning(ex, "Reconciliation could not fetch Stripe session {SessionId} for order {OrderId}",
                        order.StripeSessionId, order.Id);
                    continue;
                }

                if (session.Status == "complete" && session.PaymentStatus == "paid")
                {
                    if (await settlement.SettlePaidAsync(order, SettlementSource.Reconciliation)) settled++;
                }
                else if (session.Status == "expired")
                {
                    order.Status = OrderStatus.Failed;
                    await db.SaveChangesAsync(ct);
                    expired++;
                }
                else
                {
                    // "open" — checkout still in progress; leave it for the next sweep.
                    skipped++;
                }
            }

            _logger.LogInformation(
                "Reconciliation swept {Count} stuck orders: {Settled} settled, {Expired} expired, {Skipped} still open.",
                stuck.Count, settled, expired, skipped);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Payment reconciliation sweep failed.");
        }
    }
}

using Eventiq.OrganizationService.Application.Service;
using Eventiq.OrganizationService.Domain;
using Eventiq.OrganizationService.Domain.Entity;
using Eventiq.OrganizationService.Domain.Repositories;
using Stripe;

namespace Eventiq.OrganizationService.Application.BackgroundServices;

public class OrgPayoutJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OrgPayoutJob> _logger;

    public OrgPayoutJob(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<OrgPayoutJob> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var nextRun = now.Date.AddDays(1);
            await Task.Delay(nextRun - now, stoppingToken);

            try
            {
                await RunPayoutsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during org payout job");
            }
        }
    }

    private async Task RunPayoutsAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var configService = scope.ServiceProvider.GetRequiredService<IPlatformConfigService>();
        var orgRepo = scope.ServiceProvider.GetRequiredService<IOrganizationRepository>();
        var payoutLogRepo = scope.ServiceProvider.GetRequiredService<IPayoutLogRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var config = await configService.GetInternalAsync(ct);

        // Only run on the configured payout day
        if (DateTime.UtcNow.Day != config.PayoutDayOfMonth)
            return;

        StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];

        var orgs = await orgRepo.GetConfiguredOrgsAsync(ct);
        var balanceService = new BalanceService();
        var payoutService = new PayoutService();

        foreach (var org in orgs)
        {
            if (string.IsNullOrEmpty(org.StripeAccountId))
                continue;

            try
            {
                var balance = await balanceService.GetAsync(
                    requestOptions: new RequestOptions { StripeAccount = org.StripeAccountId },
                    cancellationToken: ct);

                foreach (var available in balance.Available.Where(b => b.Amount > 0))
                {
                    var payout = await payoutService.CreateAsync(
                        new PayoutCreateOptions
                        {
                            Amount = available.Amount,
                            Currency = available.Currency
                        },
                        new RequestOptions { StripeAccount = org.StripeAccountId },
                        ct);

                    await payoutLogRepo.AddAsync(new PayoutLog
                    {
                        Id = Guid.NewGuid(),
                        OrgId = org.Id,
                        StripePayoutId = payout.Id,
                        Amount = available.Amount,
                        Currency = available.Currency,
                        TriggeredAt = DateTime.UtcNow
                    }, ct);

                    _logger.LogInformation(
                        "Payout triggered for org {OrgId}: {Amount} {Currency}",
                        org.Id, available.Amount, available.Currency);
                }

                await uow.SaveChangesAsync(ct);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe payout failed for org {OrgId}", org.Id);
            }
        }
    }
}

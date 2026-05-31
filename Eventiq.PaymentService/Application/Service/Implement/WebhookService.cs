using Eventiq.Contracts;
using Eventiq.PaymentService.Application.Service.Interface;
using Eventiq.PaymentService.Domain.Enums;
using Eventiq.PaymentService.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace Eventiq.PaymentService.Application.Service.Implement;

public class WebhookService : IWebhookService
{
    private readonly PaymentDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IConfiguration _config;
    private readonly ILogger<WebhookService> _logger;

    public WebhookService(
        PaymentDbContext dbContext,
        IPublishEndpoint publishEndpoint,
        IConfiguration config,
        ILogger<WebhookService> logger)
    {
        _dbContext = dbContext;
        _publishEndpoint = publishEndpoint;
        _config = config;
        _logger = logger;
    }

    public async Task HandleAsync(string payload, string stripeSignature)
    {
        var webhookSecret = _config["Stripe:WebhookSecret"]!;
        // throwOnApiVersionMismatch: false — Stripe CLI may stream newer API versions
        // than Stripe.net's compiled version; we only read core fields that are stable.
        var stripeEvent = EventUtility.ConstructEvent(
            payload, stripeSignature, webhookSecret,
            tolerance: 300,
            throwOnApiVersionMismatch: false);

        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
                await HandleCompletedAsync((Session)stripeEvent.Data.Object);
                break;
            case "checkout.session.expired":
                await HandleExpiredAsync((Session)stripeEvent.Data.Object);
                break;
        }
    }

    private async Task HandleCompletedAsync(Session session)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.StripeSessionId == session.Id && o.Status == OrderStatus.Pending);

        if (order == null)
        {
            _logger.LogInformation("Duplicate webhook or already processed: {SessionId}", session.Id);
            return;
        }

        // Mark as Paid + publish to Outbox in a single SaveChanges → atomic
        order.Status = OrderStatus.Paid;
        order.PaidAt = DateTime.UtcNow;

        await _publishEndpoint.Publish(new PaymentCompleted
        {
            OrderId = order.Id,
            UserId = order.UserId,
            SessionId = order.SessionId,
            Seats = order.Items.Select(i => new PaymentCompletedSeat
            {
                SeatId = i.SeatId,
                SeatLabel = i.SeatLabel,
                LegendName = i.LegendName,
                Price = i.Price
            }).ToList()
        });

        await _dbContext.SaveChangesAsync(); // Commits order update + outbox message atomically
    }

    private async Task HandleExpiredAsync(Session session)
    {
        await _dbContext.Orders
            .Where(o => o.StripeSessionId == session.Id && o.Status == OrderStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Failed));
    }
}

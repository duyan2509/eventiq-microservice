using Eventiq.Contracts;
using Eventiq.PaymentService.Application.Service.Interface;
using Eventiq.PaymentService.Domain.Entity;
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
    private readonly IOrderSettlementService _settlement;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IConfiguration _config;
    private readonly ILogger<WebhookService> _logger;

    public WebhookService(
        PaymentDbContext dbContext,
        IOrderSettlementService settlement,
        IPublishEndpoint publishEndpoint,
        IConfiguration config,
        ILogger<WebhookService> logger)
    {
        _dbContext = dbContext;
        _settlement = settlement;
        _publishEndpoint = publishEndpoint;
        _config = config;
        _logger = logger;
    }

    public async Task HandleAsync(string payload, string stripeSignature)
    {
        var webhookSecret = _config["Stripe:WebhookSecret"]!;

        // throwOnApiVersionMismatch: false — Stripe CLI may stream newer API versions
        // than Stripe.net's compiled version; we only read core fields that are stable.
        Stripe.Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                payload, stripeSignature, webhookSecret,
                tolerance: 300,
                throwOnApiVersionMismatch: false);
        }
        catch (StripeException ex)
        {
            // Bad signature / unparseable: no trustworthy event id, but still keep a row
            // so the failure is traceable instead of vanishing into a 500.
            _logger.LogWarning(ex, "Stripe webhook signature verification failed");
            await PersistAsync(new WebhookEvent
            {
                StripeEventId = string.Empty,
                EventType = "signature_verification_failed",
                Status = WebhookEventStatus.Failed,
                Payload = payload,
                Error = Truncate(ex.Message),
                AttemptCount = 1,
                ProcessedAt = DateTime.UtcNow,
            });
            throw; // controller maps StripeException -> 400
        }

        // Idempotency: skip if we already processed this exact Stripe event.
        var record = await _dbContext.WebhookEvents
            .FirstOrDefaultAsync(w => w.StripeEventId == stripeEvent.Id);
        if (record is { Status: WebhookEventStatus.Processed })
        {
            _logger.LogInformation("Duplicate webhook ignored: {EventId}", stripeEvent.Id);
            return;
        }

        // Record (or re-use on retry) the inbound event before processing, so even a
        // crash mid-processing leaves an audit row.
        if (record == null)
        {
            record = new WebhookEvent
            {
                StripeEventId = stripeEvent.Id,
                EventType = stripeEvent.Type,
                Payload = payload,
            };
            _dbContext.WebhookEvents.Add(record);
        }
        record.Status = WebhookEventStatus.Received;
        record.AttemptCount++;
        record.Error = null;
        await _dbContext.SaveChangesAsync();

        try
        {
            switch (stripeEvent.Type)
            {
                case "checkout.session.completed":
                    await HandleCompletedAsync((Session)stripeEvent.Data.Object);
                    break;
                case "checkout.session.expired":
                    await HandleExpiredAsync((Session)stripeEvent.Data.Object);
                    break;
                default:
                    await MarkAsync(record.Id, WebhookEventStatus.Skipped, null);
                    return;
            }

            await MarkAsync(record.Id, WebhookEventStatus.Processed, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Webhook processing failed for {EventId}", stripeEvent.Id);
            await MarkAsync(record.Id, WebhookEventStatus.Failed, ex.Message);
            throw; // rethrow so Stripe retries (safe: idempotent by event id)
        }
    }

    private async Task HandleCompletedAsync(Session session)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.StripeSessionId == session.Id);

        if (order == null)
        {
            _logger.LogInformation("No order for completed session: {SessionId}", session.Id);
            return;
        }

        // Shared settle path: webhook and reconciliation both go through SettlePaidAsync,
        // which owns the Pending guard, the PaymentCompleted publish, and the atomic save.
        await _settlement.SettlePaidAsync(order, SettlementSource.Webhook);
    }

    private async Task HandleExpiredAsync(Session session)
    {
        var order = await _dbContext.Orders
            .FirstOrDefaultAsync(o => o.StripeSessionId == session.Id && o.Status == OrderStatus.Pending);

        if (order == null) return;

        order.Status = OrderStatus.Failed;
        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // Concurrently settled as Paid — saga will continue the success path, no need to cancel.
            _logger.LogInformation("Expired checkout concurrency conflict for session {StripeSessionId}, order may have been paid", session.Id);
            return;
        }

        // Saga receives this and orchestrates seat release via ReleaseSeatsCommand.
        // The saga already holds SeatIds from BookingInitiated — no need to re-fetch them here.
        await _publishEndpoint.Publish(new CheckoutSessionExpired { OrderId = order.Id });
        // Must save before returning: MarkAsync clears the tracker, losing the outbox message.
        await _dbContext.SaveChangesAsync();
    }

    // Update the audit row on a clean tracker so a failed business SaveChanges above
    // is not accidentally re-committed alongside the status update.
    private async Task MarkAsync(Guid id, WebhookEventStatus status, string? error)
    {
        _dbContext.ChangeTracker.Clear();
        var rec = await _dbContext.WebhookEvents.FirstOrDefaultAsync(w => w.Id == id);
        if (rec == null) return;
        rec.Status = status;
        rec.Error = Truncate(error);
        rec.ProcessedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
    }

    private async Task PersistAsync(WebhookEvent ev)
    {
        _dbContext.WebhookEvents.Add(ev);
        await _dbContext.SaveChangesAsync();
    }

    private static string? Truncate(string? s) =>
        s == null ? null : (s.Length > 2000 ? s[..2000] : s);
}

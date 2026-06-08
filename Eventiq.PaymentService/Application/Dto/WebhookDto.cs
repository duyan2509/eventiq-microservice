using Eventiq.PaymentService.Domain.Entity;

namespace Eventiq.PaymentService.Application.Dto;

// List view — omits the raw payload to stay light.
public record WebhookEventSummary(
    Guid Id,
    string StripeEventId,
    string EventType,
    string Status,
    int AttemptCount,
    string? Error,
    DateTime ReceivedAt,
    DateTime? ProcessedAt)
{
    public static WebhookEventSummary From(WebhookEvent w) => new(
        w.Id, w.StripeEventId, w.EventType, w.Status.ToString(),
        w.AttemptCount, w.Error, w.ReceivedAt, w.ProcessedAt);
}

// Detail view — includes the raw payload for tracing / replay.
public record WebhookEventDetail(
    Guid Id,
    string StripeEventId,
    string EventType,
    string Status,
    int AttemptCount,
    string? Error,
    string Payload,
    DateTime ReceivedAt,
    DateTime? ProcessedAt)
{
    public static WebhookEventDetail From(WebhookEvent w) => new(
        w.Id, w.StripeEventId, w.EventType, w.Status.ToString(),
        w.AttemptCount, w.Error, w.Payload, w.ReceivedAt, w.ProcessedAt);
}

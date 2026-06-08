using Eventiq.PaymentService.Domain.Enums;

namespace Eventiq.PaymentService.Domain.Entity;

/// <summary>
/// Audit log of every Stripe webhook received, so a failed delivery (bad signature
/// or processing error) is never lost and can be traced / replayed.
/// </summary>
public class WebhookEvent : BaseEntity
{
    // Stripe event id (evt_...). Empty when signature verification failed before parsing.
    public string StripeEventId { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public WebhookEventStatus Status { get; set; } = WebhookEventStatus.Received;

    // Raw request body — kept for tracing and manual replay.
    public string Payload { get; set; } = string.Empty;

    // Last error message when Status = Failed.
    public string? Error { get; set; }

    public int AttemptCount { get; set; }

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessedAt { get; set; }
}

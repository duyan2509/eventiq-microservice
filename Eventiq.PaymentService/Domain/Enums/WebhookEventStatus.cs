namespace Eventiq.PaymentService.Domain.Enums;

public enum WebhookEventStatus
{
    Received,   // signature verified, persisted, not yet processed
    Processed,  // handled successfully
    Failed,     // signature failed OR processing threw — kept for tracing / replay
    Skipped     // event type we don't handle
}

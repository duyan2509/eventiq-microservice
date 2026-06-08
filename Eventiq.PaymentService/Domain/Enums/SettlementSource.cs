namespace Eventiq.PaymentService.Domain.Enums;

/// <summary>
/// Which path settled an order as Paid. Null until the order is settled.
/// </summary>
public enum SettlementSource
{
    // Stripe pushed checkout.session.completed; the webhook handler settled it (normal path).
    Webhook,

    // The background reconciliation job polled Stripe and settled a stuck order (safety net).
    Reconciliation
}

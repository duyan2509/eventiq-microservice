namespace Eventiq.EventService.Domain.Repositories;

public interface IOrgPaymentRepository
{
    /// <summary>Returns true if the org has an active (configured) Stripe account.</summary>
    Task<bool> HasActivePaymentAsync(Guid orgId, CancellationToken ct = default);

    /// <summary>Upserts the payment status for an org (called by PaymentConfiguredConsumer).</summary>
    Task UpsertAsync(Guid orgId, string stripeAccountId, bool isActive, DateTime updatedAt, CancellationToken ct = default);
}

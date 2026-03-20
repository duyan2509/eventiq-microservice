namespace Eventiq.Contracts;

public record PaymentConfigured
{
    public Guid OrganizationId { get; init; }
    public string StripeAccountId { get; init; } = string.Empty;
    public string PaymentStatus { get; init; } = string.Empty;
    public DateTime ConfiguredAt { get; init; }
}

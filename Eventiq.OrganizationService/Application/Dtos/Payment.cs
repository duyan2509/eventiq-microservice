using Eventiq.OrganizationService.Domain.Enum;

namespace Eventiq.OrganizationService.Dtos;

public class PaymentConnectResponse
{
    public string OnboardingUrl { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
}

public class PaymentStatusResponse
{
    public Guid OrganizationId { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public string? StripeAccountId { get; set; }
    public DateTime? PaymentConfiguredAt { get; set; }
    public bool IsPaymentReady => PaymentStatus == PaymentStatus.Configured;
}

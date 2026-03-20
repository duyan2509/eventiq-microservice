namespace Eventiq.OrganizationService.Domain.Enum;

public enum PaymentStatus
{
    NotConfigured,
    Pending,      // Stripe account created, onboarding not completed
    Configured    // Onboarding completed, can receive payments
}

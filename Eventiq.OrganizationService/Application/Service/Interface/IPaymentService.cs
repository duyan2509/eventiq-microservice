using Eventiq.OrganizationService.Dtos;

namespace Eventiq.OrganizationService.Application.Service;

public interface IPaymentService
{
    /// <summary>
    /// Creates a Stripe Connected Account for the organization and returns the onboarding URL.
    /// </summary>
    Task<PaymentConnectResponse> ConnectStripeAccountAsync(Guid userId, Guid orgId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles the callback after Stripe onboarding — verifies the account and publishes PaymentConfigured message.
    /// </summary>
    Task<PaymentStatusResponse> HandleOnboardingCallbackAsync(Guid userId, Guid orgId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current payment configuration status for an organization.
    /// </summary>
    Task<PaymentStatusResponse> GetPaymentStatusAsync(Guid userId, Guid orgId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects the Stripe account from the organization.
    /// </summary>
    Task DisconnectStripeAccountAsync(Guid userId, Guid orgId, CancellationToken cancellationToken = default);
}

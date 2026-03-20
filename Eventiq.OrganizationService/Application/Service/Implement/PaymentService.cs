using Eventiq.Contracts;
using Eventiq.OrganizationService.Domain;
using Eventiq.OrganizationService.Domain.Entity;
using Eventiq.OrganizationService.Domain.Enum;
using Eventiq.OrganizationService.Domain.Repositories;
using Eventiq.OrganizationService.Dtos;
using Eventiq.OrganizationService.Guards;
using MassTransit;
using Stripe;

namespace Eventiq.OrganizationService.Application.Service;

public class PaymentService : IPaymentService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IOrganizationRepository organizationRepository,
        IUnitOfWork unitOfWork,
        IPublishEndpoint publishEndpoint,
        IConfiguration configuration,
        ILogger<PaymentService> logger)
    {
        _organizationRepository = organizationRepository;
        _unitOfWork = unitOfWork;
        _publishEndpoint = publishEndpoint;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<PaymentConnectResponse> ConnectStripeAccountAsync(
        Guid userId, Guid orgId, CancellationToken cancellationToken = default)
    {
        var org = await _organizationRepository.GetByIdAsync(orgId, cancellationToken);
        OrgGuards.EnsureExists(org);
        OwnerGuards.EnsureOwner(org, userId);

        if (org.PaymentStatus == PaymentStatus.Configured)
            throw new BusinessException("Payment is already configured for this organization");

        StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];

        string stripeAccountId;

        // Reuse existing Stripe account if available (e.g. previous incomplete onboarding)
        if (!string.IsNullOrEmpty(org.StripeAccountId))
        {
            stripeAccountId = org.StripeAccountId;
        }
        else
        {
            // Create a new Stripe Connected Account (Standard type)
            var accountOptions = new AccountCreateOptions
            {
                Type = "standard",
                Email = org.OwnerEmail,
                Metadata = new Dictionary<string, string>
                {
                    { "organization_id", orgId.ToString() },
                    { "owner_id", userId.ToString() }
                }
            };

            var accountService = new AccountService();
            var account = await accountService.CreateAsync(accountOptions, cancellationToken: cancellationToken);
            stripeAccountId = account.Id;

            org.StripeAccountId = stripeAccountId;
            org.PaymentStatus = PaymentStatus.Pending;
            await _organizationRepository.UpdateAsync(org, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Create an Account Link for onboarding
        var linkOptions = new AccountLinkCreateOptions
        {
            Account = stripeAccountId,
            RefreshUrl = _configuration["Stripe:RefreshUrl"]
                         ?? $"http://localhost:3000/organizations/{orgId}/payment/refresh",
            ReturnUrl = _configuration["Stripe:ReturnUrl"]
                        ?? $"http://localhost:3000/organizations/{orgId}/payment/return",
            Type = "account_onboarding"
        };

        var linkService = new AccountLinkService();
        var accountLink = await linkService.CreateAsync(linkOptions, cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Stripe onboarding link created for org {OrgId}, account {AccountId}",
            orgId, stripeAccountId);

        return new PaymentConnectResponse
        {
            OnboardingUrl = accountLink.Url,
            OrganizationId = orgId
        };
    }

    public async Task<PaymentStatusResponse> HandleOnboardingCallbackAsync(
        Guid userId, Guid orgId, CancellationToken cancellationToken = default)
    {
        var org = await _organizationRepository.GetByIdAsync(orgId, cancellationToken);
        OrgGuards.EnsureExists(org);
        OwnerGuards.EnsureOwner(org, userId);

        if (string.IsNullOrEmpty(org.StripeAccountId))
            throw new BusinessException("No Stripe account found. Please initiate payment setup first.");

        if (org.PaymentStatus == PaymentStatus.Configured)
            return ToPaymentStatusResponse(org);

        StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];

        // Verify the Stripe account status
        var accountService = new AccountService();
        var account = await accountService.GetAsync(org.StripeAccountId, cancellationToken: cancellationToken);

        if (!account.DetailsSubmitted)
        {
            _logger.LogWarning(
                "Stripe onboarding not completed for org {OrgId}, account {AccountId}",
                orgId, org.StripeAccountId);

            return new PaymentStatusResponse
            {
                OrganizationId = orgId,
                PaymentStatus = PaymentStatus.Pending,
                StripeAccountId = org.StripeAccountId,
                PaymentConfiguredAt = null
            };
        }

        // Onboarding completed — update org and publish message
        org.PaymentStatus = PaymentStatus.Configured;
        org.PaymentConfiguredAt = DateTime.UtcNow;
        await _organizationRepository.UpdateAsync(org, cancellationToken);

        await _publishEndpoint.Publish(new PaymentConfigured
        {
            OrganizationId = orgId,
            StripeAccountId = org.StripeAccountId,
            PaymentStatus = PaymentStatus.Configured.ToString(),
            ConfiguredAt = org.PaymentConfiguredAt.Value
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Payment configured successfully for org {OrgId}, account {AccountId}",
            orgId, org.StripeAccountId);

        return ToPaymentStatusResponse(org);
    }

    public async Task<PaymentStatusResponse> GetPaymentStatusAsync(
        Guid userId, Guid orgId, CancellationToken cancellationToken = default)
    {
        var org = await _organizationRepository.GetByIdAsync(orgId, cancellationToken);
        OrgGuards.EnsureExists(org);
        OwnerGuards.EnsureOwner(org, userId);

        return ToPaymentStatusResponse(org);
    }

    public async Task DisconnectStripeAccountAsync(
        Guid userId, Guid orgId, CancellationToken cancellationToken = default)
    {
        var org = await _organizationRepository.GetByIdAsync(orgId, cancellationToken);
        OrgGuards.EnsureExists(org);
        OwnerGuards.EnsureOwner(org, userId);

        if (string.IsNullOrEmpty(org.StripeAccountId))
            throw new BusinessException("No payment account configured for this organization");

        org.StripeAccountId = null;
        org.PaymentStatus = PaymentStatus.NotConfigured;
        org.PaymentConfiguredAt = null;
        await _organizationRepository.UpdateAsync(org, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Payment disconnected for org {OrgId}", orgId);
    }

    private static PaymentStatusResponse ToPaymentStatusResponse(Organization org)
    {
        return new PaymentStatusResponse
        {
            OrganizationId = org.Id,
            PaymentStatus = org.PaymentStatus,
            StripeAccountId = org.StripeAccountId,
            PaymentConfiguredAt = org.PaymentConfiguredAt
        };
    }
}

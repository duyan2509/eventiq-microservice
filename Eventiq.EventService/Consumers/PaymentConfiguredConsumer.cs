using Eventiq.Contracts;
using Eventiq.EventService.Domain.Repositories;
using MassTransit;

namespace Eventiq.EventService.Consumers;

/// <summary>
/// Receives PaymentConfigured from OrganizationService and updates the local
/// org_payment_info table so SubmissionService can check payment status without
/// calling OrgService over HTTP.
/// </summary>
public class PaymentConfiguredConsumer : IConsumer<PaymentConfigured>
{
    private readonly IOrgPaymentRepository _orgPaymentRepo;
    private readonly ILogger<PaymentConfiguredConsumer> _logger;

    public PaymentConfiguredConsumer(
        IOrgPaymentRepository orgPaymentRepo,
        ILogger<PaymentConfiguredConsumer> logger)
    {
        _orgPaymentRepo = orgPaymentRepo;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentConfigured> context)
    {
        var msg = context.Message;

        _logger.LogInformation(
            "PaymentConfigured received for org {OrgId}, Stripe={StripeAccountId}, Status={Status}",
            msg.OrganizationId, msg.StripeAccountId, msg.PaymentStatus);

        var isActive = msg.PaymentStatus.Equals("Configured", StringComparison.OrdinalIgnoreCase);

        await _orgPaymentRepo.UpsertAsync(
            orgId: msg.OrganizationId,
            stripeAccountId: msg.StripeAccountId,
            isActive: isActive,
            updatedAt: msg.ConfiguredAt);

        _logger.LogInformation(
            "org_payment_info upserted for org {OrgId}: IsActive={IsActive}",
            msg.OrganizationId, isActive);
    }
}

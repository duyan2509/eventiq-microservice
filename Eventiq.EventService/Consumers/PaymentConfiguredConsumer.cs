using Eventiq.Contracts;
using Eventiq.EventService.Domain.Repositories;
using MassTransit;

namespace Eventiq.EventService.Consumers;

public class PaymentConfiguredConsumer : IConsumer<PaymentConfigured>
{
    private readonly IEventRepository _eventRepository;
    private readonly ILogger<PaymentConfiguredConsumer> _logger;

    public PaymentConfiguredConsumer(
        IEventRepository eventRepository,
        ILogger<PaymentConfiguredConsumer> logger)
    {
        _eventRepository = eventRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentConfigured> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "Received PaymentConfigured for org {OrgId}, Stripe account {StripeAccountId}",
            message.OrganizationId, message.StripeAccountId);

        // Update all events of this organization that are in Approved status to Published
        // since payment is now configured
        var approvedEvents = await _eventRepository.GetEventsByOrgAndStatusAsync(
            message.OrganizationId,
            Domain.Entity.EventStatus.Approved);

        foreach (var evt in approvedEvents)
        {
            await _eventRepository.SetEventStatusAsync(evt.Id, Domain.Entity.EventStatus.Published);

            _logger.LogInformation(
                "Event {EventId} auto-published after payment configured for org {OrgId}",
                evt.Id, message.OrganizationId);
        }

        _logger.LogInformation(
            "PaymentConfigured processed successfully for org {OrgId}. {Count} events auto-published.",
            message.OrganizationId, approvedEvents.Count());
    }
}

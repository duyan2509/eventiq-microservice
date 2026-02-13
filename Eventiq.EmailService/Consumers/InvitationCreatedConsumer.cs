using Eventiq.Contracts;
using Eventiq.EmailService.Services;
using Eventiq.EmailService.Templates;
using Eventiq.EmailService.Templates.Models;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Eventiq.EmailService.Consumers;

public class InvitationCreatedConsumer(
    IEmailSender emailSender,
    ITemplateRenderer templateRenderer,
    ILogger<InvitationCreatedConsumer> logger) : IConsumer<InvitationCreated>
{
    public async Task Consume(ConsumeContext<InvitationCreated> context)
    {
        var message = context.Message;

        var model = new InvitationCreatedTemplateModel
        {
            OrganizationName = message.OrganizationName,
            EmailAddress = message.EmailAddress,
            PermissionName = message.PermissionName,
            ExpireAt = message.ExpireAt
        };

        var body = await templateRenderer.RenderInvitationCreatedAsync(model, context.CancellationToken);

        var subject = $"Invitation to join {message.OrganizationName}";

        try
        {
            await emailSender.SendEmailAsync(message.EmailAddress, subject, body, context.CancellationToken);
            logger.LogInformation("Processed InvitationCreated event for {Email}", message.EmailAddress);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send invitation email to {Email}", message.EmailAddress);
            throw;
        }
    }
}


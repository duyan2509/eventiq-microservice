using Eventiq.Contracts;
using Eventiq.EmailService.Services;
using Eventiq.EmailService.Templates;
using Eventiq.EmailService.Templates.Models;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Eventiq.EmailService.Consumers;

public class PasswordResetRequestedConsumer(
    IEmailSender emailSender,
    ITemplateRenderer templateRenderer,
    ILogger<PasswordResetRequestedConsumer> logger) : IConsumer<PasswordResetRequested>
{
    public async Task Consume(ConsumeContext<PasswordResetRequested> context)
    {
        var message = context.Message;

        var model = new PasswordResetTemplateModel
        {
            EmailAddress = message.EmailAddress,
            ResetToken = message.ResetToken,
            ExpireAt = message.ExpireAt
        };

        var body = await templateRenderer.RenderPasswordResetAsync(model, context.CancellationToken);

        var subject = "Eventiq - Password Reset Request";

        try
        {
            await emailSender.SendEmailAsync(message.EmailAddress, subject, body, context.CancellationToken);
            logger.LogInformation("Sent password reset email to {Email}", message.EmailAddress);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send password reset email to {Email}", message.EmailAddress);
            throw;
        }
    }
}

namespace Eventiq.EmailService.Services;

public interface IEmailSender
{
    Task SendEmailAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default);
}


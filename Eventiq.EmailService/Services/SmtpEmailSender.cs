using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Eventiq.EmailService.Services;

public class SmtpEmailSender(
    IConfiguration configuration,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendEmailAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        var host = configuration["Smtp:Host"];
        var port = configuration.GetValue<int?>("Smtp:Port") ?? 587;
        var username = configuration["Smtp:Username"];
        var password = configuration["Smtp:Password"];
        var from = configuration["Smtp:From"] ?? username;

        if (string.IsNullOrWhiteSpace(host) ||
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(from))
        {
            logger.LogError("SMTP configuration is missing. Please set Smtp:Host, Smtp:Port, Smtp:Username, Smtp:Password, and Smtp:From.");
            throw new InvalidOperationException("SMTP configuration is incomplete.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(from),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };

        message.To.Add(new MailAddress(to));
        var enableSsl = configuration.GetValue<bool?>("Smtp:EnableSsl") ?? true;

        using var client = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(username, password),
            EnableSsl = enableSsl
        };

        // SmtpClient does not support CancellationToken directly.
        await client.SendMailAsync(message);
        logger.LogInformation("Sent email to {Email}", to);
    }
}


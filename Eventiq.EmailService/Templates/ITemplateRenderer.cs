using Eventiq.EmailService.Templates.Models;

namespace Eventiq.EmailService.Templates;

public interface ITemplateRenderer
{
    Task<string> RenderInvitationCreatedAsync(
        InvitationCreatedTemplateModel model,
        CancellationToken cancellationToken = default);

    Task<string> RenderPasswordResetAsync(
        PasswordResetTemplateModel model,
        CancellationToken cancellationToken = default);
}

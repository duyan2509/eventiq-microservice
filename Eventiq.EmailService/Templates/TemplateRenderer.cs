using Eventiq.EmailService.Templates.Models;
using RazorLight;

namespace Eventiq.EmailService.Templates;

public class TemplateRenderer(IRazorLightEngine engine) : ITemplateRenderer
{
    public Task<string> RenderInvitationCreatedAsync(
        InvitationCreatedTemplateModel model,
        CancellationToken cancellationToken = default)
    {
        return engine.CompileRenderAsync("InvitationCreated", model);
    }

    public Task<string> RenderPasswordResetAsync(
        PasswordResetTemplateModel model,
        CancellationToken cancellationToken = default)
    {
        return engine.CompileRenderAsync("PasswordReset", model);
    }
}

using Eventiq.EmailService.Templates.Models;
using RazorLight;

namespace Eventiq.EmailService.Templates;

public class TemplateRenderer(IRazorLightEngine engine) : ITemplateRenderer
{
    public Task<string> RenderInvitationCreatedAsync(
        InvitationCreatedTemplateModel model,
        CancellationToken cancellationToken = default)
    {
        // Template key is the filename without extension
        return engine.CompileRenderAsync("InvitationCreated", model);
    }
}


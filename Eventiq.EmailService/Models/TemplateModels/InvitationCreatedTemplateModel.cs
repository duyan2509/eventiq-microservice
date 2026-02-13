namespace Eventiq.EmailService.Templates.Models;

public class InvitationCreatedTemplateModel
{
    public string OrganizationName { get; set; } = default!;
    public string EmailAddress { get; set; } = default!;
    public string PermissionName { get; set; } = default!;
    public DateTime ExpireAt { get; set; }
}


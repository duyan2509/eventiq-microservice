namespace Eventiq.Contracts;

public record InvitationCreated
{
    public string OrganizationName { get; set; } 
    public string EmailAddress { get; set; }
    public string PermissionName { get; set; }
    public DateTime ExpireAt { get; set; }
}
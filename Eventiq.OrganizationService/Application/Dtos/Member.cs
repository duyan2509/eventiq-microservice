namespace Eventiq.OrganizationService.Dtos;

public class MemberReponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; }
    public string PermissionName { get; set; }
    
}

public class ChangePermission
{
    public Guid PermissionId { get; set; }
}
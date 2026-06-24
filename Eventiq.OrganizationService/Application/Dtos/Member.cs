namespace Eventiq.OrganizationService.Dtos;

public class MemberReponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; }
    public string PermissionName { get; set; }
    public bool IsDesigner { get; set; }
}

public class ChangePermission
{
    public Guid PermissionId { get; set; }
}

public class UserOrganizationItem
{
    public Guid OrgId { get; set; }
    public string OrgName { get; set; }
    public string RoleName { get; set; }
}
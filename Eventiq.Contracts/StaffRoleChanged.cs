namespace Eventiq.Contracts;

public record StaffRoleChanged
{
    public Guid UserId { get; init; }
    public Guid OrganizationId { get; init; }
    public string NewRoleName { get; init; } = string.Empty;
}

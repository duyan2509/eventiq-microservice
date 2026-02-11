namespace Eventiq.Contracts;

public record StaffAccepted
{
    public Guid UserId { get; init; }
    public Guid OrganizationId { get; init; }
}
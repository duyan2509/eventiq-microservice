namespace Eventiq.Contracts;

public record StaffRemoved
{
    public Guid UserId { get; init; }
    public Guid OrganizationId { get; init; }
}
namespace Eventiq.Contracts;

public record OrganizationCreated
{
    public Guid OrganizationId { get; init; }
    public Guid OwnerId { get; init; }
}

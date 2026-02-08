namespace Eventiq.Contracts;

public record OrganizationCreated
{
    public Guid OwnerId { get; init; }
    public Guid OrganizationId { get; init; }
}

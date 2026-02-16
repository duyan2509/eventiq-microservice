using Eventiq.EventService.Domain.Entity;

namespace Eventiq.EventService.Infrastructure.Persistence.ReadModel;

public class EventModel
{
    public Guid OrganizationId { get; set; }
    public Guid Id { get; set; }
    public EventStatus Status { get; set; }
}
using Eventiq.EventService.Domain.Entity;

namespace Eventiq.EventService.Infrastructure.Persistence.ReadModel;

public class SubmissionModel
{
    public Guid Id { get; set; }
    public string AdminEmail { get; set; }
    public Guid AdminId { get; set; }
    public string Message { get; set; }
    public EventStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
using Eventiq.EventService.Domain.Entity;

namespace Eventiq.EventService.Dtos;

public class UpdateSubmissioDto
{
    public EventStatus Status { get; set; }
    public string? Message { get; set; }
}


public class SubmissionResponse
{
    public string AdminEmail { get; set; }
    public Guid AdminId { get; set; }
    public string Message { get; set; }
    public EventStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
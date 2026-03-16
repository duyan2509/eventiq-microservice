using Eventiq.EventService.Domain.Entity;

namespace Eventiq.EventService.Infrastructure.Persistence.ReadModel;

public class EventModel
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public EventStatus Status { get; set; }

    public string? EventBanner { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DetailAddress { get; set; }
    public string? ProvinceCode { get; set; }
    public string? CommuneCode { get; set; }
    public string? ProvinceName { get; set; }
    public string? CommuneName { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    // For listing
    public int? LowestPrice { get; set; }
}
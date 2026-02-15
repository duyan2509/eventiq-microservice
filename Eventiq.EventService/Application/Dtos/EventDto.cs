namespace Eventiq.EventService.Dtos;

public class UpdateEventDto
{
    //public required Stream BannerStream { get; set; }
    //public string EventBanner { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? DetailAddress { get; set; }
    public string? ProvinceCode { get; set; }
    public string? CommuneCode { get; set; }
    public string? ProvinceName { get; set; }
    public string? CommuneName { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}

public class CreateEventDto
{
    //public required Stream BannerStream { get; set; }
    //public string EventBanner { get; set; } = string.Empty;
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string DetailAddress { get; set; }
    public required string ProvinceCode { get; set; }
    public required string CommuneCode { get; set; }
    public required string ProvinceName { get; set; }
    public required string CommuneName { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}


public class EventQuickViewData
{
    public Guid Id { get; set; }
    public string? EventBanner { get; set; }
    public required string Name { get; set; }
    public required DateTime Start { get; set; }
    public required string Status { get; set; }
    public int? LowestPrice { get; set; }
    public string? ProvinceName { get; set; }
}

public class EventDetail
{
    public Guid Id { get; set; }
    public string? EventBanner { get; set; }
    public required string Name { get; set; }
    public required DateTime StartTime { get; set; }
    public required DateTime EndTime { get; set; }
    public string? Description { get; set; }
    public required string Status { get; set; }
    public string? DetailAddress { get; set; }
    public string? ProvinceCode { get; set; }
    public string? CommuneCode { get; set; }
    public string? ProvinceName { get; set; }
    public string? CommuneName { get; set; }
    public ICollection<SessionResponse> Sessions { get; set; } = new List<SessionResponse>();
    public ICollection<LegendResponse> Legends { get; set; } = new List<LegendResponse>();
}

public class CreateEventRequest
{
    public required IFormFile EventBanner { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string DetailAddress { get; set; }
    public required string ProvinceCode { get; set; }
    public required string CommuneCode { get; set; }
    public required string ProvinceName { get; set; }
    public required string CommuneName { get; set; }
    public required DateTime StartTime { get; set; }
    public required DateTime EndTime { get; set; }
}

public class UpdateEventRequest
{
    public IFormFile EventBanner { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public Guid? OrganizationId { get; set; }
}
using Eventiq.SeatService.Domain.Enum;

namespace Eventiq.SeatService.Application.Dtos;

// ========== SeatMap DTOs ==========

public class CreateSeatMapDto
{
    public Guid ChartId { get; set; }
    public Guid EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CanvasSettings { get; set; }
}

public class UpdateSeatMapSettingsDto
{
    public string? Name { get; set; }
    public string? CanvasSettings { get; set; }
}

public class SeatMapResponse
{
    public Guid Id { get; set; }
    public Guid ChartId { get; set; }
    public Guid EventId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? SessionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? CanvasSettings { get; set; }
    public int Version { get; set; }
    public int TotalSeats { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class SeatMapDetailResponse : SeatMapResponse
{
    public List<SeatResponse> Seats { get; set; } = [];
    public List<SeatObjectResponse> Objects { get; set; } = [];
}

// Layout-only response for the booking view — no seat statuses, safe to cache.
public class SeatMapLayoutResponse : SeatMapResponse
{
    public List<SeatLayoutResponse> Seats { get; set; } = [];
    public List<SeatObjectResponse> Objects { get; set; } = [];
}

// Bounding box in canvas coordinates.
public class BboxDto
{
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
}

// Metadata-only response (no seats) — used to bootstrap both the design editor
// and the booking view before seats are streamed in by bounding box.
public class SeatMapMetaResponse : SeatMapResponse
{
    public List<SeatObjectResponse> Objects { get; set; } = [];
    public BboxDto FullBbox { get; set; } = new();
}

// A viewport chunk of seats (layout only, no statuses) for the booking view.
public class SeatLayoutChunkResponse
{
    public List<SeatLayoutResponse> Seats { get; set; } = [];
    public BboxDto Bbox { get; set; } = new();
}

// ========== Seat DTOs ==========

public class AddSeatDto
{
    public Guid SeatMapId { get; set; }
    public int SeatType { get; set; } = 1;
    public string? Position { get; set; }
    public Guid? LegendId { get; set; }
}

public class AddSeatsBatchDto
{
    public Guid SeatMapId { get; set; }
    public int SeatType { get; set; } = 1;
    public List<string> Positions { get; set; } = [];
    public Guid? LegendId { get; set; }
}

public class UpdateSeatDto
{
    public Guid SeatId { get; set; }
    public string? Label { get; set; }
    public int? SeatNumber { get; set; }
    public SeatStatus? Status { get; set; }
    public int? SeatType { get; set; }
    public string? Position { get; set; }
    public Guid? LegendId { get; set; }
    public string? CustomProperties { get; set; }
}

public class BatchUpdateSeatsDto
{
    public List<UpdateSeatDto> Seats { get; set; } = [];
}

public class SeatResponse
{
    public Guid Id { get; set; }
    public Guid SeatMapId { get; set; }
    public string Label { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public int SeatType { get; set; }
    public string? Position { get; set; }
    public Guid? LegendId { get; set; }
    public string? CustomProperties { get; set; }
}

public class SeatLayoutResponse
{
    public Guid Id { get; set; }
    public Guid SeatMapId { get; set; }
    public string Label { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
    public int SeatType { get; set; }
    public string? Position { get; set; }
    public Guid? LegendId { get; set; }
    public string? CustomProperties { get; set; }
}

// ========== SeatObject DTOs ==========

public class AddObjectDto
{
    public SeatObjectType ObjectType { get; set; }
    public string? Label { get; set; }
    public string? Geometry { get; set; }
    public string? Style { get; set; }
    public int ZIndex { get; set; }
}

public class UpdateObjectDto
{
    public Guid ObjectId { get; set; }
    public SeatObjectType? ObjectType { get; set; }
    public string? Label { get; set; }
    public string? Geometry { get; set; }
    public string? Style { get; set; }
    public int? ZIndex { get; set; }
}

public class SeatObjectResponse
{
    public Guid Id { get; set; }
    public Guid SeatMapId { get; set; }
    public string ObjectType { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string? Geometry { get; set; }
    public string? Style { get; set; }
    public int ZIndex { get; set; }
}

// ========== Version DTOs ==========

public class CreateVersionDto
{
    public string? ChangeDescription { get; set; }
}

public class SeatMapVersionResponse
{
    public Guid Id { get; set; }
    public Guid SeatMapId { get; set; }
    public int VersionNumber { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ChangeDescription { get; set; }
}

public class SeatMapVersionDetailResponse : SeatMapVersionResponse
{
    public string Snapshot { get; set; } = string.Empty;
}

// ========== Collaboration DTOs ==========

public class CursorDto
{
    public double X { get; set; }
    public double Y { get; set; }
}

public class SelectionDto
{
    public List<Guid> ElementIds { get; set; } = [];
}

public class UserPresenceDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string AvatarColor { get; set; } = string.Empty;
}

public class SeatMapStatsResponse
{
    public int TotalSeats { get; set; }
    public int AvailableSeats { get; set; }
    public int HoldingSeats { get; set; }
    public int SoldSeats { get; set; }
    public int BlockedSeats { get; set; }
}

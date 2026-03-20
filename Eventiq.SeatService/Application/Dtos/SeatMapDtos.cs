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
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? CanvasSettings { get; set; }
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class SeatMapDetailResponse : SeatMapResponse
{
    public List<SeatSectionResponse> Sections { get; set; } = [];
    public List<SeatObjectResponse> Objects { get; set; } = [];
}

// ========== SeatSection DTOs ==========

public class AddSectionDto
{
    public string Label { get; set; } = string.Empty;
    public SectionType SectionType { get; set; } = SectionType.Rectangle;
    public string? Geometry { get; set; }
    public string? Style { get; set; }
    public Guid? LegendId { get; set; }
    public int SortOrder { get; set; }
}

public class UpdateSectionDto
{
    public Guid SectionId { get; set; }
    public string? Label { get; set; }
    public SectionType? SectionType { get; set; }
    public string? Geometry { get; set; }
    public string? Style { get; set; }
    public Guid? LegendId { get; set; }
    public int? SortOrder { get; set; }
}

public class SeatSectionResponse
{
    public Guid Id { get; set; }
    public Guid SeatMapId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string SectionType { get; set; } = string.Empty;
    public string? Geometry { get; set; }
    public string? Style { get; set; }
    public Guid? LegendId { get; set; }
    public int SortOrder { get; set; }
    public List<SeatRowResponse> Rows { get; set; } = [];
}

// ========== SeatRow DTOs ==========

public class AddRowDto
{
    public Guid SectionId { get; set; }
    public string Label { get; set; } = string.Empty;
    public int RowNumber { get; set; }
    public string? Curve { get; set; }
    public int SeatSpacing { get; set; } = 30;
    public int SeatCount { get; set; }
    public string? LabelPrefix { get; set; }
}

public class UpdateRowDto
{
    public Guid RowId { get; set; }
    public string? Label { get; set; }
    public int? RowNumber { get; set; }
    public string? Curve { get; set; }
    public int? SeatSpacing { get; set; }
}

public class SeatRowResponse
{
    public Guid Id { get; set; }
    public Guid SectionId { get; set; }
    public string Label { get; set; } = string.Empty;
    public int RowNumber { get; set; }
    public string? Curve { get; set; }
    public int SeatSpacing { get; set; }
    public List<SeatResponse> Seats { get; set; } = [];
}

// ========== Seat DTOs ==========

public class AddSeatDto
{
    public Guid RowId { get; set; }
    public string Label { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
    public SeatType SeatType { get; set; } = SeatType.Regular;
    public string? Position { get; set; }
    public Guid? LegendId { get; set; }
}

public class UpdateSeatDto
{
    public Guid SeatId { get; set; }
    public string? Label { get; set; }
    public int? SeatNumber { get; set; }
    public SeatStatus? Status { get; set; }
    public SeatType? SeatType { get; set; }
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
    public Guid RowId { get; set; }
    public string Label { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string SeatType { get; set; } = string.Empty;
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
    public int ReservedSeats { get; set; }
    public int SoldSeats { get; set; }
    public int BlockedSeats { get; set; }
    public int TotalSections { get; set; }
    public int TotalRows { get; set; }
}

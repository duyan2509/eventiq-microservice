using Eventiq.SeatService.Application.Dtos;

namespace Eventiq.SeatService.Application.Service.Interface;

/// <summary>
/// Orchestrates real-time design operations called from SignalR Hub.
/// All methods persist + return data for broadcast.
/// </summary>
public interface ISeatDesignService
{
    // Sections
    Task<SeatSectionResponse> AddSectionAsync(Guid seatMapId, Guid orgId, AddSectionDto dto);
    Task<SeatSectionResponse> UpdateSectionAsync(Guid seatMapId, Guid orgId, UpdateSectionDto dto);
    Task DeleteSectionAsync(Guid seatMapId, Guid orgId, Guid sectionId);

    // Rows
    Task<SeatRowResponse> AddRowAsync(Guid seatMapId, Guid orgId, AddRowDto dto);
    Task<SeatRowResponse> UpdateRowAsync(Guid seatMapId, Guid orgId, UpdateRowDto dto);
    Task DeleteRowAsync(Guid seatMapId, Guid orgId, Guid rowId);

    // Seats
    Task<SeatResponse> AddSeatAsync(Guid seatMapId, Guid orgId, AddSeatDto dto);
    Task<List<SeatResponse>> BatchUpdateSeatsAsync(Guid seatMapId, Guid orgId, BatchUpdateSeatsDto dto);
    Task DeleteSeatsAsync(Guid seatMapId, Guid orgId, List<Guid> seatIds);

    // Objects
    Task<SeatObjectResponse> AddObjectAsync(Guid seatMapId, Guid orgId, AddObjectDto dto);
    Task<SeatObjectResponse> UpdateObjectAsync(Guid seatMapId, Guid orgId, UpdateObjectDto dto);
    Task DeleteObjectAsync(Guid seatMapId, Guid orgId, Guid objectId);

    // Auto-save snapshot
    Task<SeatMapVersionResponse> AutoSaveSnapshotAsync(Guid seatMapId, Guid userId, string? description = null);
}

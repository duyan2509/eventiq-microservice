using Eventiq.SeatService.Application.Dtos;

namespace Eventiq.SeatService.Application.Service.Interface;

/// <summary>
/// Orchestrates real-time design operations called from SignalR Hub.
/// All methods persist + return data for broadcast.
/// </summary>
public interface ISeatDesignService
{
    // Seats
    Task<SeatResponse> AddSeatAsync(Guid seatMapId, Guid orgId, AddSeatDto dto);
    Task<List<SeatResponse>> AddSeatsAsync(Guid seatMapId, Guid orgId, AddSeatsBatchDto dto);
    Task<SeatMutationResult> BatchUpdateSeatsAsync(Guid seatMapId, Guid orgId, BatchUpdateSeatsDto dto);
    Task<SeatMutationResult> SetSeatLegendAsync(Guid seatMapId, Guid orgId, List<Guid> seatIds, Guid? legendId, Dictionary<Guid, int>? expectedStyleVersions = null);
    Task DeleteSeatsAsync(Guid seatMapId, Guid orgId, List<Guid> seatIds);

    // Objects
    Task<SeatObjectResponse> AddObjectAsync(Guid seatMapId, Guid orgId, AddObjectDto dto);
    Task<SeatObjectResponse> UpdateObjectAsync(Guid seatMapId, Guid orgId, UpdateObjectDto dto);
    Task DeleteObjectAsync(Guid seatMapId, Guid orgId, Guid objectId);

    // Auto-save snapshot
    Task<SeatMapVersionResponse> AutoSaveSnapshotAsync(Guid seatMapId, Guid userId, string? description = null);

    Task<Guid> GetSeatMapOrgIdAsync(Guid seatMapId);
}

using Eventiq.SeatService.Application.Dtos;

namespace Eventiq.SeatService.Application.Service.Interface;

public interface ISeatMapService
{
    Task<List<SeatMapResponse>> GetByEventIdAsync(Guid eventId);

    // Design: metadata (objects + bounds, no seats) then all seats in a separate call.
    Task<SeatMapMetaResponse> GetByIdAsync(Guid id);
    Task<List<SeatResponse>> GetSeatsAsync(Guid seatMapId);

    // Booking: metadata then viewport chunks of seats by bounding box.
    Task<SeatMapMetaResponse> GetSessionMetaAsync(Guid sessionId);
    Task<SeatLayoutChunkResponse> GetSessionSeatsAsync(Guid sessionId, BboxDto? bbox);
    Task<SeatMapResponse> CreateAsync(Guid userId, Guid orgId, CreateSeatMapDto dto);
    Task<SeatMapResponse> UpdateSettingsAsync(Guid userId, Guid orgId, Guid seatMapId, UpdateSeatMapSettingsDto dto);
    Task DeleteAsync(Guid orgId, Guid seatMapId);
    Task<SeatMapResponse> PublishAsync(Guid orgId, Guid seatMapId);
    Task<SeatMapStatsResponse> GetStatsAsync(Guid seatMapId);
    Task<bool> HasPublishedTemplateForEventAsync(Guid eventId);
    Task<bool> HasSeatMapDesignAsync(Guid eventId);
    Task<SeatMapResponse> RecoverSessionCloneAsync(Guid sessionId, Guid? eventId);
    Task<SeatMapResponse?> CloneForSessionAsync(Guid sessionId, Guid chartId, Guid eventId);
}

using Eventiq.SeatService.Application.Dtos;

namespace Eventiq.SeatService.Application.Service.Interface;

public interface ISeatMapService
{
    Task<List<SeatMapResponse>> GetByEventIdAsync(Guid eventId);
    Task<SeatMapDetailResponse> GetByIdAsync(Guid id);
    Task<SeatMapResponse> CreateAsync(Guid userId, Guid orgId, CreateSeatMapDto dto);
    Task<SeatMapResponse> UpdateSettingsAsync(Guid userId, Guid orgId, Guid seatMapId, UpdateSeatMapSettingsDto dto);
    Task DeleteAsync(Guid orgId, Guid seatMapId);
    Task<SeatMapResponse> PublishAsync(Guid orgId, Guid seatMapId);
    Task<SeatMapStatsResponse> GetStatsAsync(Guid seatMapId);
}

using Eventiq.SeatService.Domain.Entity;

namespace Eventiq.SeatService.Domain.Repositories;

public interface ISeatMapRepository
{
    Task<SeatMap?> GetByIdAsync(Guid id);
    Task<SeatMap?> GetByIdWithDetailsAsync(Guid id);
    Task<SeatMap?> GetByIdWithObjectsAsync(Guid id);
    Task<SeatMap?> GetBySessionIdWithObjectsAsync(Guid sessionId);
    Task<SeatMap?> GetByChartIdAsync(Guid chartId);
    Task<SeatMap?> GetPublishedTemplateByChartIdAsync(Guid chartId);
    Task<SeatMap?> GetTemplateByChartIdWithDetailsAsync(Guid chartId);
    Task<bool> HasTemplateForEventAsync(Guid eventId);
    Task<SeatMap?> GetBySessionIdAsync(Guid sessionId);
    Task<SeatMap?> GetBySessionIdWithDetailsAsync(Guid sessionId);
    Task<List<SeatMap>> GetByEventIdAsync(Guid eventId);
    Task<List<SeatMap>> GetByOrganizationIdAsync(Guid organizationId);
    Task<SeatMap> AddAsync(SeatMap seatMap);
    Task UpdateAsync(SeatMap seatMap);
    Task<bool> DeleteAsync(Guid id);
    Task<int> IncrementAndGetNextSeatNumberAsync(Guid seatMapId);
    Task<int> IncrementAndGetNextSeatNumberByAsync(Guid seatMapId, int count);
}

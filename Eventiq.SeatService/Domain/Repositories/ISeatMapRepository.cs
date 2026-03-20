using Eventiq.SeatService.Domain.Entity;

namespace Eventiq.SeatService.Domain.Repositories;

public interface ISeatMapRepository
{
    Task<SeatMap?> GetByIdAsync(Guid id);
    Task<SeatMap?> GetByIdWithDetailsAsync(Guid id);
    Task<SeatMap?> GetByChartIdAsync(Guid chartId);
    Task<List<SeatMap>> GetByEventIdAsync(Guid eventId);
    Task<List<SeatMap>> GetByOrganizationIdAsync(Guid organizationId);
    Task<SeatMap> AddAsync(SeatMap seatMap);
    Task UpdateAsync(SeatMap seatMap);
    Task<bool> DeleteAsync(Guid id);
}

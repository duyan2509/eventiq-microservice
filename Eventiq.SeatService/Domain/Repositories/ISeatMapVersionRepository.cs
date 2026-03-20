using Eventiq.SeatService.Domain.Entity;

namespace Eventiq.SeatService.Domain.Repositories;

public interface ISeatMapVersionRepository
{
    Task<SeatMapVersion?> GetByIdAsync(Guid id);
    Task<List<SeatMapVersion>> GetBySeatMapIdAsync(Guid seatMapId);
    Task<SeatMapVersion?> GetLatestAsync(Guid seatMapId);
    Task<SeatMapVersion> AddAsync(SeatMapVersion version);
}

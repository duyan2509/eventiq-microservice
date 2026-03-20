using Eventiq.SeatService.Domain.Entity;

namespace Eventiq.SeatService.Domain.Repositories;

public interface ISeatObjectRepository
{
    Task<SeatObject?> GetByIdAsync(Guid id);
    Task<List<SeatObject>> GetBySeatMapIdAsync(Guid seatMapId);
    Task<SeatObject> AddAsync(SeatObject seatObject);
    Task UpdateAsync(SeatObject seatObject);
    Task<bool> DeleteAsync(Guid id);
}

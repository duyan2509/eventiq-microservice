using Eventiq.SeatService.Domain.Entity;

namespace Eventiq.SeatService.Domain.Repositories;

public interface ISeatRepository
{
    Task<Seat?> GetByIdAsync(Guid id);
    Task<List<Seat>> GetByRowIdAsync(Guid rowId);
    Task<List<Seat>> GetBySeatMapIdAsync(Guid seatMapId);
    Task AddRangeAsync(IEnumerable<Seat> seats);
    Task UpdateAsync(Seat seat);
    Task UpdateRangeAsync(IEnumerable<Seat> seats);
    Task<bool> DeleteAsync(Guid id);
    Task DeleteRangeAsync(IEnumerable<Guid> ids);
}

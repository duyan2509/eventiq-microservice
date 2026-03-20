using Eventiq.SeatService.Domain.Entity;

namespace Eventiq.SeatService.Domain.Repositories;

public interface ISeatSectionRepository
{
    Task<SeatSection?> GetByIdAsync(Guid id);
    Task<SeatSection?> GetByIdWithRowsAsync(Guid id);
    Task<List<SeatSection>> GetBySeatMapIdAsync(Guid seatMapId);
    Task<SeatSection> AddAsync(SeatSection section);
    Task UpdateAsync(SeatSection section);
    Task<bool> DeleteAsync(Guid id);
}

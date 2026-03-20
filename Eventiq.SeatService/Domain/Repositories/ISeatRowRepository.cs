using Eventiq.SeatService.Domain.Entity;

namespace Eventiq.SeatService.Domain.Repositories;

public interface ISeatRowRepository
{
    Task<SeatRow?> GetByIdAsync(Guid id);
    Task<SeatRow?> GetByIdWithSeatsAsync(Guid id);
    Task<List<SeatRow>> GetBySectionIdAsync(Guid sectionId);
    Task<SeatRow> AddAsync(SeatRow row);
    Task UpdateAsync(SeatRow row);
    Task<bool> DeleteAsync(Guid id);
}

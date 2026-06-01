using Eventiq.SeatService.Domain.Entity;

namespace Eventiq.SeatService.Domain.Repositories;

public interface ISeatRepository
{
    Task<Seat?> GetByIdAsync(Guid id);
    Task<List<Seat>> GetByIdsAsync(IEnumerable<Guid> ids);
    Task<List<Seat>> GetBySeatMapIdAsync(Guid seatMapId);
    Task<List<Seat>> GetByBboxAsync(Guid seatMapId, double x1, double y1, double x2, double y2);
    Task<SeatBounds> GetSeatBoundsAsync(Guid seatMapId);
    Task AddRangeAsync(IEnumerable<Seat> seats);
    Task UpdateAsync(Seat seat);
    Task UpdateRangeAsync(IEnumerable<Seat> seats);
    Task<bool> DeleteAsync(Guid id);
    Task DeleteRangeAsync(IEnumerable<Guid> ids);
    Task<List<Seat>> GetExpiredHoldingAsync(DateTime cutoff);
}

/// <summary>Aggregate bounds of a seat map's seats (in canvas coordinates) plus seat count.</summary>
public readonly record struct SeatBounds(double MinX, double MinY, double MaxX, double MaxY, int Total);

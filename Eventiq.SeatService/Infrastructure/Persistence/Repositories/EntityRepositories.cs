using Eventiq.SeatService.Domain.Entity;
using Eventiq.SeatService.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Eventiq.SeatService.Infrastructure.Persistence.Repositories;

public class SeatRepository : ISeatRepository
{
    private readonly SeatDbContext _ctx;

    public SeatRepository(SeatDbContext ctx) => _ctx = ctx;

    public async Task<Seat?> GetByIdAsync(Guid id)
        => await _ctx.Seats.FindAsync(id);

    public async Task<List<Seat>> GetByIdsAsync(IEnumerable<Guid> ids)
        => await _ctx.Seats
            .Where(s => ids.Contains(s.Id))
            .ToListAsync();

    public async Task<List<Seat>> GetBySeatMapIdAsync(Guid seatMapId)
        => await _ctx.Seats
            .Where(s => s.SeatMapId == seatMapId)
            .OrderBy(s => s.SeatNumber)
            .ToListAsync();

    public async Task<List<Seat>> GetByBboxAsync(Guid seatMapId, double x1, double y1, double x2, double y2)
        => await _ctx.Seats
            .Where(s => s.SeatMapId == seatMapId
                && s.PositionX >= x1 && s.PositionX <= x2
                && s.PositionY >= y1 && s.PositionY <= y2)
            .OrderBy(s => s.SeatNumber)
            .ToListAsync();

    public async Task<SeatBounds> GetSeatBoundsAsync(Guid seatMapId)
    {
        var agg = await _ctx.Seats
            .Where(s => s.SeatMapId == seatMapId && s.PositionX != null && s.PositionY != null)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                MinX = g.Min(s => s.PositionX!.Value),
                MinY = g.Min(s => s.PositionY!.Value),
                MaxX = g.Max(s => s.PositionX!.Value),
                MaxY = g.Max(s => s.PositionY!.Value),
            })
            .FirstOrDefaultAsync();

        // Total counts every seat, including those without a position.
        var total = await _ctx.Seats.CountAsync(s => s.SeatMapId == seatMapId);

        return agg is null
            ? new SeatBounds(0, 0, 0, 0, total)
            : new SeatBounds(agg.MinX, agg.MinY, agg.MaxX, agg.MaxY, total);
    }

    public async Task AddRangeAsync(IEnumerable<Seat> seats)
    {
        await _ctx.Seats.AddRangeAsync(seats);
    }

    public Task UpdateAsync(Seat seat)
    {
        _ctx.Seats.Update(seat);
        return Task.CompletedTask;
    }

    public Task UpdateRangeAsync(IEnumerable<Seat> seats)
    {
        _ctx.Seats.UpdateRange(seats);
        return Task.CompletedTask;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _ctx.Seats.FindAsync(id);
        if (entity == null) return false;
        entity.MarkDeleted();
        return true;
    }

    public async Task DeleteRangeAsync(IEnumerable<Guid> ids)
    {
        var seats = await _ctx.Seats.Where(s => ids.Contains(s.Id)).ToListAsync();
        foreach (var seat in seats)
            seat.MarkDeleted();
    }

    public async Task<List<Seat>> GetExpiredHoldingAsync(DateTime cutoff)
        => await _ctx.Seats
            .Where(s => s.Status == Domain.Enum.SeatStatus.Holding && s.HeldUntil < cutoff)
            .ToListAsync();
}

public class SeatObjectRepository : ISeatObjectRepository
{
    private readonly SeatDbContext _ctx;

    public SeatObjectRepository(SeatDbContext ctx) => _ctx = ctx;

    public async Task<SeatObject?> GetByIdAsync(Guid id)
        => await _ctx.Objects.FindAsync(id);

    public async Task<List<SeatObject>> GetBySeatMapIdAsync(Guid seatMapId)
        => await _ctx.Objects
            .Where(o => o.SeatMapId == seatMapId)
            .OrderBy(o => o.ZIndex)
            .ToListAsync();

    public async Task<SeatObject> AddAsync(SeatObject seatObject)
    {
        await _ctx.Objects.AddAsync(seatObject);
        return seatObject;
    }

    public Task UpdateAsync(SeatObject seatObject)
    {
        _ctx.Objects.Update(seatObject);
        return Task.CompletedTask;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _ctx.Objects.FindAsync(id);
        if (entity == null) return false;
        entity.MarkDeleted();
        return true;
    }
}

public class SeatMapVersionRepository : ISeatMapVersionRepository
{
    private readonly SeatDbContext _ctx;

    public SeatMapVersionRepository(SeatDbContext ctx) => _ctx = ctx;

    public async Task<SeatMapVersion?> GetByIdAsync(Guid id)
        => await _ctx.Versions.FindAsync(id);

    public async Task<List<SeatMapVersion>> GetBySeatMapIdAsync(Guid seatMapId)
        => await _ctx.Versions
            .Where(v => v.SeatMapId == seatMapId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync();

    public async Task<SeatMapVersion?> GetLatestAsync(Guid seatMapId)
        => await _ctx.Versions
            .Where(v => v.SeatMapId == seatMapId)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync();

    public async Task<SeatMapVersion> AddAsync(SeatMapVersion version)
    {
        await _ctx.Versions.AddAsync(version);
        return version;
    }
}

using Eventiq.SeatService.Domain.Entity;
using Eventiq.SeatService.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Eventiq.SeatService.Infrastructure.Persistence.Repositories;

public class SeatSectionRepository : ISeatSectionRepository
{
    private readonly SeatDbContext _ctx;

    public SeatSectionRepository(SeatDbContext ctx) => _ctx = ctx;

    public async Task<SeatSection?> GetByIdAsync(Guid id)
        => await _ctx.Sections.FindAsync(id);

    public async Task<SeatSection?> GetByIdWithRowsAsync(Guid id)
        => await _ctx.Sections
            .Include(s => s.Rows)
                .ThenInclude(r => r.Seats)
            .FirstOrDefaultAsync(s => s.Id == id);

    public async Task<List<SeatSection>> GetBySeatMapIdAsync(Guid seatMapId)
        => await _ctx.Sections
            .Where(s => s.SeatMapId == seatMapId)
            .OrderBy(s => s.SortOrder)
            .ToListAsync();

    public async Task<SeatSection> AddAsync(SeatSection section)
    {
        await _ctx.Sections.AddAsync(section);
        return section;
    }

    public Task UpdateAsync(SeatSection section)
    {
        _ctx.Sections.Update(section);
        return Task.CompletedTask;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _ctx.Sections.FindAsync(id);
        if (entity == null) return false;
        entity.MarkDeleted();
        return true;
    }
}

public class SeatRowRepository : ISeatRowRepository
{
    private readonly SeatDbContext _ctx;

    public SeatRowRepository(SeatDbContext ctx) => _ctx = ctx;

    public async Task<SeatRow?> GetByIdAsync(Guid id)
        => await _ctx.Rows.FindAsync(id);

    public async Task<SeatRow?> GetByIdWithSeatsAsync(Guid id)
        => await _ctx.Rows
            .Include(r => r.Seats)
            .FirstOrDefaultAsync(r => r.Id == id);

    public async Task<List<SeatRow>> GetBySectionIdAsync(Guid sectionId)
        => await _ctx.Rows
            .Where(r => r.SectionId == sectionId)
            .OrderBy(r => r.RowNumber)
            .ToListAsync();

    public async Task<SeatRow> AddAsync(SeatRow row)
    {
        await _ctx.Rows.AddAsync(row);
        return row;
    }

    public Task UpdateAsync(SeatRow row)
    {
        _ctx.Rows.Update(row);
        return Task.CompletedTask;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _ctx.Rows.FindAsync(id);
        if (entity == null) return false;
        entity.MarkDeleted();
        return true;
    }
}

public class SeatRepository : ISeatRepository
{
    private readonly SeatDbContext _ctx;

    public SeatRepository(SeatDbContext ctx) => _ctx = ctx;

    public async Task<Seat?> GetByIdAsync(Guid id)
        => await _ctx.Seats.FindAsync(id);

    public async Task<List<Seat>> GetByRowIdAsync(Guid rowId)
        => await _ctx.Seats
            .Where(s => s.RowId == rowId)
            .OrderBy(s => s.SeatNumber)
            .ToListAsync();

    public async Task<List<Seat>> GetBySeatMapIdAsync(Guid seatMapId)
        => await _ctx.Seats
            .Include(s => s.Row)
                .ThenInclude(r => r.Section)
            .Where(s => s.Row.Section.SeatMapId == seatMapId)
            .ToListAsync();

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

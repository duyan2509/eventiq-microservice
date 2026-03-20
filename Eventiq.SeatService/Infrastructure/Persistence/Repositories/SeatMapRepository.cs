using Eventiq.SeatService.Domain.Entity;
using Eventiq.SeatService.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Eventiq.SeatService.Infrastructure.Persistence.Repositories;

public class SeatMapRepository : ISeatMapRepository
{
    private readonly SeatDbContext _ctx;

    public SeatMapRepository(SeatDbContext ctx)
    {
        _ctx = ctx;
    }

    public async Task<SeatMap?> GetByIdAsync(Guid id)
        => await _ctx.SeatMaps.FindAsync(id);

    public async Task<SeatMap?> GetByIdWithDetailsAsync(Guid id)
        => await _ctx.SeatMaps
            .Include(m => m.Sections)
                .ThenInclude(s => s.Rows)
                    .ThenInclude(r => r.Seats)
            .Include(m => m.Objects)
            .AsSplitQuery()
            .FirstOrDefaultAsync(m => m.Id == id);

    public async Task<SeatMap?> GetByChartIdAsync(Guid chartId)
        => await _ctx.SeatMaps.FirstOrDefaultAsync(m => m.ChartId == chartId);

    public async Task<List<SeatMap>> GetByEventIdAsync(Guid eventId)
        => await _ctx.SeatMaps
            .Where(m => m.EventId == eventId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

    public async Task<List<SeatMap>> GetByOrganizationIdAsync(Guid organizationId)
        => await _ctx.SeatMaps
            .Where(m => m.OrganizationId == organizationId)
            .ToListAsync();

    public async Task<SeatMap> AddAsync(SeatMap seatMap)
    {
        await _ctx.SeatMaps.AddAsync(seatMap);
        return seatMap;
    }

    public Task UpdateAsync(SeatMap seatMap)
    {
        _ctx.SeatMaps.Update(seatMap);
        return Task.CompletedTask;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _ctx.SeatMaps.FindAsync(id);
        if (entity == null) return false;
        entity.MarkDeleted();
        return true;
    }
}

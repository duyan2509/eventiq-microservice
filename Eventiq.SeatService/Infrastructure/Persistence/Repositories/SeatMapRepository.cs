using Eventiq.SeatService.Domain.Entity;
using Eventiq.SeatService.Domain.Enum;
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
            .Include(m => m.Seats.OrderBy(s => s.SeatNumber))
            .Include(m => m.Objects.OrderBy(o => o.ZIndex))
            .AsSplitQuery()
            .FirstOrDefaultAsync(m => m.Id == id);

    public async Task<SeatMap?> GetByIdWithObjectsAsync(Guid id)
        => await _ctx.SeatMaps
            .Include(m => m.Objects.OrderBy(o => o.ZIndex))
            .FirstOrDefaultAsync(m => m.Id == id);

    public async Task<SeatMap?> GetBySessionIdWithObjectsAsync(Guid sessionId)
        => await _ctx.SeatMaps
            .Include(m => m.Objects.OrderBy(o => o.ZIndex))
            .FirstOrDefaultAsync(m => m.SessionId == sessionId);

    public async Task<SeatMap?> GetByChartIdAsync(Guid chartId)
        => await _ctx.SeatMaps.FirstOrDefaultAsync(m => m.ChartId == chartId);

    public async Task<SeatMap?> GetBySessionIdAsync(Guid sessionId)
        => await _ctx.SeatMaps.FirstOrDefaultAsync(m => m.SessionId == sessionId);

    public async Task<SeatMap?> GetBySessionIdWithDetailsAsync(Guid sessionId)
        => await _ctx.SeatMaps
            .Include(m => m.Seats.OrderBy(s => s.SeatNumber))
            .Include(m => m.Objects.OrderBy(o => o.ZIndex))
            .AsSplitQuery()
            .FirstOrDefaultAsync(m => m.SessionId == sessionId);

    public async Task<SeatMap?> GetPublishedTemplateByChartIdAsync(Guid chartId)
        => await _ctx.SeatMaps
            .Include(m => m.Seats)
            .Include(m => m.Objects)
            .AsSplitQuery()
            .FirstOrDefaultAsync(m => m.ChartId == chartId
                && m.SessionId == null
                && m.Status == SeatMapStatus.Published);

    public async Task<SeatMap?> GetTemplateByChartIdWithDetailsAsync(Guid chartId)
        => await _ctx.SeatMaps
            .Include(m => m.Seats)
            .Include(m => m.Objects)
            .AsSplitQuery()
            .FirstOrDefaultAsync(m => m.ChartId == chartId && m.SessionId == null);

    public async Task<bool> HasTemplateForEventAsync(Guid eventId)
        => await _ctx.SeatMaps.AnyAsync(m => m.EventId == eventId && m.SessionId == null);

    public async Task<List<SeatMap>> GetByEventIdAsync(Guid eventId)
        => await _ctx.SeatMaps
            .Where(m => m.EventId == eventId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

    public async Task<List<SeatMap>> GetByOrganizationIdAsync(Guid organizationId)
        => await _ctx.SeatMaps
            .Where(m => m.OrganizationId == organizationId)
            .ToListAsync();

    public async Task<List<SeatMap>> GetAllTemplatesAsync()
        => await _ctx.SeatMaps
            .Where(m => m.SessionId == null)
            .OrderByDescending(m => m.CreatedAt)
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

    public async Task<int> IncrementAndGetNextSeatNumberAsync(Guid seatMapId)
    {
        var result = await _ctx.Database
            .SqlQuery<int>($"""
                UPDATE seat_service.seat_maps
                SET next_seat_number = next_seat_number + 1
                WHERE id = {seatMapId}
                RETURNING next_seat_number
                """)
            .ToListAsync();
        return result[0];
    }

    public async Task<int> IncrementAndGetNextSeatNumberByAsync(Guid seatMapId, int count)
    {
        var result = await _ctx.Database
            .SqlQuery<int>($"""
                UPDATE seat_service.seat_maps
                SET next_seat_number = next_seat_number + {count}
                WHERE id = {seatMapId}
                RETURNING next_seat_number
                """)
            .ToListAsync();
        return result[0];
    }
}

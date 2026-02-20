using System.Data;
using Dapper;
using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Domain.Repositories;
using Eventiq.EventService.Dtos;
using Eventiq.EventService.Infrastructure.Persistence.ReadModel;

namespace Eventiq.EventService.Infrastructure.Persistence.DapperRepositories;

public class ChartRepository : BaseRepository, IChartRepository
{
    public ChartRepository(IDbConnection connection) : base(connection)
    {
    }

    public async Task<int> DeleteAsync(Guid eventId, Guid orgId, Guid chartId)
    {
        var sql = $@"
        DELETE FROM charts c
        using events e
        where c.event_id = e.id
        and e.id = @EventId
        and e.status = @DraftStatus
        and e.organization_id = @OrgId
        and c.id = @ChartId
        ";
        return await _connection.ExecuteAsync(
            sql,
            new
            {
                ChartId = chartId,
                EventId = eventId,
                OrgId = orgId,
                DraftStatus = EventStatus.Draft
            },
            transaction: _transaction
        );
    }

    public async Task<PaginatedResult<ChartModel>> GetAllChartsByEventIdAsync(Guid eventId, int page, int size)
    {
        var sql = @"
    SELECT COUNT(*) 
    FROM charts
    WHERE event_id = @EventId;

    SELECT name, event_id, id
    FROM charts
    WHERE event_id = @EventId
    ORDER BY created_at DESC
    OFFSET @Offset LIMIT @PageSize;
";


        using var multi = await _connection.QueryMultipleAsync(
            sql,
            new
            {
                EventId = eventId,
                Offset = (page - 1) * size,
                PageSize = size
            },
            transaction: _transaction);

        var total = await multi.ReadSingleAsync<int>();
        var data = (await multi.ReadAsync<ChartModel>()).ToList();

        return new PaginatedResult<ChartModel>
        {
            Page = page,
            Size = size,
            Total = total,
            Data = data
        };
    }

    public async Task<int> AddAsync(Guid EventId, Chart chart)
    {
        var sql = @"
        INSERT INTO charts
        (
            id,
            event_id,
            created_at,
            updated_at,
            deleted_at,
            is_deleted,
            name
        )
        VALUES
        (
            @Id,
            @EventId,
            @CreatedAt,
            @UpdatedAt,
            @DeletedAt,
            @IsDeleted,
            @Name
        );
    ";
        return await _connection.ExecuteAsync(
            sql,
            new
            {
                Id = chart.Id,
                EventId = EventId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                DeletedAt = chart.DeletedAt,
                IsDeleted = false,
                Name = chart.Name,
            },
            transaction: _transaction);
    }

    public async Task<ChartModel?> UpdatePartialAsync(Guid chartId, Guid eventId, UpdateChartDto dto)
    {
        var sql = @"
    UPDATE charts
    SET name = COALESCE(@Name, name),
        updated_at = NOW()
    WHERE id = @ChartId
      AND event_id = @EventId
    RETURNING id, name, event_id;
";
        return await _connection.QueryFirstOrDefaultAsync<ChartModel>(
            sql,
            new
            {
                ChartId = chartId,
                EventId = eventId,
                dto.Name,
            },
            transaction: _transaction
        );
    }
}

using System.Data;
using Dapper;
using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Domain.Repositories;
using Eventiq.EventService.Dtos;
using Eventiq.EventService.Infrastructure.Persistence.ReadModel;

namespace Eventiq.EventService.Infrastructure.Persistence.DapperRepositories;

    public class LegendRepository : BaseRepository, ILegendRepository
    {
        public LegendRepository(IDbConnection connection) : base(connection)
        {
        }

        public async Task<PaginatedResult<LegendModel>> GetAllLegendsByEventIdAsync(Guid eventId, int page = 1, int size = 10)
        {
            var sql = @"
    SELECT COUNT(*) 
    FROM legends
    WHERE event_id = @EventId;

    SELECT *
    FROM legends
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
        var data = (await multi.ReadAsync<LegendModel>()).ToList();

        return new PaginatedResult<LegendModel>
        {
            Page = page,
            Size =size,
            Total = total,
            Data = data
        };

    }
        public async Task<int> AddAsync(Legend legend)
        {
            var sql = @"
        INSERT INTO legends
        (
            id,
            event_id,
            created_at,
            updated_at,
            deleted_at,
            is_deleted,
            price,
            name,
            color
        )
        VALUES
        (
            @Id,
            @EventId,
            @CreatedAt,
            @UpdatedAt,
            @DeletedAt,
            @IsDeleted,
            @Price,
            @Name,
            @Color
        );
    ";
            return await _connection.ExecuteAsync(
                sql,
                new
                {
                    Id = legend.Id,
                    EventId = legend.EventId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    DeletedAt = legend.DeletedAt,
                    IsDeleted = false,
                    Price = legend.Price,
                    Name = legend.Name,
                    Color = legend.Color,
                },
                transaction: _transaction);
        }

        public async Task<LegendModel> GetLegendByIdEventIdAsync(Guid legendId, Guid eventId)
        {
            var sql = @"
    SELECT id, name, color, price, event_id
    FROM legends
    WHERE id = @LegendId
      AND event_id = @EventId
    LIMIT 1;
";

            return await _connection.QueryFirstOrDefaultAsync<LegendModel>(
                sql,
                new
                {
                    LegendId = legendId,
                    EventId = eventId
                },
                transaction: _transaction);
        }

        public async Task<LegendModel?> UpdatePartialAsync(Guid legendId, Guid eventId, UpdateLegendDto dto)
        {
            var sql = @"
    UPDATE legends
    SET name = COALESCE(@Name, name),
        color = COALESCE(@Color, color),
        updated_at = NOW()
    WHERE id = @LegendId
      AND event_id = @EventId
    RETURNING id, name, color, price, event_id;
";
            return await _connection.QueryFirstOrDefaultAsync<LegendModel>(
                sql,
                new
                {
                    LegendId = legendId,
                    EventId = eventId,
                    dto.Name,
                    dto.Color
                },
                transaction: _transaction
            );
    }

    public async Task<int> DeleteAsync(Guid eventId, Guid orgId, Guid legendId)
    {
        var sql = @"
    DELETE FROM legends l
    USING events e
    WHERE l.id = @LegendId
      AND l.event_id = e.id
      AND e.id = @EventId
      AND e.organization_id = @OrgId 
      AND e.status = @Status;
";

        return await _connection.ExecuteAsync(
            sql,
            new
            {
                LegendId = legendId,
                EventId = eventId,
                OrgId = orgId,
                Status = EventStatus.Draft
            },
            transaction: _transaction
        );

    }
}

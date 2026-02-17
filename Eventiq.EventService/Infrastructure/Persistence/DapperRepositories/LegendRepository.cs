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
        FROM Legends
        WHERE EventId = @EventId;

        SELECT *
        FROM Legends
        WHERE EventId = @EventId
        ORDER BY CreatedDate DESC
        OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
    ";

        using var multi = await _connection.QueryMultipleAsync(sql, new
        {
            EventId = eventId,
            Offset = (page - 1) * size,
            PageSize = size
        });

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
        var sql = $@"insert  into Legends 
        (
         EventId=@EventId, 
         CreatedAt=@CreatedAt,
         UpdatedAt=@UpdatedAt,
         DeletedAt=@DeletedAt,
         IsDeleted=@IsDeleted,
         Id=@Id,
         Price=@Price,
         Name=@Name,
         Color=@Color)";
        return await _connection.ExecuteAsync(sql, new 
        {
            EventId = legend.EventId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            DeletedAt = legend.DeletedAt,
            IsDeleted = false,
            Price = legend.Price,
            Name = legend.Name,
            Color = legend.Color,
            Id = legend.Id,
        });
    }

    public Task<LegendModel> GetLegendByIdEventIdAsync(Guid legendId, Guid eventId)
    {
        throw new NotImplementedException();
    }

    public async Task<LegendModel?> UpdatePartialAsync(Guid legendId, Guid eventId, UpdateLegendDto dto)
    {
        var sql = $@"
        UPDATE Legends
        SET Name  = COALESCE(@Name, Name),
            Color = COALESCE(@Color, Color),
            UpdatedAt = NOW()
        WHERE Id = @LegendId
          AND EventId = @EventId
        {"R"}ETURNING Id, Name, Color, Price, EventId;
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
        var sql = $@"
        DELETE FROM Legends l
        USING Events e
        WHERE l.Id = @LegendId
          AND l.EventId = e.Id
          AND e.Id = @EventId
          AND e.OrganizationId = @OrgId
          AND e.Status = @Status
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

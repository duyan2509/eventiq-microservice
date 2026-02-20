using System.Data;
using Dapper;
using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Domain.Repositories;
using Eventiq.EventService.Dtos;
using Eventiq.EventService.Infrastructure.Persistence.ReadModel;

namespace Eventiq.EventService.Infrastructure.Persistence.DapperRepositories;

public class SessionRepository : BaseRepository, ISessionRepository
{
    public SessionRepository(IDbConnection connection) : base(connection)
    {
    }

    public async Task<PaginatedResult<SessionModel>> GetAllSessionsByEventIdAsync(Guid eventId, int page, int size)
    {
        var sql = @"
    SELECT COUNT(*) 
    FROM sessions
    WHERE event_id = @EventId;

    SELECT s.id, s.name, s.start_time, s.end_time, s.event_id, s.chart_id, c.name as chart_name
    FROM sessions s
    join charts c
    on  c.id = s.chart_id
    WHERE s.event_id = @EventId
    ORDER BY s.created_at DESC
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
        var data = (await multi.ReadAsync<SessionModel>()).ToList();

        return new PaginatedResult<SessionModel>
        {
            Page = page,
            Size = size,
            Total = total,
            Data = data
        };
    }

    public async Task<int> AddAsync(Guid eventId, Session session)
    {
        var sql = @"
        INSERT INTO sessions
        (
            id,
            event_id,
            created_at,
            updated_at,
            deleted_at,
            is_deleted,
            start_time,
            name,
            end_time,
            chart_id
        )
        VALUES
        (
            @Id,
            @EventId,
            @CreatedAt,
            @UpdatedAt,
            @DeletedAt,
            @IsDeleted,
            @StartTime,
            @Name,
            @EndTime,
            @ChartId
        );
    ";
        return await _connection.ExecuteAsync(
            sql,
            new
            {
                Id = session.Id,
                EventId = eventId,
                CreatedAt = session.CreatedAt,
                UpdatedAt = session.UpdatedAt,
                DeletedAt = session.DeletedAt,
                IsDeleted = false,
                StartTime = session.StartTime,
                Name = session.Name,
                EndTime = session.EndTime,
                ChartId = session.ChartId,
            },
            transaction: _transaction);
    }

    public async Task<int> UpdateAsync(Session session)
    {
        var sql = @"
        UPDATE sessions
        SET name = @Name,
            start_time = COALESCE(@StartTime, start_time),
            end_time = COALESCE(@EndTime, end_time),
            chart_id = COALESCE(@ChartId, chart_id)
        WHERE id = @Id
        ";

        return await _connection.ExecuteAsync(sql, session, _transaction);
    }

    public async Task<int> DeleteAsync(Guid eventId, Guid orgId, Guid sessionId)
    {
        var sql = @"
    DELETE FROM sessions s
    USING events e
    WHERE s.id = @SessionId
      AND s.event_id = e.id
      AND e.id = @EventId
      AND e.organization_id = @OrgId 
      AND e.status = @Status;
";

        return await _connection.ExecuteAsync(
            sql,
            new
            {
                SessionId = sessionId,
                EventId = eventId,
                OrgId = orgId,
                Status = EventStatus.Draft
            },
            transaction: _transaction
        );
    }

    public async Task<Session?> GetByIdAsync(Guid sessionId)
    {
        var sql = $@"
        select id, name, event_id, start_time, end_time, chart_id
        from sessions
        where id = @SessionId
        limit 1
";
        return await _connection.QueryFirstOrDefaultAsync<Session>(sql, new
            {
                SessionId = sessionId,
            },
            transaction: _transaction);
    }

    public async Task<bool> CheckOverlappedAsync(Guid eventId, Guid sessionId, DateTime sessionStartTime, DateTime sessionEndTime)
    {
        var sql = $@"
        select 1
        from sessions s 
        where event_id = @EventId
        and id <> @SessionId
        and (
            @StartTime < end_time
            and @EndTime > start_time
        )
        limit 1";
        var rs = await  _connection.QueryFirstOrDefaultAsync<int?>(sql, new
        {
            EventId = eventId,
            SessionId = sessionId,
            StartTime = sessionStartTime,
            EndTime = sessionEndTime,
        }, _transaction);
        return rs.HasValue;
    }
}

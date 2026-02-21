using System.Data;
using Dapper;
using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Domain.Repositories;
using Eventiq.EventService.Dtos;
using Eventiq.EventService.Infrastructure.Persistence.ReadModel;

namespace Eventiq.EventService.Infrastructure.Persistence.DapperRepositories;

public class SubmissionRepository : BaseRepository, ISubmissionRepository
{
    public SubmissionRepository(IDbConnection connection) : base(connection)
    {
    }
    public Guid Id { get; set; }
    public string AdminEmail { get; set; }
    public Guid AdminId { get; set; }
    public string Message { get; set; }
    public EventStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public async Task<PaginatedResult<SubmissionModel>> GetAllSubmissionsByEventIdAsync(Guid eventId)
    {
        var sql = @"
        SELECT COUNT(*) 
        FROM submissions
        WHERE event_id = @EventId;

        SELECT s.id, s.admin_email, s.admin_id, s.message, s.create_at, s,status
        FROM submissions s
        WHERE s.event_id = @EventId
        ORDER BY s.created_at DESC
    ";


        using var multi = await _connection.QueryMultipleAsync(
            sql,
            new
            {
                EventId = eventId,
            },
            transaction: _transaction);

        var total = await multi.ReadSingleAsync<int>();
        var data = (await multi.ReadAsync<SubmissionModel>()).ToList();

        return new PaginatedResult<SubmissionModel>
        {
            Page = 1,
            Size = total,
            Total = total,
            Data = data
        };
    }

    public async Task AddAsync(Guid eventId, Submission submission)
    {
        const string sql = @"
        INSERT INTO submissions
        (
            id,
            event_id,
            admin_email,
            admin_id,
            message,
            status,
            created_at,
            is_deleted
        )
        VALUES
        (
            @Id,
            @EventId,
            @AdminEmail,
            @AdminId,
            @Message,
            @Status,
            @CreatedAt,
            @IsDeleted
        );
    ";


        submission.Id = Guid.NewGuid();

        await _connection.ExecuteAsync(
            sql,
            submission,
            _transaction
        );    
    }
}

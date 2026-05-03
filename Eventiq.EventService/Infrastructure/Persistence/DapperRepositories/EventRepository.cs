using System.Data;
using System.Text;
using Dapper;
using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Domain.Repositories;
using Eventiq.EventService.Dtos;
using Eventiq.EventService.Infrastructure.Persistence.ReadModel;

namespace Eventiq.EventService.Infrastructure.Persistence.DapperRepositories;

public class EventRepository : BaseRepository, IEventRepository
{
    public EventRepository(IDbConnection connection) : base(connection)
    {
    }

    public async Task<EventModel?> GetByIdAsync(Guid eventId)
    {
        const string sql = @"
        SELECT 
            id,
            organization_id AS OrganizationId,
            status,
            event_banner AS EventBanner,
            name,
            description,
            detail_address AS DetailAddress,
            province_code AS ProvinceCode,
            commune_code AS CommuneCode,
            province_name AS ProvinceName,
            commune_name AS CommuneName,
            start_time AS StartTime,
            end_time AS EndTime
        FROM events
        WHERE id = @EventId
        LIMIT 1;
        ";

        return await _connection.QueryFirstOrDefaultAsync<EventModel>(
            sql,
            new { EventId = eventId },
            transaction: _transaction);
    }

    public async Task SetEventStatusAsync(Guid eventId, EventStatus status)
    {
        const string sql = @"
        UPDATE events
        SET status = @Status
        WHERE id = @EventId;
        ";

        await _connection.ExecuteAsync(
            sql,
            new
            {
                EventId = eventId,
                Status = status
            },
            transaction: _transaction);
    }

    public async Task<PaginatedResult<EventModel>> GetAllEventsAsync(
        string? query,
        EventStatus? status,
        string? province,
        Guid? organizationId,
        bool newest,
        bool increasePrice,
        int page,
        int size)
    {
        if (page <= 0) page = 1;
        if (size <= 0) size = 10;

        var whereBuilder = new StringBuilder("WHERE 1=1");

        if (!string.IsNullOrWhiteSpace(query))
        {
            whereBuilder.Append(" AND (name ILIKE @Query OR description ILIKE @Query)");
        }

        if (status.HasValue)
        {
            whereBuilder.Append(" AND status = @Status");
        }

        if (!string.IsNullOrWhiteSpace(province))
        {
            whereBuilder.Append(" AND (province_code = @Province OR province_name = @Province)");
        }

        if (organizationId.HasValue)
        {
            whereBuilder.Append(" AND organization_id = @OrganizationId");
        }

        var orderClause = new StringBuilder();

        if (increasePrice)
        {
            if (newest)
            {
                orderClause.Append("ORDER BY e.start_time DESC, COALESCE(MIN(l.price), 0) ASC");
            }
            else
            {
                orderClause.Append("ORDER BY e.start_time ASC, COALESCE(MIN(l.price), 0) ASC");
            }
        }
        else
        {
            orderClause.Append(newest
                ? "ORDER BY e.start_time DESC"
                : "ORDER BY e.start_time ASC");
        }

        var sql = $@"
        SELECT COUNT(*) 
        FROM events e
        {whereBuilder};

        SELECT 
            e.id,
            e.organization_id AS OrganizationId,
            e.status,
            e.event_banner AS EventBanner,
            e.name,
            e.description,
            e.detail_address AS DetailAddress,
            e.province_code AS ProvinceCode,
            e.commune_code AS CommuneCode,
            e.province_name AS ProvinceName,
            e.commune_name AS CommuneName,
            e.start_time AS StartTime,
            e.end_time AS EndTime,
            MIN(l.price) AS LowestPrice
        FROM events e
        LEFT JOIN legends l ON l.event_id = e.id
        {whereBuilder}
        GROUP BY 
            e.id,
            e.organization_id,
            e.status,
            e.event_banner,
            e.name,
            e.description,
            e.detail_address,
            e.province_code,
            e.commune_code,
            e.province_name,
            e.commune_name,
            e.start_time,
            e.end_time
        {orderClause}
        OFFSET @Offset LIMIT @PageSize;
        ";

        using var multi = await _connection.QueryMultipleAsync(
            sql,
            new
            {
                Query = !string.IsNullOrWhiteSpace(query) ? $"%{query.Trim()}%" : null,
                Status = status,
                Province = string.IsNullOrWhiteSpace(province) ? null : province.Trim(),
                OrganizationId = organizationId,
                Offset = (page - 1) * size,
                PageSize = size
            },
            transaction: _transaction);

        var total = await multi.ReadSingleAsync<int>();
        var data = (await multi.ReadAsync<EventModel>()).ToList();

        return new PaginatedResult<EventModel>
        {
            Page = page,
            Size = size,
            Total = total,
            Data = data
        };
    }

    public async Task<int> AddAsync(Event ev)
    {
        const string sql = @"
        INSERT INTO events
        (
            id,
            organization_id,
            oranization_avatar,
            event_banner,
            name,
            description,
            detail_address,
            province_code,
            commune_code,
            province_name,
            commune_name,
            status,
            start_time,
            end_time,
            created_at,
            updated_at,
            deleted_at,
            is_deleted,
            organization_name
        )
        VALUES
        (
            @Id,
            @OrganizationId,
            @OranizationAvatar,
            @EventBanner,
            @Name,
            @Description,
            @DetailAddress,
            @ProvinceCode,
            @CommuneCode,
            @ProvinceName,
            @CommuneName,
            @Status,
            @StartTime,
            @EndTime,
            @CreatedAt,
            @UpdatedAt,
            @DeletedAt,
            @IsDeleted,
            @OrganizationName
        );
        ";

        return await _connection.ExecuteAsync(
            sql,
            new
            {
                ev.Id,
                ev.OrganizationId,
                ev.OranizationAvatar,
                ev.EventBanner,
                ev.Name,
                ev.Description,
                ev.DetailAddress,
                ev.ProvinceCode,
                ev.CommuneCode,
                ev.ProvinceName,
                ev.CommuneName,
                ev.Status,
                ev.StartTime,
                ev.EndTime,
                ev.CreatedAt,
                ev.UpdatedAt,
                ev.DeletedAt,
                ev.IsDeleted,
                ev.OrganizationName
            },
            transaction: _transaction);
    }

    public async Task<EventModel?> UpdatePartialAsync(Guid eventId, UpdateEventDto dto)
    {
        const string sql = @"
        UPDATE events
        SET 
            name = COALESCE(@Name, name),
            description = COALESCE(@Description, description),
            detail_address = COALESCE(@DetailAddress, detail_address),
            province_code = COALESCE(@ProvinceCode, province_code),
            commune_code = COALESCE(@CommuneCode, commune_code),
            province_name = COALESCE(@ProvinceName, province_name),
            commune_name = COALESCE(@CommuneName, commune_name),
            start_time = COALESCE(@StartTime, start_time),
            end_time = COALESCE(@EndTime, end_time),
            event_banner = COALESCE(@EventBanner, event_banner),
            updated_at = NOW()
        WHERE id = @EventId
        RETURNING 
            id,
            organization_id AS OrganizationId,
            status,
            event_banner AS EventBanner,
            name,
            description,
            detail_address AS DetailAddress,
            province_code AS ProvinceCode,
            commune_code AS CommuneCode,
            province_name AS ProvinceName,
            commune_name AS CommuneName,
            start_time AS StartTime,
            end_time AS EndTime;
        ";

        return await _connection.QueryFirstOrDefaultAsync<EventModel>(
            sql,
            new
            {
                EventId = eventId,
                dto.EventBanner,
                dto.Name,
                dto.Description,
                dto.DetailAddress,
                dto.ProvinceCode,
                dto.CommuneCode,
                dto.ProvinceName,
                dto.CommuneName,
                dto.StartTime,
                dto.EndTime
            },
            transaction: _transaction);
    }

    public async Task<IEnumerable<EventModel>> GetEventsByOrgAndStatusAsync(Guid organizationId, EventStatus status)
    {
        const string sql = @"
        SELECT 
            id,
            organization_id AS OrganizationId,
            status,
            event_banner AS EventBanner,
            name,
            start_time AS StartTime,
            end_time AS EndTime
        FROM events
        WHERE organization_id = @OrganizationId
          AND status = @Status;
        ";

        return await _connection.QueryAsync<EventModel>(
            sql,
            new { OrganizationId = organizationId, Status = status },
            transaction: _transaction);
    }
}

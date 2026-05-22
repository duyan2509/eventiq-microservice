using System.Text.Json;
using AutoMapper;
using Eventiq.SeatService.Application.Dtos;
using Eventiq.SeatService.Application.Guards;
using Eventiq.SeatService.Application.Service.Interface;
using Eventiq.SeatService.Domain.Entity;
using Eventiq.SeatService.Domain.Enum;

namespace Eventiq.SeatService.Application.Service.Implement;

public class SeatDesignService : ISeatDesignService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ILogger<SeatDesignService> _logger;

    public SeatDesignService(IUnitOfWork uow, IMapper mapper, ILogger<SeatDesignService> logger)
    {
        _uow = uow;
        _mapper = mapper;
        _logger = logger;
    }

    // ========== Seats ==========

    public async Task<SeatResponse> AddSeatAsync(Guid seatMapId, Guid orgId, AddSeatDto dto)
    {
        var seatMap = await GetAndValidateSeatMap(seatMapId, orgId);

        var seat = new Seat
        {
            Id = Guid.NewGuid(),
            SeatMapId = seatMapId,
            Label = dto.Label,
            SeatNumber = dto.SeatNumber,
            SeatType = dto.SeatType,
            Status = SeatStatus.Available,
            Position = dto.Position,
            LegendId = dto.LegendId,
        };

        await _uow.Seats.AddRangeAsync(new[] { seat });
        seatMap.IncrementVersion();
        await _uow.SeatMaps.UpdateAsync(seatMap);
        await _uow.SaveChangesAsync();

        return _mapper.Map<SeatResponse>(seat);
    }

    public async Task<List<SeatResponse>> BatchUpdateSeatsAsync(Guid seatMapId, Guid orgId, BatchUpdateSeatsDto dto)
    {
        var seatMap = await GetAndValidateSeatMap(seatMapId, orgId);

        // Load all target seats in a single query — no N+1
        var seatIds = dto.Seats.Select(s => s.SeatId).ToList();
        var seats = await _uow.Seats.GetByIdsAsync(seatIds);
        var seatById = seats.ToDictionary(s => s.Id);

        foreach (var seatDto in dto.Seats)
        {
            if (!seatById.TryGetValue(seatDto.SeatId, out var seat)) continue;

            if (seatDto.Label != null) seat.Label = seatDto.Label;
            if (seatDto.SeatNumber.HasValue) seat.SeatNumber = seatDto.SeatNumber.Value;
            if (seatDto.Status.HasValue) seat.Status = seatDto.Status.Value;
            if (seatDto.SeatType.HasValue) seat.SeatType = seatDto.SeatType.Value;
            if (seatDto.Position != null) seat.Position = seatDto.Position;
            if (seatDto.LegendId.HasValue) seat.LegendId = seatDto.LegendId;
            if (seatDto.CustomProperties != null) seat.CustomProperties = seatDto.CustomProperties;
            seat.MarkUpdated();
        }

        await _uow.Seats.UpdateRangeAsync(seats);
        seatMap.IncrementVersion();
        await _uow.SeatMaps.UpdateAsync(seatMap);
        await _uow.SaveChangesAsync();

        return _mapper.Map<List<SeatResponse>>(seats);
    }

    public async Task<List<SeatResponse>> SetSeatLegendAsync(Guid seatMapId, Guid orgId, List<Guid> seatIds, Guid? legendId)
    {
        var seatMap = await GetAndValidateSeatMap(seatMapId, orgId);

        var seats = await _uow.Seats.GetByIdsAsync(seatIds);
        foreach (var seat in seats)
        {
            seat.LegendId = legendId;
            seat.MarkUpdated();
        }

        await _uow.Seats.UpdateRangeAsync(seats);
        seatMap.IncrementVersion();
        await _uow.SeatMaps.UpdateAsync(seatMap);
        await _uow.SaveChangesAsync();

        return _mapper.Map<List<SeatResponse>>(seats);
    }

    public async Task DeleteSeatsAsync(Guid seatMapId, Guid orgId, List<Guid> seatIds)
    {
        _logger.LogInformation("DeleteSeats called: seatMapId={SeatMapId}, orgId={OrgId}, ids=[{Ids}]",
            seatMapId, orgId, string.Join(",", seatIds));

        var seatMap = await GetAndValidateSeatMap(seatMapId, orgId);

        await _uow.Seats.DeleteRangeAsync(seatIds);
        seatMap.IncrementVersion();
        await _uow.SeatMaps.UpdateAsync(seatMap);
        var saved = await _uow.SaveChangesAsync();

        _logger.LogInformation("DeleteSeats saved {Count} rows", saved);
    }

    // ========== Objects ==========

    public async Task<SeatObjectResponse> AddObjectAsync(Guid seatMapId, Guid orgId, AddObjectDto dto)
    {
        var seatMap = await GetAndValidateSeatMap(seatMapId, orgId);

        var obj = _mapper.Map<SeatObject>(dto);
        obj.Id = Guid.NewGuid();
        obj.SeatMapId = seatMapId;

        await _uow.Objects.AddAsync(obj);
        seatMap.IncrementVersion();
        await _uow.SeatMaps.UpdateAsync(seatMap);
        await _uow.SaveChangesAsync();

        return _mapper.Map<SeatObjectResponse>(obj);
    }

    public async Task<SeatObjectResponse> UpdateObjectAsync(Guid seatMapId, Guid orgId, UpdateObjectDto dto)
    {
        var seatMap = await GetAndValidateSeatMap(seatMapId, orgId);

        var obj = await _uow.Objects.GetByIdAsync(dto.ObjectId);
        SeatMapGuards.EnsureObjectExists(obj);

        if (dto.ObjectType.HasValue) obj!.ObjectType = dto.ObjectType.Value;
        if (dto.Label != null) obj!.Label = dto.Label;
        if (dto.Geometry != null) obj!.Geometry = dto.Geometry;
        if (dto.Style != null) obj!.Style = dto.Style;
        if (dto.ZIndex.HasValue) obj!.ZIndex = dto.ZIndex.Value;
        obj!.MarkUpdated();

        await _uow.Objects.UpdateAsync(obj);
        seatMap.IncrementVersion();
        await _uow.SeatMaps.UpdateAsync(seatMap);
        await _uow.SaveChangesAsync();

        return _mapper.Map<SeatObjectResponse>(obj);
    }

    public async Task DeleteObjectAsync(Guid seatMapId, Guid orgId, Guid objectId)
    {
        var seatMap = await GetAndValidateSeatMap(seatMapId, orgId);

        var deleted = await _uow.Objects.DeleteAsync(objectId);
        if (!deleted) throw new NotFoundException("Object not found.");

        seatMap.IncrementVersion();
        await _uow.SeatMaps.UpdateAsync(seatMap);
        await _uow.SaveChangesAsync();
    }

    // ========== Auto-save Snapshot ==========

    public async Task<SeatMapVersionResponse> AutoSaveSnapshotAsync(Guid seatMapId, Guid userId, string? description = null)
    {
        var seatMap = await _uow.SeatMaps.GetByIdWithDetailsAsync(seatMapId);
        SeatMapGuards.EnsureExists(seatMap);

        var latestVersion = await _uow.Versions.GetLatestAsync(seatMapId);
        var nextVersionNumber = (latestVersion?.VersionNumber ?? 0) + 1;

        var detailResponse = _mapper.Map<SeatMapDetailResponse>(seatMap!);
        var snapshot = JsonSerializer.Serialize(detailResponse);

        var version = new SeatMapVersion
        {
            Id = Guid.NewGuid(),
            SeatMapId = seatMapId,
            VersionNumber = nextVersionNumber,
            Snapshot = snapshot,
            CreatedBy = userId,
            ChangeDescription = description ?? $"Auto-save v{nextVersionNumber}"
        };

        await _uow.Versions.AddAsync(version);
        await _uow.SaveChangesAsync();

        return _mapper.Map<SeatMapVersionResponse>(version);
    }

    public async Task<Guid> GetSeatMapOrgIdAsync(Guid seatMapId)
    {
        var seatMap = await _uow.SeatMaps.GetByIdAsync(seatMapId);
        SeatMapGuards.EnsureExists(seatMap);
        return seatMap!.OrganizationId;
    }

    private async Task<SeatMap> GetAndValidateSeatMap(Guid seatMapId, Guid orgId)
    {
        var seatMap = await _uow.SeatMaps.GetByIdAsync(seatMapId);
        SeatMapGuards.EnsureExists(seatMap);
        SeatMapGuards.EnsureOwner(seatMap!, orgId);
        SeatMapGuards.EnsureDraft(seatMap!);
        return seatMap!;
    }
}

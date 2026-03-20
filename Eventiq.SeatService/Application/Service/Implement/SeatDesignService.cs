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

    public SeatDesignService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    // ========== Sections ==========

    public async Task<SeatSectionResponse> AddSectionAsync(Guid seatMapId, Guid orgId, AddSectionDto dto)
    {
        var seatMap = await GetAndValidateSeatMap(seatMapId, orgId);

        var section = _mapper.Map<SeatSection>(dto);
        section.Id = Guid.NewGuid();
        section.SeatMapId = seatMapId;

        await _uow.Sections.AddAsync(section);
        seatMap.IncrementVersion();
        await _uow.SeatMaps.UpdateAsync(seatMap);
        await _uow.SaveChangesAsync();

        return _mapper.Map<SeatSectionResponse>(section);
    }

    public async Task<SeatSectionResponse> UpdateSectionAsync(Guid seatMapId, Guid orgId, UpdateSectionDto dto)
    {
        var seatMap = await GetAndValidateSeatMap(seatMapId, orgId);

        var section = await _uow.Sections.GetByIdAsync(dto.SectionId);
        SeatMapGuards.EnsureSectionExists(section);

        if (dto.Label != null) section!.Label = dto.Label;
        if (dto.SectionType.HasValue) section!.SectionType = dto.SectionType.Value;
        if (dto.Geometry != null) section!.Geometry = dto.Geometry;
        if (dto.Style != null) section!.Style = dto.Style;
        if (dto.LegendId.HasValue) section!.LegendId = dto.LegendId;
        if (dto.SortOrder.HasValue) section!.SortOrder = dto.SortOrder.Value;
        section!.MarkUpdated();

        await _uow.Sections.UpdateAsync(section);
        seatMap.IncrementVersion();
        await _uow.SeatMaps.UpdateAsync(seatMap);
        await _uow.SaveChangesAsync();

        return _mapper.Map<SeatSectionResponse>(section);
    }

    public async Task DeleteSectionAsync(Guid seatMapId, Guid orgId, Guid sectionId)
    {
        var seatMap = await GetAndValidateSeatMap(seatMapId, orgId);

        var deleted = await _uow.Sections.DeleteAsync(sectionId);
        if (!deleted) throw new NotFoundException("Section not found.");

        seatMap.IncrementVersion();
        await _uow.SeatMaps.UpdateAsync(seatMap);
        await _uow.SaveChangesAsync();
    }

    // ========== Rows ==========

    public async Task<SeatRowResponse> AddRowAsync(Guid seatMapId, Guid orgId, AddRowDto dto)
    {
        var seatMap = await GetAndValidateSeatMap(seatMapId, orgId);

        var section = await _uow.Sections.GetByIdAsync(dto.SectionId);
        SeatMapGuards.EnsureSectionExists(section);

        var row = new SeatRow
        {
            Id = Guid.NewGuid(),
            SectionId = dto.SectionId,
            Label = dto.Label,
            RowNumber = dto.RowNumber,
            Curve = dto.Curve,
            SeatSpacing = dto.SeatSpacing
        };

        await _uow.Rows.AddAsync(row);

        // Auto-generate seats
        if (dto.SeatCount > 0)
        {
            var prefix = dto.LabelPrefix ?? dto.Label;
            var seats = new List<Seat>();
            for (int i = 1; i <= dto.SeatCount; i++)
            {
                seats.Add(new Seat
                {
                    Id = Guid.NewGuid(),
                    RowId = row.Id,
                    Label = $"{prefix}{i}",
                    SeatNumber = i,
                    Status = SeatStatus.Available,
                    SeatType = SeatType.Regular,
                    Position = JsonSerializer.Serialize(new { x = (i - 1) * dto.SeatSpacing, y = 0 })
                });
            }
            await _uow.Seats.AddRangeAsync(seats);
            row.Seats = seats;
        }

        seatMap.IncrementVersion();
        await _uow.SeatMaps.UpdateAsync(seatMap);
        await _uow.SaveChangesAsync();

        return _mapper.Map<SeatRowResponse>(row);
    }

    public async Task<SeatRowResponse> UpdateRowAsync(Guid seatMapId, Guid orgId, UpdateRowDto dto)
    {
        var seatMap = await GetAndValidateSeatMap(seatMapId, orgId);

        var row = await _uow.Rows.GetByIdAsync(dto.RowId);
        SeatMapGuards.EnsureRowExists(row);

        if (dto.Label != null) row!.Label = dto.Label;
        if (dto.RowNumber.HasValue) row!.RowNumber = dto.RowNumber.Value;
        if (dto.Curve != null) row!.Curve = dto.Curve;
        if (dto.SeatSpacing.HasValue) row!.SeatSpacing = dto.SeatSpacing.Value;
        row!.MarkUpdated();

        await _uow.Rows.UpdateAsync(row);
        seatMap.IncrementVersion();
        await _uow.SeatMaps.UpdateAsync(seatMap);
        await _uow.SaveChangesAsync();

        return _mapper.Map<SeatRowResponse>(row);
    }

    public async Task DeleteRowAsync(Guid seatMapId, Guid orgId, Guid rowId)
    {
        var seatMap = await GetAndValidateSeatMap(seatMapId, orgId);

        var deleted = await _uow.Rows.DeleteAsync(rowId);
        if (!deleted) throw new NotFoundException("Row not found.");

        seatMap.IncrementVersion();
        await _uow.SeatMaps.UpdateAsync(seatMap);
        await _uow.SaveChangesAsync();
    }

    // ========== Seats ==========

    public async Task<SeatResponse> AddSeatAsync(Guid seatMapId, Guid orgId, AddSeatDto dto)
    {
        var seatMap = await GetAndValidateSeatMap(seatMapId, orgId);

        var row = await _uow.Rows.GetByIdAsync(dto.RowId);
        SeatMapGuards.EnsureRowExists(row);

        var seat = _mapper.Map<Seat>(dto);
        seat.Id = Guid.NewGuid();
        seat.Status = SeatStatus.Available;

        await _uow.Seats.AddRangeAsync(new[] { seat });
        seatMap.IncrementVersion();
        await _uow.SeatMaps.UpdateAsync(seatMap);
        await _uow.SaveChangesAsync();

        return _mapper.Map<SeatResponse>(seat);
    }

    public async Task<List<SeatResponse>> BatchUpdateSeatsAsync(Guid seatMapId, Guid orgId, BatchUpdateSeatsDto dto)
    {
        var seatMap = await GetAndValidateSeatMap(seatMapId, orgId);

        var updatedSeats = new List<Seat>();
        foreach (var seatDto in dto.Seats)
        {
            var seat = await _uow.Seats.GetByIdAsync(seatDto.SeatId);
            SeatMapGuards.EnsureSeatExists(seat);

            if (seatDto.Label != null) seat!.Label = seatDto.Label;
            if (seatDto.SeatNumber.HasValue) seat!.SeatNumber = seatDto.SeatNumber.Value;
            if (seatDto.Status.HasValue) seat!.Status = seatDto.Status.Value;
            if (seatDto.SeatType.HasValue) seat!.SeatType = seatDto.SeatType.Value;
            if (seatDto.Position != null) seat!.Position = seatDto.Position;
            if (seatDto.LegendId.HasValue) seat!.LegendId = seatDto.LegendId;
            if (seatDto.CustomProperties != null) seat!.CustomProperties = seatDto.CustomProperties;
            seat!.MarkUpdated();

            updatedSeats.Add(seat);
        }

        await _uow.Seats.UpdateRangeAsync(updatedSeats);
        seatMap.IncrementVersion();
        await _uow.SeatMaps.UpdateAsync(seatMap);
        await _uow.SaveChangesAsync();

        return _mapper.Map<List<SeatResponse>>(updatedSeats);
    }

    public async Task DeleteSeatsAsync(Guid seatMapId, Guid orgId, List<Guid> seatIds)
    {
        var seatMap = await GetAndValidateSeatMap(seatMapId, orgId);

        await _uow.Seats.DeleteRangeAsync(seatIds);
        seatMap.IncrementVersion();
        await _uow.SeatMaps.UpdateAsync(seatMap);
        await _uow.SaveChangesAsync();
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

    // ========== Private Helpers ==========

    private async Task<SeatMap> GetAndValidateSeatMap(Guid seatMapId, Guid orgId)
    {
        var seatMap = await _uow.SeatMaps.GetByIdAsync(seatMapId);
        SeatMapGuards.EnsureExists(seatMap);
        SeatMapGuards.EnsureOwner(seatMap!, orgId);
        SeatMapGuards.EnsureDraft(seatMap!);
        return seatMap!;
    }
}

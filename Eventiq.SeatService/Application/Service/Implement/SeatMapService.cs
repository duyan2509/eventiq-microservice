using AutoMapper;
using Eventiq.SeatService.Application.Dtos;
using Eventiq.SeatService.Application.Guards;
using Eventiq.SeatService.Application.Service.Interface;
using Eventiq.SeatService.Domain.Entity;
using Eventiq.SeatService.Domain.Enum;

namespace Eventiq.SeatService.Application.Service.Implement;

public class SeatMapService : ISeatMapService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public SeatMapService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<List<SeatMapResponse>> GetByEventIdAsync(Guid eventId)
    {
        var seatMaps = await _uow.SeatMaps.GetByEventIdAsync(eventId);
        return _mapper.Map<List<SeatMapResponse>>(seatMaps);
    }

    public async Task<SeatMapDetailResponse> GetByIdAsync(Guid id)
    {
        var seatMap = await _uow.SeatMaps.GetByIdWithDetailsAsync(id);
        SeatMapGuards.EnsureExists(seatMap);
        return _mapper.Map<SeatMapDetailResponse>(seatMap!);
    }

    public async Task<SeatMapResponse> CreateAsync(Guid userId, Guid orgId, CreateSeatMapDto dto)
    {
        // Check if a seat map already exists for this chart
        var existing = await _uow.SeatMaps.GetByChartIdAsync(dto.ChartId);
        if (existing != null)
            throw new ConflictException($"A seat map already exists for chart {dto.ChartId}.");

        var seatMap = _mapper.Map<SeatMap>(dto);
        seatMap.Id = Guid.NewGuid();
        seatMap.OrganizationId = orgId;
        seatMap.Status = SeatMapStatus.Draft;
        seatMap.Version = 1;

        await _uow.SeatMaps.AddAsync(seatMap);
        await _uow.SaveChangesAsync();

        return _mapper.Map<SeatMapResponse>(seatMap);
    }

    public async Task<SeatMapResponse> UpdateSettingsAsync(Guid userId, Guid orgId, Guid seatMapId, UpdateSeatMapSettingsDto dto)
    {
        var seatMap = await _uow.SeatMaps.GetByIdAsync(seatMapId);
        SeatMapGuards.EnsureExists(seatMap);
        SeatMapGuards.EnsureOwner(seatMap!, orgId);
        SeatMapGuards.EnsureDraft(seatMap!);

        if (!string.IsNullOrWhiteSpace(dto.Name))
            seatMap!.Name = dto.Name;
        if (dto.CanvasSettings != null)
            seatMap!.CanvasSettings = dto.CanvasSettings;

        seatMap!.IncrementVersion();
        await _uow.SeatMaps.UpdateAsync(seatMap);
        await _uow.SaveChangesAsync();

        return _mapper.Map<SeatMapResponse>(seatMap);
    }

    public async Task DeleteAsync(Guid orgId, Guid seatMapId)
    {
        var seatMap = await _uow.SeatMaps.GetByIdAsync(seatMapId);
        SeatMapGuards.EnsureExists(seatMap);
        SeatMapGuards.EnsureOwner(seatMap!, orgId);

        var deleted = await _uow.SeatMaps.DeleteAsync(seatMapId);
        if (!deleted)
            throw new BusinessException("Failed to delete seat map.");
        await _uow.SaveChangesAsync();
    }

    public async Task<SeatMapResponse> PublishAsync(Guid orgId, Guid seatMapId)
    {
        var seatMap = await _uow.SeatMaps.GetByIdAsync(seatMapId);
        SeatMapGuards.EnsureExists(seatMap);
        SeatMapGuards.EnsureOwner(seatMap!, orgId);

        seatMap!.Publish();
        await _uow.SeatMaps.UpdateAsync(seatMap);
        await _uow.SaveChangesAsync();

        return _mapper.Map<SeatMapResponse>(seatMap);
    }

    public async Task<SeatMapStatsResponse> GetStatsAsync(Guid seatMapId)
    {
        var seatMap = await _uow.SeatMaps.GetByIdWithDetailsAsync(seatMapId);
        SeatMapGuards.EnsureExists(seatMap);

        var allSeats = seatMap!.Sections
            .SelectMany(s => s.Rows)
            .SelectMany(r => r.Seats)
            .ToList();

        return new SeatMapStatsResponse
        {
            TotalSeats = allSeats.Count,
            AvailableSeats = allSeats.Count(s => s.Status == SeatStatus.Available),
            ReservedSeats = allSeats.Count(s => s.Status == SeatStatus.Reserved),
            SoldSeats = allSeats.Count(s => s.Status == SeatStatus.Sold),
            BlockedSeats = allSeats.Count(s => s.Status == SeatStatus.Blocked),
            TotalSections = seatMap.Sections.Count,
            TotalRows = seatMap.Sections.SelectMany(s => s.Rows).Count()
        };
    }
}

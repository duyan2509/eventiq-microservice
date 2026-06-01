using AutoMapper;
using Eventiq.Contracts;
using Eventiq.SeatService.Application.Dtos;
using Eventiq.SeatService.Application.Guards;
using Eventiq.SeatService.Application.Service.Interface;
using Eventiq.SeatService.Domain.Entity;
using Eventiq.SeatService.Domain.Enum;
using MassTransit;

namespace Eventiq.SeatService.Application.Service.Implement;

public class SeatMapService : ISeatMapService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IPublishEndpoint _publishEndpoint;

    public SeatMapService(IUnitOfWork uow, IMapper mapper, IPublishEndpoint publishEndpoint)
    {
        _uow = uow;
        _mapper = mapper;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<List<SeatMapResponse>> GetByEventIdAsync(Guid eventId)
    {
        var seatMaps = await _uow.SeatMaps.GetByEventIdAsync(eventId);
        return _mapper.Map<List<SeatMapResponse>>(seatMaps);
    }

    public async Task<SeatMapMetaResponse> GetByIdAsync(Guid id)
    {
        var seatMap = await _uow.SeatMaps.GetByIdWithObjectsAsync(id);
        SeatMapGuards.EnsureExists(seatMap);
        return await BuildMetaAsync(seatMap!);
    }

    public async Task<List<SeatResponse>> GetSeatsAsync(Guid seatMapId)
    {
        var seats = await _uow.Seats.GetBySeatMapIdAsync(seatMapId);
        return _mapper.Map<List<SeatResponse>>(seats);
    }

    public async Task<SeatMapMetaResponse> GetSessionMetaAsync(Guid sessionId)
    {
        var seatMap = await _uow.SeatMaps.GetBySessionIdWithObjectsAsync(sessionId);
        SeatMapGuards.EnsureExists(seatMap);
        return await BuildMetaAsync(seatMap!);
    }

    public async Task<SeatLayoutChunkResponse> GetSessionSeatsAsync(Guid sessionId, BboxDto? bbox)
    {
        var seatMap = await _uow.SeatMaps.GetBySessionIdAsync(sessionId);
        SeatMapGuards.EnsureExists(seatMap);

        var seats = bbox is null
            ? await _uow.Seats.GetBySeatMapIdAsync(seatMap!.Id)
            : await _uow.Seats.GetByBboxAsync(seatMap!.Id, bbox.X1, bbox.Y1, bbox.X2, bbox.Y2);

        return new SeatLayoutChunkResponse
        {
            Seats = _mapper.Map<List<SeatLayoutResponse>>(seats),
            Bbox = bbox ?? new BboxDto(),
        };
    }

    // Builds a metadata response (objects already loaded) and fills bounds + total seat count.
    private async Task<SeatMapMetaResponse> BuildMetaAsync(Domain.Entity.SeatMap seatMap)
    {
        var meta = _mapper.Map<SeatMapMetaResponse>(seatMap);
        var bounds = await _uow.Seats.GetSeatBoundsAsync(seatMap.Id);
        meta.FullBbox = new BboxDto { X1 = bounds.MinX, Y1 = bounds.MinY, X2 = bounds.MaxX, Y2 = bounds.MaxY };
        meta.TotalSeats = bounds.Total;
        return meta;
    }

    public async Task<SeatMapResponse> CreateAsync(Guid userId, Guid orgId, CreateSeatMapDto dto)
    {
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
        var seatMap = await _uow.SeatMaps.GetByIdWithDetailsAsync(seatMapId);
        SeatMapGuards.EnsureExists(seatMap);
        SeatMapGuards.EnsureOwner(seatMap!, orgId);

        var totalSeats = seatMap!.Seats.Count;

        seatMap.Publish();
        seatMap.TotalSeats = totalSeats;
        await _uow.SeatMaps.UpdateAsync(seatMap);
        await _uow.SaveChangesAsync();

        await _publishEndpoint.Publish(new SeatMapPublished
        {
            SeatMapId = seatMapId,
            ChartId = seatMap.ChartId,
            EventId = seatMap.EventId,
            OrganizationId = seatMap.OrganizationId,
            TotalSeats = totalSeats
        });

        return _mapper.Map<SeatMapResponse>(seatMap);
    }

    public async Task<bool> HasPublishedTemplateForEventAsync(Guid eventId)
    {
        var maps = await _uow.SeatMaps.GetByEventIdAsync(eventId);
        return maps.Any(m => m.SessionId == null && m.Status == SeatMapStatus.Published);
    }

    public async Task<bool> HasSeatMapDesignAsync(Guid eventId)
        => await _uow.SeatMaps.HasTemplateForEventAsync(eventId);

    public async Task<SeatMapStatsResponse> GetStatsAsync(Guid seatMapId)
    {
        var seatMap = await _uow.SeatMaps.GetByIdWithDetailsAsync(seatMapId);
        SeatMapGuards.EnsureExists(seatMap);

        var seats = seatMap!.Seats;

        return new SeatMapStatsResponse
        {
            TotalSeats = seats.Count,
            AvailableSeats = seats.Count(s => s.Status == SeatStatus.Available),
            HoldingSeats = seats.Count(s => s.Status == SeatStatus.Holding),
            SoldSeats = seats.Count(s => s.Status == SeatStatus.Sold),
            BlockedSeats = seats.Count(s => s.Status == SeatStatus.Blocked),
        };
    }
}

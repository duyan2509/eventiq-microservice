using AutoMapper;
using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Dtos;
using Eventiq.EventService.Guards;
using Eventiq.EventService.Infrastructure.Persistence.ReadModel;

namespace Eventiq.EventService.Application.Service;

public class EventService : IEventService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IBlobService _blobService;

    public EventService(IUnitOfWork uow, IMapper mapper, IBlobService blobService)
    {
        _uow = uow;
        _mapper = mapper;
        _blobService = blobService;
    }

    public async Task<PaginatedResult<EventQuickViewData>> GetAllEventsAsync(
        string? query,
        EventStatus? status,
        string? province,
        Guid? organizationId,
        bool newest = true,
        bool increasePrice = true,
        int page = 1,
        int size = 10)
    {
        var rs = await _uow.Events.GetAllEventsAsync(query, status, province, organizationId, newest, increasePrice, page, size);

        var data = rs.Data.Select(ToQuickView);

        return new PaginatedResult<EventQuickViewData>
        {
            Data = data,
            Total = rs.Total,
            Page = rs.Page,
            Size = rs.Size
        };
    }

    public async Task<EventQuickViewData> CreateEventAsync(Guid userId, Guid orgId, CreateEventDto dto, IFormFile? banner = null)
    {
        // Upload banner to Azure Blob Storage if provided
        string? bannerUrl = null;
        if (banner is { Length: > 0 })
        {
            using var stream = banner.OpenReadStream();
            bannerUrl = await _blobService.UploadAsync(stream, banner.FileName, banner.ContentType);
        }

        var ev = new Event
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            OrganizationName = string.Empty,
            OranizationAvatar = null,
            EventBanner = bannerUrl,
            Name = dto.Name,
            Description = dto.Description,
            DetailAddress = dto.DetailAddress,
            ProvinceCode = dto.ProvinceCode,
            CommuneCode = dto.CommuneCode,
            ProvinceName = dto.ProvinceName,
            CommuneName = dto.CommuneName,
            Status = EventStatus.Draft,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime
        };

        if (ev.StartTime.HasValue && ev.EndTime.HasValue && ev.StartTime.Value >= ev.EndTime.Value)
        {
            throw new BusinessException("Event StartTime must be earlier than EndTime");
        }

        try
        {
            await _uow.BeginTransactionAsync();
            await _uow.Events.AddAsync(ev);
            await _uow.CommitAsync();
        }
        catch
        {
            await _uow.RollbackAsync();
            // Clean up uploaded banner if DB save fails
            if (bannerUrl != null)
                await _blobService.DeleteAsync(bannerUrl);
            throw;
        }

        return new EventQuickViewData
        {
            Id = ev.Id,
            EventBanner = ev.EventBanner,
            Name = ev.Name,
            Start = ev.StartTime ?? DateTime.MinValue,
            Status = ev.Status.ToString(),
            LowestPrice = null,
            ProvinceName = ev.ProvinceName
        };
    }

    public async Task<EventDetail> GetDetailEventAsync(Guid userId, Guid eventId)
    {
        var evt = await _uow.Events.GetByIdAsync(eventId);
        EventGuards.EnsureExist(evt);

        // load legends and sessions via read models
        var legendsPage = await _uow.Legends.GetAllLegendsByEventIdAsync(eventId, page: 1, size: int.MaxValue);
        var sessionsPage = await _uow.Sessions.GetAllSessionsByEventIdAsync(eventId, page: 1, size: int.MaxValue);

        var legends = legendsPage.Data.Select(l => _mapper.Map<LegendResponse>(l)).ToList();
        var sessions = sessionsPage.Data.Select(s => _mapper.Map<SessionResponse>(s)).ToList();

        return new EventDetail
        {
            Id = evt.Id,
            OrganizationId = evt.OrganizationId,
            EventBanner = evt.EventBanner,
            Name = evt.Name,
            StartTime = evt.StartTime ?? DateTime.MinValue,
            EndTime = evt.EndTime ?? DateTime.MinValue,
            Description = evt.Description,
            Status = evt.Status.ToString(),
            DetailAddress = evt.DetailAddress,
            ProvinceCode = evt.ProvinceCode,
            CommuneCode = evt.CommuneCode,
            ProvinceName = evt.ProvinceName,
            CommuneName = evt.CommuneName,
            Legends = legends,
            Sessions = sessions
        };
    }

    public async Task<EventQuickViewData> UpdateEventAsync(Guid userId, Guid eventId, UpdateEventDto dto, IFormFile? banner = null)
    {
        // First load current event to validate time range
        var current = await _uow.Events.GetByIdAsync(eventId);
        EventGuards.EnsureExist(current);

        var newStart = dto.StartTime ?? current.StartTime;
        var newEnd = dto.EndTime ?? current.EndTime;

        if (newStart.HasValue && newEnd.HasValue && newStart.Value >= newEnd.Value)
        {
            throw new BusinessException("Event StartTime must be earlier than EndTime");
        }

        // Upload new banner if provided
        string? newBannerUrl = null;
        if (banner is { Length: > 0 })
        {
            using var stream = banner.OpenReadStream();
            newBannerUrl = await _blobService.UploadAsync(stream, banner.FileName, banner.ContentType);
            dto.EventBanner = newBannerUrl;
        }

        EventModel? updated;
        var oldBannerUrl = current.EventBanner;
        try
        {
            await _uow.BeginTransactionAsync();
            updated = await _uow.Events.UpdatePartialAsync(eventId, dto);
            await _uow.CommitAsync();

            // Delete old banner after successful DB update
            if (newBannerUrl != null && !string.IsNullOrEmpty(oldBannerUrl))
                await _blobService.DeleteAsync(oldBannerUrl);
        }
        catch
        {
            await _uow.RollbackAsync();
            // Clean up newly uploaded banner if DB update fails
            if (newBannerUrl != null)
                await _blobService.DeleteAsync(newBannerUrl);
            throw;
        }

        EventGuards.EnsureExist(updated);

        // recompute lowest price
        var legendsPage = await _uow.Legends.GetAllLegendsByEventIdAsync(eventId, page: 1, size: int.MaxValue);
        var lowestPrice = legendsPage.Data.Any() ? legendsPage.Data.Min(l => l.Price) : (int?)null;

        return new EventQuickViewData
        {
            Id = updated.Id,
            EventBanner = updated.EventBanner,
            Name = updated.Name,
            Start = updated.StartTime ?? DateTime.MinValue,
            Status = updated.Status.ToString(),
            LowestPrice = lowestPrice,
            ProvinceName = updated.ProvinceName
        };
    }

    private static EventQuickViewData ToQuickView(EventModel ev) =>
        new EventQuickViewData
        {
            Id = ev.Id,
            EventBanner = ev.EventBanner,
            Name = ev.Name,
            Start = ev.StartTime ?? DateTime.MinValue,
            Status = ev.Status.ToString(),
            LowestPrice = ev.LowestPrice,
            ProvinceName = ev.ProvinceName
        };
}

using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Dtos;

namespace Eventiq.EventService.Application.Service;

public interface IEventService
{
    Task<PaginatedResult<EventQuickViewData>> GetAllEventsAsync(string? query, EventStatus? status, string? province, Guid? organizationId, bool newest = true, bool increasePrice = true, int page = 1, int size = 10);
    Task<EventQuickViewData> CreateEventAsync(Guid userId, Guid orgId, CreateEventDto dto, IFormFile? banner = null);
    Task<EventDetail> GetDetailEventAsync(Guid userId, Guid eventId);
    Task<EventQuickViewData> UpdateEventAsync(Guid userId, Guid eventId, UpdateEventDto dto, IFormFile? banner = null);
}


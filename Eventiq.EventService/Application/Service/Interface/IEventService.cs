using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Dtos;

namespace Eventiq.EventService.Application.Service;

public interface IEventService
{
    Task<PaginatedResult<EventQuickViewData>> GetAllEventsAsync(string? query, EventStatus? status, string? province,bool newest = true, bool increasePrice = true,int page=1, int size=10);
    Task<EventQuickViewData> CreateEventAsync(Guid userId, Guid orgId, CreateEventDto  dto);
    Task<EventDetail> GetDetailEventAsync(Guid userId, Guid eventId); 
    Task<EventQuickViewData> UpdateEventAsync(Guid userId, Guid eventId, UpdateEventDto  dto);
}


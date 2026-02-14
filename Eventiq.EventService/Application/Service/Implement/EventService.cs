using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Domain.Repositories;
using Eventiq.EventService.Dtos;

namespace Eventiq.EventService.Application.Service;

public class EventService : IEventService
{
    public Task<PaginatedResult<EventQuickViewData>> GetAllEventsAsync(string? query, EventStatus? status, string? province, bool newest = true,
        bool increasePrice = true, int page = 1, int size = 10)
    {
        throw new NotImplementedException();
    }

    public Task<EventQuickViewData> CreateEventAsync(Guid userId, Guid orgId, CreateEventDto dto)
    {
        throw new NotImplementedException();
    }

    public Task<EventDetail> GetDetailEventAsync(Guid userId, Guid eventId)
    {
        throw new NotImplementedException();
    }

    public Task<EventQuickViewData> UpdateEventAsync(Guid userId, Guid eventId, UpdateEventDto dto)
    {
        throw new NotImplementedException();
    }
}

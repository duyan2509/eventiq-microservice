using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Dtos;
using Eventiq.EventService.Infrastructure.Persistence.ReadModel;

namespace Eventiq.EventService.Domain.Repositories;

public interface IEventRepository
{
    Task<EventModel?> GetByIdAsync(Guid eventId);
    Task SetEventStatusAsync(Guid eventId, EventStatus status);

    Task<PaginatedResult<EventModel>> GetAllEventsAsync(
        string? query,
        EventStatus? status,
        string? province,
        bool newest,
        bool increasePrice,
        int page,
        int size);

    Task<int> AddAsync(Event ev);

    Task<EventModel?> UpdatePartialAsync(Guid eventId, UpdateEventDto dto);
}

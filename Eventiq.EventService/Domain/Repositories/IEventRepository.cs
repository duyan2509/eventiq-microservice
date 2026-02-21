using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Dtos;
using Eventiq.EventService.Infrastructure.Persistence.ReadModel;

namespace Eventiq.EventService.Domain.Repositories;

public interface IEventRepository
{
    Task<EventModel> GetByIdAsync(Guid eventId);
    Task SetEventStatusAsync(Guid eventId, EventStatus draft);
}

using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Dtos;
using Eventiq.EventService.Infrastructure.Persistence.ReadModel;

namespace Eventiq.EventService.Domain.Repositories;

public interface ISessionRepository
{
    Task<PaginatedResult<SessionModel>> GetAllSessionsByEventIdAsync(Guid eventId, int page, int size);
    Task<int> AddAsync(Guid eventId, Session session);
    Task<int> UpdateAsync(Session session);
    Task<int> DeleteAsync(Guid eventId, Guid orgId, Guid sessionId);
    Task<Session?> GetByIdAsync(Guid sessionId);

    Task<bool> CheckOverlappedAsync(Guid eventId, Guid sessionId, DateTime sessionStartTime, DateTime sessionEndTime);
}



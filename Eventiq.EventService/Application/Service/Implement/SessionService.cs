using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Domain.Repositories;
using Eventiq.EventService.Dtos;

namespace Eventiq.EventService.Application.Service;

public class SessionService : ISessionService
{
    public Task<PaginatedResult<SessionResponse>> GetAllSessionByEventIdAsync(Guid eventId, int page = 1, int size = 10)
    {
        throw new NotImplementedException();
    }

    public Task<SessionResponse> CreateSessionAsync(Guid userId, Guid orgId, Guid eventId, CreateSessionDto dto)
    {
        throw new NotImplementedException();
    }

    public Task<SessionResponse> UpdateSessionAsync(Guid userId, Guid orgId, Guid eventId, Guid sessionId, UpdateSessionDto dto)
    {
        throw new NotImplementedException();
    }

    public Task DeleteSessionAsync(Guid userId, Guid orgId, Guid sessionId)
    {
        throw new NotImplementedException();
    }
}

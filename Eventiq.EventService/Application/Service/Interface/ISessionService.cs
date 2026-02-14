using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Dtos;

namespace Eventiq.EventService.Application.Service;

public interface ISessionService
{
    Task<PaginatedResult<SessionResponse>> GetAllSessionByEventIdAsync(Guid eventId, int page = 1, int size = 10);
    Task<SessionResponse> CreateSessionAsync(Guid userId, Guid orgId, Guid eventId, CreateSessionDto dto);
    Task<SessionResponse> UpdateSessionAsync(Guid userId, Guid orgId, Guid eventId, Guid sessionId, UpdateSessionDto dto);
    Task DeleteSessionAsync(Guid userId, Guid orgId, Guid sessionId);
}


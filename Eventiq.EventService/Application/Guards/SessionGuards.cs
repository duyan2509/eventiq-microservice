using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Domain.Repositories;
using Eventiq.EventService.Infrastructure.Persistence.ReadModel;

namespace Eventiq.EventService.Guards;

public static class SessionGuards
{
    public static void EnsureExist(SessionModel session)
    {
        if(session == null)
            throw new NotFoundException("Session not found");
    }
    public static void EnsureExist(Session session)
    {
        if(session == null)
            throw new NotFoundException("Session not found");
    }
}
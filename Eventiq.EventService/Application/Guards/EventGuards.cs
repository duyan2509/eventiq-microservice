using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Infrastructure.Persistence.ReadModel;

namespace Eventiq.EventService.Guards;

public static class EventGuards
{
    public static void EnsureExist(EventModel e)
    {
        if(e==null)
            throw new NotFoundException("Not found event");
    }

    public static void EnsureOwner(EventModel evt, Guid orgId)
    {
        if(evt.OrganizationId!=orgId)
            throw new ForbiddenException("You are not the owner of this event");
    }

    public static void EnsureDraft(EventModel evt)
    {
        if(evt.Status!=EventStatus.Draft)
            throw new BusinessException("Only draft events are supported");
    }
}
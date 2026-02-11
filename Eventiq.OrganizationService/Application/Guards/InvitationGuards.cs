using Eventiq.OrganizationService.Domain.Entity;
using Eventiq.OrganizationService.Domain.Enum;

namespace Eventiq.OrganizationService.Guards;

public static class InvitationGuards
{
    public static void EnsureExist(Invitation? invitation)
    {
        if(invitation==null)
            throw new NotFoundException("Invitation not found");
    }

    public static void EnsureCanResponse(Invitation? invitation)
    {
        if(invitation.ExpiresAt < DateTime.UtcNow)
            throw new BusinessException("Invitation has expired");
        if(invitation.Status!=InvitationStatus.PENDING)
            throw new BusinessException("Invitation has already been accepted");
    }

    public static void EnsureOrgInvitation(Invitation invitation, Guid orgId)
    {
        if(invitation.OrganizationId != orgId)
            throw new BusinessException("Invitation is not in org");
    }

    public static DateTime GetExpiresAfter7Day()
    {
        return DateTime.UtcNow.AddDays(7);
    }
}
using Eventiq.Contracts;
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
            
        switch (invitation.Status)
        {
            case InvitationStatus.ACCEPTED:
                throw new BusinessException("Invitation has already been accepted");
            case InvitationStatus.REJECTED:
                throw new BusinessException("Invitation has already been rejected");
            case InvitationStatus.CANCELED:
                throw new BusinessException("Invitation has been canceled by the organization");
        }
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

    public static void EnsureNotActive(Invitation invitation)
    {
        if (invitation.Status == InvitationStatus.ACCEPTED)
            throw new BusinessException("User has already accepted an invitation");
            
        if (invitation.Status == InvitationStatus.PENDING && invitation.ExpiresAt > DateTime.UtcNow)
            throw new BusinessException("A pending invitation has already been sent and hasn't expired");
    }
}
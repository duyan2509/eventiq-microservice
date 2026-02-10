using Eventiq.OrganizationService.Domain.Entity;

namespace Eventiq.OrganizationService.Guards;

public static class OwnerGuards
{
    public static void EnsureOwner(Organization organization, Guid userId)
    {
        if (organization.OwnerId != userId)
            throw new ForbiddenException("You are not the owner of this organization");
    }
}
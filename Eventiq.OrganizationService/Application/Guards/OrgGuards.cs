using Eventiq.OrganizationService.Domain.Entity;

namespace Eventiq.OrganizationService.Guards;

public static class OrgGuards
{
    public static void EnsureExists(Organization? org)
    {
        if (org == null)
            throw new NotFoundException("Organization not found");
    }
}
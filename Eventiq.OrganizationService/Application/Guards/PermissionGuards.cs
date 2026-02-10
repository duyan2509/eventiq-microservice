using Eventiq.OrganizationService.Domain.Entity;

namespace Eventiq.OrganizationService.Guards;

public static class PermissionGuards
{
    public static void EnsureExists(Permission? permission)
    {
        if (permission == null)
            throw new NotFoundException("Permission not found");
    }
    public static void EnsureNotOwnerPermission(Permission? permission)
    {
        if (permission.Name=="Owner")
            throw new BusinessException("Cannot modify Owner permission.");
    }

    public static void EnsureNotDuplicatePermission(Permission permission, Guid permissionId)
    {
        if(permissionId==permission.Id)
            throw new BusinessException($"Member has already had {permission.Name} permission.");
    }
}
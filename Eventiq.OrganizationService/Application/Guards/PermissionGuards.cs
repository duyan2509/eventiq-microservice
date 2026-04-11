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
        if (permission?.Name == "Owner")
            throw new BusinessException("Cannot interact Owner permission.");
    }

    public static void EnsureNotDuplicatePermission(Guid newPermissionId, Guid? oldPermissionId)
    {
        if(newPermissionId == oldPermissionId)
            throw new BusinessException($"Member has already had this permission.");
    }
}
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

    public static void EnsureNoMembersAssigned(bool hasMembersWithPermission)
    {
        if (hasMembersWithPermission)
            throw new BusinessException("Cannot delete permission that is still assigned to members. Please reassign or remove members first.");
    }

    public static void EnsureNameNotDuplicate(bool exists)
    {
        if (exists)
            throw new ConflictException("A permission with this name already exists in the organization.");
    }
}
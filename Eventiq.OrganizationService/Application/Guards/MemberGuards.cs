using Eventiq.OrganizationService.Domain.Entity;

namespace Eventiq.OrganizationService.Guards;

public static class MemberGuards
{
    public static void EnsureExists(Member? member)
    {
        if (member == null)
            throw new NotFoundException("Member not found");
    }

    public static void EnsureNotOwner(Member? member)
    {
        if (member.Permission.Name == "Owner")
            throw new BusinessException("Cannot remove Owner");
    }
}
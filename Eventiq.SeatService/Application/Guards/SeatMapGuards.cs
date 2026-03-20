using Eventiq.SeatService.Domain.Entity;
using Eventiq.SeatService.Domain.Enum;

namespace Eventiq.SeatService.Application.Guards;

public static class SeatMapGuards
{
    public static void EnsureExists(SeatMap? seatMap)
    {
        if (seatMap == null)
            throw new NotFoundException("Seat map not found.");
    }

    public static void EnsureOwner(SeatMap seatMap, Guid organizationId)
    {
        if (seatMap.OrganizationId != organizationId)
            throw new ForbiddenException("You do not have access to this seat map.");
    }

    public static void EnsureDraft(SeatMap seatMap)
    {
        if (seatMap.Status != SeatMapStatus.Draft)
            throw new BusinessException("Seat map must be in Draft status to edit.");
    }

    public static void EnsureSectionExists(SeatSection? section)
    {
        if (section == null)
            throw new NotFoundException("Section not found.");
    }

    public static void EnsureRowExists(SeatRow? row)
    {
        if (row == null)
            throw new NotFoundException("Row not found.");
    }

    public static void EnsureSeatExists(Seat? seat)
    {
        if (seat == null)
            throw new NotFoundException("Seat not found.");
    }

    public static void EnsureObjectExists(SeatObject? obj)
    {
        if (obj == null)
            throw new NotFoundException("Seat object not found.");
    }
}

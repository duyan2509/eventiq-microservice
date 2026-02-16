using Eventiq.EventService.Dtos;
using Eventiq.EventService.Infrastructure.Persistence.ReadModel;

namespace Eventiq.EventService.Guards;

public static class LegendGuards
{
    public static void EnsureExist(LegendModel? legend)
    {
        if(legend ==null)
            throw new NotFoundException("Legend not found");
    }
}
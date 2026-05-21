using Eventiq.SeatService.Application.Service.Interface;
using Eventiq.SeatService.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Eventiq.SeatService.Infrastructure.SignalR;

public class SignalRSeatStatusBroadcaster : ISeatStatusBroadcaster
{
    private readonly IHubContext<SeatBookingHub> _hub;

    public SignalRSeatStatusBroadcaster(IHubContext<SeatBookingHub> hub)
    {
        _hub = hub;
    }

    public Task BroadcastSeatStatusAsync(Guid seatMapId, IEnumerable<SeatStatusUpdate> updates)
        => _hub.Clients
            .Group(SeatBookingHub.GroupName(seatMapId))
            .SendAsync("SeatsStatusChanged", updates);
}

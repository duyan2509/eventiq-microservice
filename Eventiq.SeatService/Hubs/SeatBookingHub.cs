using System.Security.Claims;
using Eventiq.SeatService.Application;
using Eventiq.SeatService.Application.Service.Interface;
using Eventiq.SeatService.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Eventiq.SeatService.Hubs;

[Authorize]
public class SeatBookingHub : Hub
{
    private readonly ISeatRepository _seats;
    private readonly ILogger<SeatBookingHub> _logger;

    public SeatBookingHub(ISeatRepository seats, ILogger<SeatBookingHub> logger)
    {
        _seats = seats;
        _logger = logger;
    }

    public async Task JoinSeatMap(Guid seatMapId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(seatMapId));

        // Send current seat statuses as initial snapshot.
        var seats = await _seats.GetBySeatMapIdAsync(seatMapId);
        var snapshot = seats.Select(s => new SeatStatusUpdate(s.Id, s.Status.ToString(), s.HeldUntil));

        await Clients.Caller.SendAsync("InitialSeatStatuses", snapshot);

        _logger.LogInformation("User {UserId} joined booking view for seat map {SeatMapId}", GetUserId(), seatMapId);
    }

    public async Task LeaveSeatMap(Guid seatMapId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(seatMapId));
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("Booking hub connection {ConnectionId} disconnected", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    private Guid GetUserId()
    {
        var sub = Context.User?.FindFirst("sub")?.Value
            ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var id) ? id : throw new UnauthorizedException("Invalid user.");
    }

    public static string GroupName(Guid seatMapId) => $"booking-{seatMapId}";
}

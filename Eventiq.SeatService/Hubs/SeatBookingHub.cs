using System.Security.Claims;
using Eventiq.SeatService.Application;
using Eventiq.SeatService.Application.Dtos;
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

    // Join the live-update group. No longer sends the full status snapshot —
    // statuses are requested per viewport region via GetRegionStatuses.
    public async Task JoinSeatMap(Guid seatMapId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(seatMapId));
        _logger.LogInformation("User {UserId} joined booking view for seat map {SeatMapId}", GetUserId(), seatMapId);
    }

    // Send current statuses for seats within a bounding box (the caller's viewport).
    // Invoked on initial load and whenever the client pans/zooms into new territory.
    // A null bbox returns statuses for every seat (zoom-out / small maps).
    public async Task GetRegionStatuses(Guid seatMapId, BboxDto? bbox)
    {
        var seats = bbox is null
            ? await _seats.GetBySeatMapIdAsync(seatMapId)
            : await _seats.GetByBboxAsync(seatMapId, bbox.X1, bbox.Y1, bbox.X2, bbox.Y2);

        var snapshot = seats.Select(s => new SeatStatusUpdate(s.Id, s.Status.ToString(), s.HeldUntil));
        await Clients.Caller.SendAsync("InitialSeatStatuses", snapshot);
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

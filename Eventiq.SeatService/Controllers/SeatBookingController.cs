using Eventiq.SeatService.Application;
using Eventiq.SeatService.Application.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eventiq.SeatService.Controllers;

public record HoldSeatsRequest(IReadOnlyList<Guid> SeatIds);
public record ReleaseSeatsRequest(IReadOnlyList<Guid> SeatIds);

[ApiController]
[Route("api/seat-maps/{seatMapId:guid}/seats")]
[Authorize]
public class SeatBookingController : ControllerBase
{
    private readonly ISeatReservationService _reservation;
    private readonly ISeatStatusBroadcaster _broadcaster;

    public SeatBookingController(ISeatReservationService reservation, ISeatStatusBroadcaster broadcaster)
    {
        _reservation = reservation;
        _broadcaster = broadcaster;
    }

    [HttpPost("hold")]
    public async Task<IActionResult> Hold(Guid seatMapId, [FromBody] HoldSeatsRequest request)
    {
        var userId = GetUserId();
        var result = await _reservation.HoldSeatsAsync(seatMapId, request.SeatIds, userId);

        if (!result.Success)
            return Conflict(new { error = result.Error });

        var updates = result.HeldSeats!
            .Select(s => new SeatStatusUpdate(s.SeatId, "Holding", s.HeldUntil));

        await _broadcaster.BroadcastSeatStatusAsync(seatMapId, updates);

        return Ok(new
        {
            seatIds = result.HeldSeats!.Select(s => s.SeatId),
            status = "Holding",
            heldUntil = result.HeldSeats![0].HeldUntil
        });
    }

    [HttpDelete("hold")]
    public async Task<IActionResult> Release(Guid seatMapId, [FromBody] ReleaseSeatsRequest request)
    {
        var userId = GetUserId();
        var released = await _reservation.ReleaseSeatsAsync(seatMapId, request.SeatIds, userId);

        if (!released)
            return NotFound();

        var updates = request.SeatIds.Select(id => new SeatStatusUpdate(id, "Available"));
        await _broadcaster.BroadcastSeatStatusAsync(seatMapId, updates);

        return NoContent();
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var id) ? id : throw new UnauthorizedException("Invalid user.");
    }
}

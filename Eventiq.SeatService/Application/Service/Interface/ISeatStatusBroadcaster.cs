namespace Eventiq.SeatService.Application.Service.Interface;

public record SeatStatusUpdate(Guid SeatId, string Status, DateTime? HeldUntil = null);

public interface ISeatStatusBroadcaster
{
    Task BroadcastSeatStatusAsync(Guid seatMapId, IEnumerable<SeatStatusUpdate> updates);
}

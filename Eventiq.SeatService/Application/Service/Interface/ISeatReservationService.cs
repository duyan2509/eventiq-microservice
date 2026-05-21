namespace Eventiq.SeatService.Application.Service.Interface;

public interface ISeatReservationService
{
    Task<ReservationResult> HoldSeatsAsync(Guid seatMapId, IReadOnlyList<Guid> seatIds, Guid userId);
    Task<bool> ReleaseSeatsAsync(Guid seatMapId, IReadOnlyList<Guid> seatIds, Guid userId);
    Task MarkSoldAsync(IEnumerable<Guid> seatIds);
}

public record HeldSeat(Guid SeatId, DateTime HeldUntil);
public record ReservationResult(bool Success, string? Error = null, IReadOnlyList<HeldSeat>? HeldSeats = null);

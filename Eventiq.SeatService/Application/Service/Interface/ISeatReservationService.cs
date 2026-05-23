namespace Eventiq.SeatService.Application.Service.Interface;

public interface ISeatReservationService
{
    Task<ReservationResult> HoldSeatsAsync(Guid seatMapId, IReadOnlyList<Guid> seatIds, Guid userId);
    Task<bool> ReleaseSeatsAsync(Guid seatMapId, IReadOnlyList<Guid> seatIds, Guid userId);
    Task<MarkSoldResult> MarkSoldAsync(IReadOnlyList<Guid> seatIds, Guid userId);
}

public record HeldSeat(Guid SeatId, DateTime HeldUntil);
public record ReservationResult(bool Success, string? Error = null, IReadOnlyList<HeldSeat>? HeldSeats = null);
public record MarkSoldResult(bool Success, string? Error = null);

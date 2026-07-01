namespace Eventiq.SeatService.Application.Service.Interface;

public interface ISeatReservationService
{
    Task<ReservationResult> HoldSeatsAsync(Guid seatMapId, IReadOnlyList<Guid> seatIds, Guid userId);
    Task<bool> ReleaseSeatsAsync(Guid seatMapId, IReadOnlyList<Guid> seatIds, Guid userId);
    Task<MarkSoldResult> MarkSoldAsync(IReadOnlyList<Guid> seatIds, Guid userId);
    Task<HoldStatusResult> GetHoldStatusAsync(Guid seatMapId, IReadOnlyList<Guid> seatIds, Guid userId);
    Task<ExtendHoldResult> ExtendHoldAsync(IReadOnlyList<Guid> seatIds, Guid userId, TimeSpan duration);
}

public record HeldSeat(Guid SeatId, DateTime HeldUntil);
public record ReservationResult(bool Success, string? Error = null, IReadOnlyList<HeldSeat>? HeldSeats = null);
public record MarkSoldResult(bool Success, string? Error = null);

public record HeldSeatInfo(Guid Id, string Label, int SeatNumber, Guid? LegendId);
public record HoldStatusResult(bool Valid, string? Error = null, DateTime? HeldUntil = null, IReadOnlyList<HeldSeatInfo>? Seats = null);
public record ExtendHoldResult(bool Success, string? Error = null, DateTime? HeldUntil = null);

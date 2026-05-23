using Eventiq.SeatService.Application.Service.Interface;
using Eventiq.SeatService.Domain.Entity;
using Eventiq.SeatService.Domain.Enum;
using Eventiq.SeatService.Domain.Repositories;
using Eventiq.SeatService.Infrastructure;
using RedLockNet;

namespace Eventiq.SeatService.Application.Service.Implement;

public class SeatReservationService : ISeatReservationService
{
    private readonly ISeatRepository _seats;
    private readonly IUnitOfWork _uow;
    private readonly IDistributedLockFactory _redLock;

    private static readonly TimeSpan HoldDuration = TimeSpan.FromMinutes(10);
    // Redlock only guards the critical section of this request; DB hold governs the checkout window.
    private static readonly TimeSpan LockExpiry = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LockWait = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LockRetry = TimeSpan.FromMilliseconds(200);

    public SeatReservationService(
        ISeatRepository seats,
        IUnitOfWork uow,
        IDistributedLockFactory redLock)
    {
        _seats = seats;
        _uow = uow;
        _redLock = redLock;
    }

    public async Task<ReservationResult> HoldSeatsAsync(Guid seatMapId, IReadOnlyList<Guid> seatIds, Guid userId)
    {
        if (seatIds.Count == 0)
            return new ReservationResult(false, "No seats specified.");

        // Sort to prevent deadlocks when concurrent requests target overlapping seats.
        var orderedIds = seatIds.OrderBy(id => id).ToList();
        var acquiredLocks = new List<IRedLock>(orderedIds.Count);

        try
        {
            foreach (var seatId in orderedIds)
            {
                var lockKey = $"seat-lock:{seatMapId}:{seatId}";
                var redLock = await _redLock.CreateLockAsync(lockKey, LockExpiry, LockWait, LockRetry);
                if (!redLock.IsAcquired)
                {
                    return new ReservationResult(false, $"Seat {seatId} is currently being reserved.");
                }
                acquiredLocks.Add(redLock);
            }

            // Validate all seats inside the lock window.
            var seats = await Task.WhenAll(orderedIds.Select(id => _seats.GetByIdAsync(id)));

            for (var i = 0; i < seats.Length; i++)
            {
                if (seats[i] == null)
                    return new ReservationResult(false, $"Seat {orderedIds[i]} not found.");

                if (seats[i]!.Status != SeatStatus.Available)
                    return new ReservationResult(false, $"Seat {seats[i]!.Label} is not available (status: {seats[i]!.Status}).");
            }

            foreach (var seat in seats)
                seat!.Hold(userId, HoldDuration);

            await _seats.UpdateRangeAsync(seats!);
            await _uow.SaveChangesAsync();

            var heldSeats = seats
                .Select(s => new HeldSeat(s!.Id, s.HeldUntil!.Value))
                .ToList();

            return new ReservationResult(true, HeldSeats: heldSeats);
        }
        finally
        {
            foreach (var l in acquiredLocks)
                await l.DisposeAsync();
        }
    }

    public async Task<bool> ReleaseSeatsAsync(Guid seatMapId, IReadOnlyList<Guid> seatIds, Guid userId)
    {
        var seats = await Task.WhenAll(seatIds.Select(id => _seats.GetByIdAsync(id)));
        var toRelease = seats
            .Where(s => s != null && s.Status == SeatStatus.Holding && s.HeldBy == userId)
            .ToList();

        if (toRelease.Count == 0) return false;

        foreach (var seat in toRelease)
            seat!.Release();

        await _seats.UpdateRangeAsync(toRelease!);
        await _uow.SaveChangesAsync();
        return true;
    }

    public async Task<MarkSoldResult> MarkSoldAsync(IReadOnlyList<Guid> seatIds, Guid userId)
    {
        if (seatIds.Count == 0)
            return new MarkSoldResult(true);

        var seats = await _seats.GetByIdsAsync(seatIds);

        var toSell = new List<Seat>();
        foreach (var id in seatIds)
        {
            var seat = seats.FirstOrDefault(s => s.Id == id);
            if (seat == null)
                return new MarkSoldResult(false, $"Seat {id} not found.");

            switch (seat.Status)
            {
                case SeatStatus.Sold:
                    // Already sold — idempotent success
                    continue;
                case SeatStatus.Holding when seat.HeldBy == userId:
                    toSell.Add(seat);
                    break;
                case SeatStatus.Holding:
                    return new MarkSoldResult(false, $"Seat {seat.Label} is held by another user.");
                default:
                    return new MarkSoldResult(false, $"Seat {seat.Label} is not held (status: {seat.Status}).");
            }
        }

        if (toSell.Count == 0)
            return new MarkSoldResult(true);

        foreach (var seat in toSell)
            seat.Sell();

        await _seats.UpdateRangeAsync(toSell);
        await _uow.SaveChangesAsync();
        return new MarkSoldResult(true);
    }
}

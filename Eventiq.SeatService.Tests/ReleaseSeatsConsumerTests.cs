using Eventiq.Contracts;
using Eventiq.SeatService.Consumers;
using Eventiq.SeatService.Application.Service.Interface;
using Eventiq.SeatService.Domain.Entity;
using Eventiq.SeatService.Domain.Enum;
using Eventiq.SeatService.Domain.Repositories;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Eventiq.SeatService.Tests;

public class ReleaseSeatsConsumerTests
{
    private readonly IUnitOfWork _uow;
    private readonly ISeatRepository _seatRepo;
    private readonly ISeatStatusBroadcaster _broadcaster;
    private readonly ReleaseSeatsConsumer _consumer;

    private static readonly Guid SeatMapId = Guid.NewGuid();
    private static readonly Guid OwnerUserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    public ReleaseSeatsConsumerTests()
    {
        _uow      = Substitute.For<IUnitOfWork>();
        _seatRepo = Substitute.For<ISeatRepository>();
        _uow.Seats.Returns(_seatRepo);
        _uow.SaveChangesAsync().Returns(1);

        _broadcaster = Substitute.For<ISeatStatusBroadcaster>();
        _broadcaster
            .BroadcastSeatStatusAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<SeatStatusUpdate>>())
            .Returns(Task.CompletedTask);

        _consumer = new ReleaseSeatsConsumer(_uow, _broadcaster, NullLogger<ReleaseSeatsConsumer>.Instance);
    }

    private Seat MakeHoldingSeat(Guid id, Guid? heldBy = null)
        => new() { Id = id, SeatMapId = SeatMapId, Status = SeatStatus.Holding, HeldBy = heldBy ?? OwnerUserId, Label = "A1" };

    private Seat MakeAvailableSeat(Guid id)
        => new() { Id = id, SeatMapId = SeatMapId, Status = SeatStatus.Available, Label = "A1" };

    private static ConsumeContext<ReleaseSeatsCommand> MakeContext(Guid orderId, Guid userId, params Guid[] seatIds)
    {
        var ctx = Substitute.For<ConsumeContext<ReleaseSeatsCommand>>();
        ctx.Message.Returns(new ReleaseSeatsCommand
        {
            OrderId = orderId,
            UserId  = userId,
            SeatIds = seatIds.ToList()
        });
        return ctx;
    }

    // ── 1. Holding seats by correct user are released ────────────────────────
    [Fact]
    public async Task Consume_HoldingSeats_ReleasesToAvailable()
    {
        var orderId = Guid.NewGuid();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var seat1 = MakeHoldingSeat(id1);
        var seat2 = MakeHoldingSeat(id2);
        _seatRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([seat1, seat2]);

        await _consumer.Consume(MakeContext(orderId, OwnerUserId, id1, id2));

        Assert.Equal(SeatStatus.Available, seat1.Status);
        Assert.Equal(SeatStatus.Available, seat2.Status);
        Assert.Null(seat1.HeldBy);
        Assert.Null(seat2.HeldBy);
        await _uow.Received(1).SaveChangesAsync();
    }

    // ── 2. SeatsReleased is always published (idempotent saga signal) ────────
    [Fact]
    public async Task Consume_AlwaysPublishesSeatsReleased()
    {
        var orderId = Guid.NewGuid();
        var seatId  = Guid.NewGuid();
        _seatRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([MakeHoldingSeat(seatId)]);

        var ctx = MakeContext(orderId, OwnerUserId, seatId);
        await _consumer.Consume(ctx);

        await ctx.Received(1).Publish(Arg.Is<SeatsReleased>(m => m.OrderId == orderId));
    }

    // ── 3. Seats held by a DIFFERENT user are NOT released ──────────────────
    [Fact]
    public async Task Consume_SeatHeldByDifferentUser_IsSkipped()
    {
        var orderId = Guid.NewGuid();
        var seatId  = Guid.NewGuid();
        var seat    = MakeHoldingSeat(seatId, heldBy: OtherUserId); // held by someone else
        _seatRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([seat]);

        await _consumer.Consume(MakeContext(orderId, OwnerUserId, seatId));

        Assert.Equal(SeatStatus.Holding, seat.Status);     // unchanged
        Assert.Equal(OtherUserId, seat.HeldBy);            // still owned by other user
        await _uow.DidNotReceive().SaveChangesAsync();      // no DB write needed
    }

    // ── 4. SeatsReleased still published even if no seats to release ─────────
    [Fact]
    public async Task Consume_NoSeatsToRelease_StillPublishesSeatsReleased()
    {
        var orderId = Guid.NewGuid();
        var seatId  = Guid.NewGuid();
        _seatRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([MakeAvailableSeat(seatId)]);

        var ctx = MakeContext(orderId, OwnerUserId, seatId);
        await _consumer.Consume(ctx);

        await ctx.Received(1).Publish(Arg.Is<SeatsReleased>(m => m.OrderId == orderId));
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    // ── 5. SignalR broadcast fires with "Available" status ───────────────────
    [Fact]
    public async Task Consume_ReleasedSeats_BroadcastsAvailableStatus()
    {
        var orderId = Guid.NewGuid();
        var seatId  = Guid.NewGuid();
        _seatRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([MakeHoldingSeat(seatId)]);

        await _consumer.Consume(MakeContext(orderId, OwnerUserId, seatId));

        await _broadcaster.Received(1).BroadcastSeatStatusAsync(
            SeatMapId,
            Arg.Is<IEnumerable<SeatStatusUpdate>>(u => u.Any(x => x.Status == "Available")));
    }

    // ── 6. Mixed: some releasable, some held by others ───────────────────────
    [Fact]
    public async Task Consume_MixedHolders_OnlyReleasesOwnedByRequestedUser()
    {
        var orderId  = Guid.NewGuid();
        var ownId    = Guid.NewGuid();
        var otherId  = Guid.NewGuid();
        var ownSeat  = MakeHoldingSeat(ownId,   heldBy: OwnerUserId);
        var otherSeat = MakeHoldingSeat(otherId, heldBy: OtherUserId);
        _seatRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([ownSeat, otherSeat]);

        await _consumer.Consume(MakeContext(orderId, OwnerUserId, ownId, otherId));

        Assert.Equal(SeatStatus.Available, ownSeat.Status);   // released
        Assert.Equal(SeatStatus.Holding,   otherSeat.Status); // untouched
    }
}

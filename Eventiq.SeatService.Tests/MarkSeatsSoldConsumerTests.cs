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

public class MarkSeatsSoldConsumerTests
{
    private readonly IUnitOfWork _uow;
    private readonly ISeatRepository _seatRepo;
    private readonly ISeatStatusBroadcaster _broadcaster;
    private readonly MarkSeatsSoldConsumer _consumer;

    private static readonly Guid SeatMapId = Guid.NewGuid();
    private static readonly Guid UserId    = Guid.NewGuid();

    public MarkSeatsSoldConsumerTests()
    {
        _uow      = Substitute.For<IUnitOfWork>();
        _seatRepo = Substitute.For<ISeatRepository>();
        _uow.Seats.Returns(_seatRepo);
        _uow.SaveChangesAsync().Returns(1);

        _broadcaster = Substitute.For<ISeatStatusBroadcaster>();
        _broadcaster
            .BroadcastSeatStatusAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<SeatStatusUpdate>>())
            .Returns(Task.CompletedTask);

        _consumer = new MarkSeatsSoldConsumer(_uow, _broadcaster, NullLogger<MarkSeatsSoldConsumer>.Instance);
    }

    private Seat MakeSeat(Guid id, SeatStatus status = SeatStatus.Holding)
    {
        var s = new Seat { Id = id, SeatMapId = SeatMapId, Status = status, HeldBy = UserId, Label = "A1" };
        if (status != SeatStatus.Holding) s.HeldBy = null;
        return s;
    }

    private static ConsumeContext<MarkSeatsSoldCommand> MakeContext(Guid orderId, params Guid[] seatIds)
    {
        var ctx = Substitute.For<ConsumeContext<MarkSeatsSoldCommand>>();
        ctx.Message.Returns(new MarkSeatsSoldCommand
        {
            OrderId = orderId,
            UserId  = UserId,
            SeatIds = seatIds.ToList()
        });
        return ctx;
    }

    // ── 1. Holding seats are marked Sold ────────────────────────────────────
    [Fact]
    public async Task Consume_HoldingSeats_MarksAllAsSold()
    {
        var orderId = Guid.NewGuid();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var seat1 = MakeSeat(id1);
        var seat2 = MakeSeat(id2);

        _seatRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([seat1, seat2]);

        await _consumer.Consume(MakeContext(orderId, id1, id2));

        Assert.Equal(SeatStatus.Sold, seat1.Status);
        Assert.Equal(SeatStatus.Sold, seat2.Status);
        await _uow.Received(1).SaveChangesAsync();
    }

    // ── 2. SeatsMarkedSold is published ─────────────────────────────────────
    [Fact]
    public async Task Consume_AfterMarkingSold_PublishesSeatsMarkedSold()
    {
        var orderId = Guid.NewGuid();
        var seatId  = Guid.NewGuid();
        _seatRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([MakeSeat(seatId)]);

        var ctx = MakeContext(orderId, seatId);
        await _consumer.Consume(ctx);

        await ctx.Received(1).Publish(Arg.Is<SeatsMarkedSold>(m => m.OrderId == orderId));
    }

    // ── 3. SignalR broadcast fires per seat map ──────────────────────────────
    [Fact]
    public async Task Consume_HoldingSeats_BroadcastsSeatStatusSold()
    {
        var orderId = Guid.NewGuid();
        var seatId  = Guid.NewGuid();
        _seatRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([MakeSeat(seatId)]);

        await _consumer.Consume(MakeContext(orderId, seatId));

        await _broadcaster.Received(1).BroadcastSeatStatusAsync(
            SeatMapId,
            Arg.Is<IEnumerable<SeatStatusUpdate>>(u => u.Any(x => x.Status == "Sold")));
    }

    // ── 4. Already-Sold seat is idempotent (no exception, still publishes) ──
    [Fact]
    public async Task Consume_AlreadySoldSeat_IsIdempotent_StillPublishes()
    {
        var orderId = Guid.NewGuid();
        var seatId  = Guid.NewGuid();
        var seat    = MakeSeat(seatId, SeatStatus.Sold);
        _seatRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([seat]);

        var ctx = MakeContext(orderId, seatId);
        await _consumer.Consume(ctx); // should not throw

        Assert.Equal(SeatStatus.Sold, seat.Status);
        await ctx.Received(1).Publish(Arg.Is<SeatsMarkedSold>(m => m.OrderId == orderId));
    }

    // ── 5. Mixed: some Holding, some already Sold → both processed cleanly ──
    [Fact]
    public async Task Consume_MixedSeats_ProcessesAll()
    {
        var orderId   = Guid.NewGuid();
        var holdingId = Guid.NewGuid();
        var soldId    = Guid.NewGuid();
        var holding   = MakeSeat(holdingId, SeatStatus.Holding);
        var alreadySold = MakeSeat(soldId, SeatStatus.Sold);
        _seatRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([holding, alreadySold]);

        await _consumer.Consume(MakeContext(orderId, holdingId, soldId));

        Assert.Equal(SeatStatus.Sold, holding.Status);
        Assert.Equal(SeatStatus.Sold, alreadySold.Status); // unchanged, still Sold
        await _uow.Received(1).SaveChangesAsync();
    }

    // ── 6. MarkSeatsSoldCommand carries the correct SeatIds to the consumer ─
    [Fact]
    public async Task Consume_CorrectSeatIdsPassedToRepository()
    {
        var orderId = Guid.NewGuid();
        var id1     = Guid.NewGuid();
        var id2     = Guid.NewGuid();
        _seatRepo.GetByIdsAsync(Arg.Any<IEnumerable<Guid>>()).Returns([]);

        await _consumer.Consume(MakeContext(orderId, id1, id2));

        await _seatRepo.Received(1).GetByIdsAsync(
            Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(id1) && ids.Contains(id2)));
    }
}

using Eventiq.Contracts;
using Eventiq.PaymentService.Sagas;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Eventiq.PaymentService.Tests;

/// <summary>
/// Fake consumers simulate SeatService / EventService responding to saga commands.
/// Each one publishes the expected reply event so the saga can advance its state.
/// </summary>
class FakeMarkSeatsSoldConsumer : IConsumer<MarkSeatsSoldCommand>
{
    public Task Consume(ConsumeContext<MarkSeatsSoldCommand> context)
        => context.Publish(new SeatsMarkedSold { OrderId = context.Message.OrderId });
}

class FakeIssueTicketsConsumer : IConsumer<IssueTicketsCommand>
{
    public Task Consume(ConsumeContext<IssueTicketsCommand> context)
        => context.Publish(new TicketsIssued { OrderId = context.Message.OrderId });
}

class FakeReleaseSeatsConsumer : IConsumer<ReleaseSeatsCommand>
{
    public Task Consume(ConsumeContext<ReleaseSeatsCommand> context)
        => context.Publish(new SeatsReleased { OrderId = context.Message.OrderId });
}

// ──────────────────────────────────────────────────────────────────────────────

public class BookingSagaStateMachineTests : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private ITestHarness _harness = null!;
    private ISagaStateMachineTestHarness<BookingSagaStateMachine, BookingSagaState> _sagaHarness = null!;

    private static readonly Guid UserId    = Guid.NewGuid();
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly List<Guid> SeatIds = [Guid.NewGuid(), Guid.NewGuid()];

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddMassTransitTestHarness(x =>
        {
            x.AddSagaStateMachine<BookingSagaStateMachine, BookingSagaState>()
                .InMemoryRepository();

            x.AddConsumer<FakeMarkSeatsSoldConsumer>()
                .Endpoint(e => e.Name = "seat-service-mark-sold");
            x.AddConsumer<FakeIssueTicketsConsumer>()
                .Endpoint(e => e.Name = "event-service-issue-tickets");
            x.AddConsumer<FakeReleaseSeatsConsumer>()
                .Endpoint(e => e.Name = "seat-service-release-seats");
        });

        _provider = services.BuildServiceProvider(true);
        _harness = _provider.GetRequiredService<ITestHarness>();
        await _harness.Start();
        _sagaHarness = _harness.GetSagaStateMachineHarness<BookingSagaStateMachine, BookingSagaState>();
    }

    public async Task DisposeAsync()
    {
        await _harness.Stop();
        await _provider.DisposeAsync();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static CancellationToken Timeout(int seconds = 5)
        => new CancellationTokenSource(TimeSpan.FromSeconds(seconds)).Token;

    private BookingInitiated MakeBookingInitiated(Guid orderId) => new()
    {
        OrderId   = orderId,
        UserId    = UserId,
        SessionId = SessionId,
        SeatIds   = SeatIds
    };

    private PaymentCompleted MakePaymentCompleted(Guid orderId) => new()
    {
        OrderId   = orderId,
        UserId    = UserId,
        SessionId = SessionId,
        Seats = SeatIds.Select((id, i) => new PaymentCompletedSeat
        {
            SeatId     = id,
            SeatLabel  = $"A{i + 1}",
            LegendName = "Standard",
            Price      = 100m
        }).ToList()
    };

    // ─────────────────────────────────────────────────────────────────────────
    // 1. BookingInitiated → saga created in AwaitingPayment
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task BookingInitiated_SagaCreatedInAwaitingPayment()
    {
        var orderId = NewId.NextGuid();
        await _harness.Bus.Publish(MakeBookingInitiated(orderId));

        Assert.NotNull(
            await _sagaHarness.Exists(orderId, m => m.AwaitingPayment, TimeSpan.FromSeconds(5)));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. PaymentCompleted → MarkSeatsSoldCommand sent to SeatService
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task PaymentCompleted_SendsMarkSeatsSoldCommand()
    {
        var orderId = NewId.NextGuid();
        await _harness.Bus.Publish(MakeBookingInitiated(orderId));
        Assert.NotNull(await _sagaHarness.Exists(orderId, m => m.AwaitingPayment, TimeSpan.FromSeconds(5)));

        await _harness.Bus.Publish(MakePaymentCompleted(orderId));

        Assert.True(
            await _harness.Sent.Any<MarkSeatsSoldCommand>(
                x => x.Context.Message.OrderId == orderId, Timeout()));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. SeatsMarkedSold (via FakeMarkSeatsSoldConsumer) → IssueTicketsCommand
    //    sent to EventService. Fake consumers drive the full chain automatically.
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task SeatsMarkedSold_SendsIssueTicketsCommand()
    {
        var orderId = NewId.NextGuid();
        await _harness.Bus.Publish(MakeBookingInitiated(orderId));
        await _harness.Bus.Publish(MakePaymentCompleted(orderId));

        Assert.True(
            await _harness.Sent.Any<IssueTicketsCommand>(
                x => x.Context.Message.OrderId == orderId, Timeout(8)));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. IssueTicketsCommand carries seat items serialized from PaymentCompleted
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task IssueTicketsCommand_CarriesSeatItemsFromPaymentCompleted()
    {
        var orderId = NewId.NextGuid();
        await _harness.Bus.Publish(MakeBookingInitiated(orderId));
        await _harness.Bus.Publish(MakePaymentCompleted(orderId));

        Assert.True(
            await _harness.Sent.Any<IssueTicketsCommand>(
                x => x.Context.Message.OrderId   == orderId &&
                     x.Context.Message.SessionId  == SessionId &&
                     x.Context.Message.UserId     == UserId &&
                     x.Context.Message.Seats.Count == SeatIds.Count,
                Timeout(8)));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. ReleaseSeatsCommand carries UserId + SeatIds from BookingInitiated
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ReleaseSeatsCommand_CarriesSeatIdsFromBookingInitiated()
    {
        var orderId = NewId.NextGuid();
        await _harness.Bus.Publish(MakeBookingInitiated(orderId));
        Assert.NotNull(await _sagaHarness.Exists(orderId, m => m.AwaitingPayment, TimeSpan.FromSeconds(5)));

        await _harness.Bus.Publish(new CheckoutSessionExpired { OrderId = orderId });

        Assert.True(
            await _harness.Sent.Any<ReleaseSeatsCommand>(
                x => x.Context.Message.OrderId      == orderId &&
                     x.Context.Message.UserId        == UserId &&
                     x.Context.Message.SeatIds.Count == SeatIds.Count,
                Timeout()));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6. CheckoutSessionExpired → ReleaseSeatsCommand sent, state = ReleasingSeats
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task CheckoutSessionExpired_SendsReleaseSeatsCommand_TransitionsToReleasingSeats()
    {
        var orderId = NewId.NextGuid();
        await _harness.Bus.Publish(MakeBookingInitiated(orderId));
        Assert.NotNull(await _sagaHarness.Exists(orderId, m => m.AwaitingPayment, TimeSpan.FromSeconds(5)));

        await _harness.Bus.Publish(new CheckoutSessionExpired { OrderId = orderId });

        Assert.True(
            await _harness.Sent.Any<ReleaseSeatsCommand>(
                x => x.Context.Message.OrderId == orderId, Timeout()));

        Assert.NotNull(
            await _sagaHarness.Exists(orderId, m => m.ReleasingSeats, TimeSpan.FromSeconds(5)));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 7. Full success path: fake workers auto-reply → saga finalized
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task FullSuccessPath_SagaFinalizesAndIsRemoved()
    {
        var orderId = NewId.NextGuid();
        await _harness.Bus.Publish(MakeBookingInitiated(orderId));
        await _harness.Bus.Publish(MakePaymentCompleted(orderId));

        Assert.Null(
            await _sagaHarness.NotExists(orderId, TimeSpan.FromSeconds(10)));

        Assert.True(await _harness.Sent.Any<MarkSeatsSoldCommand>(
            x => x.Context.Message.OrderId == orderId));
        Assert.True(await _harness.Sent.Any<IssueTicketsCommand>(
            x => x.Context.Message.OrderId == orderId));
        Assert.True(await _harness.Published.Any<TicketsIssued>(
            x => x.Context.Message.OrderId == orderId));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 8. Full cancellation path: fake worker auto-replies → saga finalized
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task FullCancellationPath_SagaFinalizesAndIsRemoved()
    {
        var orderId = NewId.NextGuid();
        await _harness.Bus.Publish(MakeBookingInitiated(orderId));
        await _harness.Bus.Publish(new CheckoutSessionExpired { OrderId = orderId });

        Assert.Null(
            await _sagaHarness.NotExists(orderId, TimeSpan.FromSeconds(10)));

        Assert.True(await _harness.Sent.Any<ReleaseSeatsCommand>(
            x => x.Context.Message.OrderId == orderId));
        Assert.True(await _harness.Published.Any<SeatsReleased>(
            x => x.Context.Message.OrderId == orderId));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 9. Duplicate PaymentCompleted after saga finalized → ignored
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task DuplicatePaymentCompleted_AfterFinalization_DoesNotReprocessSeats()
    {
        var orderId = NewId.NextGuid();
        await _harness.Bus.Publish(MakeBookingInitiated(orderId));
        await _harness.Bus.Publish(MakePaymentCompleted(orderId));
        Assert.Null(await _sagaHarness.NotExists(orderId, TimeSpan.FromSeconds(10)));

        // Publish PaymentCompleted a second time — saga is already gone
        await _harness.Bus.Publish(MakePaymentCompleted(orderId));
        await Task.Delay(300);

        var markSoldCount = _harness.Sent
            .Select<MarkSeatsSoldCommand>()
            .Count(x => x.Context.Message.OrderId == orderId);

        Assert.Equal(1, markSoldCount);
    }
}

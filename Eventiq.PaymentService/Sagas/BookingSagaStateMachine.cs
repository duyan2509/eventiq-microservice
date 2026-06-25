using System.Text.Json;
using Eventiq.Contracts;
using MassTransit;

namespace Eventiq.PaymentService.Sagas;

public class BookingSagaStateMachine : MassTransitStateMachine<BookingSagaState>
{
    // ── States ────────────────────────────────────────────────────────────────
    public State AwaitingPayment { get; private set; } = null!;
    public State MarkingSeats    { get; private set; } = null!;
    public State IssuingTickets  { get; private set; } = null!;
    public State ReleasingSeats  { get; private set; } = null!;
    public State Completed       { get; private set; } = null!;
    public State Cancelled       { get; private set; } = null!;

    // ── Events ────────────────────────────────────────────────────────────────
    public Event<BookingInitiated>      BookingInitiated      { get; private set; } = null!;
    public Event<PaymentCompleted>      PaymentCompleted      { get; private set; } = null!;
    public Event<CheckoutSessionExpired> CheckoutSessionExpired { get; private set; } = null!;
    public Event<SeatsMarkedSold>       SeatsMarkedSold       { get; private set; } = null!;
    public Event<TicketsIssued>         TicketsIssued         { get; private set; } = null!;
    public Event<SeatsReleased>         SeatsReleased         { get; private set; } = null!;

    public BookingSagaStateMachine()
    {
        InstanceState(x => x.CurrentState);

        // Correlate all events by OrderId
        Event(() => BookingInitiated,       x => x.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => PaymentCompleted,       x => x.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => CheckoutSessionExpired, x => x.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => SeatsMarkedSold,        x => x.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => TicketsIssued,          x => x.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => SeatsReleased,          x => x.CorrelateById(ctx => ctx.Message.OrderId));

        // ── Initial: Checkout created ─────────────────────────────────────────
        Initially(
            When(BookingInitiated)
                .Then(ctx =>
                {
                    ctx.Saga.UserId    = ctx.Message.UserId;
                    ctx.Saga.SessionId = ctx.Message.SessionId;
                    ctx.Saga.SeatIdsJson = JsonSerializer.Serialize(ctx.Message.SeatIds);
                })
                .TransitionTo(AwaitingPayment));

        // ── AwaitingPayment: two possible outcomes ────────────────────────────
        During(AwaitingPayment,
            // Happy path: Stripe payment succeeded
            When(PaymentCompleted)
                .Then(ctx =>
                {
                    // Cache seat details so IssueTicketsCommand has all needed data
                    ctx.Saga.SeatItemsJson = JsonSerializer.Serialize(ctx.Message.Seats);
                })
                .TransitionTo(MarkingSeats)
                .Send(new Uri("queue:seat-service-mark-sold"), ctx => new MarkSeatsSoldCommand
                {
                    OrderId = ctx.Saga.CorrelationId,
                    UserId  = ctx.Saga.UserId,
                    SeatIds = JsonSerializer.Deserialize<List<Guid>>(ctx.Saga.SeatIdsJson) ?? []
                }),

            // Sad path: Stripe session expired — orchestrate seat release
            When(CheckoutSessionExpired)
                .TransitionTo(ReleasingSeats)
                .Send(new Uri("queue:seat-service-release-seats"), ctx => new ReleaseSeatsCommand
                {
                    OrderId = ctx.Saga.CorrelationId,
                    UserId  = ctx.Saga.UserId,
                    SeatIds = JsonSerializer.Deserialize<List<Guid>>(ctx.Saga.SeatIdsJson) ?? []
                }));

        // ── MarkingSeats: waiting for SeatService to confirm ─────────────────
        During(MarkingSeats,
            When(SeatsMarkedSold)
                .TransitionTo(IssuingTickets)
                .Send(new Uri("queue:event-service-issue-tickets"), ctx =>
                {
                    var seats = JsonSerializer.Deserialize<List<PaymentCompletedSeat>>(ctx.Saga.SeatItemsJson) ?? [];
                    return new IssueTicketsCommand
                    {
                        OrderId   = ctx.Saga.CorrelationId,
                        UserId    = ctx.Saga.UserId,
                        SessionId = ctx.Saga.SessionId,
                        Seats     = seats.Select(s => new IssueTicketSeat
                        {
                            SeatId     = s.SeatId,
                            SeatLabel  = s.SeatLabel,
                            LegendName = s.LegendName,
                            Price      = s.Price
                        }).ToList()
                    };
                }));

        // ── IssuingTickets: waiting for EventService to confirm ───────────────
        During(IssuingTickets,
            When(TicketsIssued)
                .TransitionTo(Completed)
                .Finalize());

        // ── ReleasingSeats: waiting for SeatService to confirm release ────────
        During(ReleasingSeats,
            When(SeatsReleased)
                .TransitionTo(Cancelled)
                .Finalize());

        // ── Guard against duplicate / late-arriving events ────────────────────
        During(Completed,
            Ignore(PaymentCompleted),
            Ignore(SeatsMarkedSold),
            Ignore(TicketsIssued));
        During(Cancelled,
            Ignore(PaymentCompleted),
            Ignore(CheckoutSessionExpired),
            Ignore(SeatsReleased));

        SetCompletedWhenFinalized();
    }
}

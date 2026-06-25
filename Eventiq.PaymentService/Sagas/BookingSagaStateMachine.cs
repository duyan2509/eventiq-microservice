using Eventiq.Contracts;
using MassTransit;

namespace Eventiq.PaymentService.Sagas;

public class BookingSagaStateMachine : MassTransitStateMachine<BookingSagaState>
{
    public State AwaitingPayment { get; private set; } = null!;
    public State Completed { get; private set; } = null!;
    public State Cancelled { get; private set; } = null!;

    public Event<BookingInitiated> BookingInitiated { get; private set; } = null!;
    public Event<PaymentCompleted> PaymentCompleted { get; private set; } = null!;
    public Event<CheckoutExpired> CheckoutExpired { get; private set; } = null!;

    public BookingSagaStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => BookingInitiated, x => x.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => PaymentCompleted, x => x.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => CheckoutExpired, x => x.CorrelateById(ctx => ctx.Message.OrderId));

        Initially(
            When(BookingInitiated)
                .Then(ctx =>
                {
                    ctx.Saga.UserId = ctx.Message.UserId;
                    ctx.Saga.SeatIdsJson = System.Text.Json.JsonSerializer.Serialize(ctx.Message.SeatIds);
                })
                .TransitionTo(AwaitingPayment));

        During(AwaitingPayment,
            When(PaymentCompleted)
                .TransitionTo(Completed)
                .Finalize(),
            When(CheckoutExpired)
                .TransitionTo(Cancelled)
                .Finalize());

        During(Completed,
            Ignore(PaymentCompleted),
            Ignore(CheckoutExpired));

        During(Cancelled,
            Ignore(PaymentCompleted),
            Ignore(CheckoutExpired));

        SetCompletedWhenFinalized();
    }
}

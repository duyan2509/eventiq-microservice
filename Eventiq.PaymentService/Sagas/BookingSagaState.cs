using MassTransit;

namespace Eventiq.PaymentService.Sagas;

public class BookingSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = null!;
    public Guid UserId { get; set; }
    public string SeatIdsJson { get; set; } = "[]";
}

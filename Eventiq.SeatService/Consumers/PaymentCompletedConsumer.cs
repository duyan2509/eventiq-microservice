using Eventiq.Contracts;
using Eventiq.SeatService.Application.Service.Interface;
using MassTransit;

namespace Eventiq.SeatService.Consumers;

public class PaymentCompletedConsumer : IConsumer<PaymentCompleted>
{
    private readonly ILogger<PaymentCompletedConsumer> _logger;
    private readonly IUnitOfWork _uow;

    public PaymentCompletedConsumer(ILogger<PaymentCompletedConsumer> logger, IUnitOfWork uow)
    {
        _logger = logger;
        _uow = uow;
    }

    public async Task Consume(ConsumeContext<PaymentCompleted> context)
    {
        var message = context.Message;
        _logger.LogInformation(
            "Processing PaymentCompleted: OrderId={OrderId}, SessionId={SessionId}, Seats={Count}",
            message.OrderId, message.SessionId, message.Seats.Count);

        try
        {
            var seatIds = message.Seats.Select(s => s.SeatId);
            var seats = await _uow.Seats.GetByIdsAsync(seatIds);

            foreach (var seat in seats)
                seat.Sell();

            await _uow.Seats.UpdateRangeAsync(seats);
            await _uow.SaveChangesAsync();

            _logger.LogInformation(
                "Marked {Count} seats as Sold for OrderId={OrderId}",
                seats.Count, message.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PaymentCompleted for OrderId={OrderId}", message.OrderId);
            throw;
        }
    }
}

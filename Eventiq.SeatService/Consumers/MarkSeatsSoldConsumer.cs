using Eventiq.Contracts;
using Eventiq.SeatService.Application.Service.Interface;
using Eventiq.SeatService.Domain.Enum;
using MassTransit;

namespace Eventiq.SeatService.Consumers;

public class MarkSeatsSoldConsumer : IConsumer<MarkSeatsSoldCommand>
{
    private readonly IUnitOfWork _uow;
    private readonly ISeatStatusBroadcaster _broadcaster;
    private readonly ILogger<MarkSeatsSoldConsumer> _logger;

    public MarkSeatsSoldConsumer(IUnitOfWork uow, ISeatStatusBroadcaster broadcaster, ILogger<MarkSeatsSoldConsumer> logger)
    {
        _uow = uow;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<MarkSeatsSoldCommand> context)
    {
        var msg = context.Message;
        _logger.LogInformation("MarkSeatsSold: OrderId={OrderId}, Seats={Count}", msg.OrderId, msg.SeatIds.Count);

        var seats = await _uow.Seats.GetByIdsAsync(msg.SeatIds);

        foreach (var seat in seats)
        {
            if (seat.Status == SeatStatus.Sold) continue; // idempotent
            seat.Sell();
        }

        await _uow.Seats.UpdateRangeAsync(seats);
        await _uow.SaveChangesAsync();

        var bySeatMap = seats.GroupBy(s => s.SeatMapId);
        foreach (var group in bySeatMap)
        {
            var updates = group.Select(s => new SeatStatusUpdate(s.Id, "Sold")).ToList();
            await _broadcaster.BroadcastSeatStatusAsync(group.Key, updates);
        }

        await context.Publish(new SeatsMarkedSold { OrderId = msg.OrderId });
        _logger.LogInformation("SeatsMarkedSold published for OrderId={OrderId}", msg.OrderId);
    }
}

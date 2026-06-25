using Eventiq.Contracts;
using Eventiq.SeatService.Application.Service.Interface;
using Eventiq.SeatService.Domain.Enum;
using MassTransit;

namespace Eventiq.SeatService.Consumers;

public class ReleaseSeatsConsumer : IConsumer<ReleaseSeatsCommand>
{
    private readonly IUnitOfWork _uow;
    private readonly ISeatStatusBroadcaster _broadcaster;
    private readonly ILogger<ReleaseSeatsConsumer> _logger;

    public ReleaseSeatsConsumer(IUnitOfWork uow, ISeatStatusBroadcaster broadcaster, ILogger<ReleaseSeatsConsumer> logger)
    {
        _uow = uow;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ReleaseSeatsCommand> context)
    {
        var msg = context.Message;
        _logger.LogInformation("ReleaseSeats: OrderId={OrderId}, Seats={Count}", msg.OrderId, msg.SeatIds.Count);

        var seats = await _uow.Seats.GetByIdsAsync(msg.SeatIds);
        var toRelease = seats
            .Where(s => s.Status == SeatStatus.Holding && s.HeldBy == msg.UserId)
            .ToList();

        foreach (var seat in toRelease)
            seat.Release();

        if (toRelease.Count > 0)
        {
            await _uow.Seats.UpdateRangeAsync(toRelease);
            await _uow.SaveChangesAsync();

            var bySeatMap = toRelease.GroupBy(s => s.SeatMapId);
            foreach (var group in bySeatMap)
            {
                var updates = group.Select(s => new SeatStatusUpdate(s.Id, "Available")).ToList();
                await _broadcaster.BroadcastSeatStatusAsync(group.Key, updates);
            }
        }

        // Always publish SeatsReleased so saga can finalize (idempotent)
        await context.Publish(new SeatsReleased { OrderId = msg.OrderId });
        _logger.LogInformation("SeatsReleased published for OrderId={OrderId}", msg.OrderId);
    }
}

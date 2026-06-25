using Eventiq.Contracts;
using Eventiq.SeatService.Application.Service.Interface;
using Eventiq.SeatService.Domain.Enum;
using MassTransit;

namespace Eventiq.SeatService.Consumers;

public class CheckoutExpiredConsumer : IConsumer<CheckoutExpired>
{
    private readonly IUnitOfWork _uow;
    private readonly ISeatStatusBroadcaster _broadcaster;
    private readonly ILogger<CheckoutExpiredConsumer> _logger;

    public CheckoutExpiredConsumer(IUnitOfWork uow, ISeatStatusBroadcaster broadcaster, ILogger<CheckoutExpiredConsumer> logger)
    {
        _uow = uow;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CheckoutExpired> context)
    {
        var msg = context.Message;
        if (msg.SeatIds.Count == 0) return;

        _logger.LogInformation("Processing CheckoutExpired: OrderId={OrderId}, Seats={Count}", msg.OrderId, msg.SeatIds.Count);

        var seats = await _uow.Seats.GetByIdsAsync(msg.SeatIds);
        var toRelease = seats
            .Where(s => s.Status == SeatStatus.Holding && s.HeldBy == msg.UserId)
            .ToList();

        if (toRelease.Count == 0) return;

        foreach (var seat in toRelease)
            seat.Release();

        await _uow.Seats.UpdateRangeAsync(toRelease);
        await _uow.SaveChangesAsync();

        var bySeatMap = toRelease.GroupBy(s => s.SeatMapId);
        foreach (var group in bySeatMap)
        {
            var updates = group.Select(s => new SeatStatusUpdate(s.Id, "Available")).ToList();
            await _broadcaster.BroadcastSeatStatusAsync(group.Key, updates);
        }

        _logger.LogInformation("Released {Count} seats for expired checkout OrderId={OrderId}", toRelease.Count, msg.OrderId);
    }
}

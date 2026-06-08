using Eventiq.Contracts;
using Eventiq.PaymentService.Application.Service.Interface;
using Eventiq.PaymentService.Domain.Entity;
using Eventiq.PaymentService.Domain.Enums;
using Eventiq.PaymentService.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Eventiq.PaymentService.Application.Service.Implement;

public class OrderSettlementService : IOrderSettlementService
{
    private readonly PaymentDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<OrderSettlementService> _logger;

    public OrderSettlementService(
        PaymentDbContext dbContext,
        IPublishEndpoint publishEndpoint,
        ILogger<OrderSettlementService> logger)
    {
        _dbContext = dbContext;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task<bool> SettlePaidAsync(Order order, SettlementSource source)
    {
        // Idempotent: only the first transition out of Pending wins. The webhook and the
        // reconciliation job can both observe the same paid session, so the guard is essential.
        if (order.Status != OrderStatus.Pending)
        {
            _logger.LogInformation("Order {OrderId} already {Status}; skipping settle.", order.Id, order.Status);
            return false;
        }

        order.Status = OrderStatus.Paid;
        order.PaidAt = DateTime.UtcNow;
        order.SettledBy = source;

        await _publishEndpoint.Publish(new PaymentCompleted
        {
            OrderId = order.Id,
            UserId = order.UserId,
            SessionId = order.SessionId,
            Seats = order.Items.Select(i => new PaymentCompletedSeat
            {
                SeatId = i.SeatId,
                SeatLabel = i.SeatLabel,
                LegendName = i.LegendName,
                Price = i.Price
            }).ToList()
        });

        try
        {
            // Commits order update + outbox message atomically.
            await _dbContext.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another transaction (webhook vs reconciliation) settled this order between our
            // read and write. The concurrency token (xmin) caught it — safe to skip.
            _logger.LogInformation("Order {OrderId} settled concurrently; skipping.", order.Id);
            return false;
        }
    }
}

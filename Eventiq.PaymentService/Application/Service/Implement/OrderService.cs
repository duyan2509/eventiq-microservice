using Eventiq.Contracts.Grpc;
using Eventiq.PaymentService.Application.Dto;
using Eventiq.PaymentService.Application.Service.Interface;
using Eventiq.PaymentService.Domain.Entity;
using Eventiq.PaymentService.Domain.Enums;
using Eventiq.PaymentService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Eventiq.PaymentService.Application.Service.Implement;

public class OrderService : IOrderService
{
    private readonly PaymentDbContext _dbContext;
    private readonly EventInternal.EventInternalClient _eventClient;

    public OrderService(PaymentDbContext dbContext, EventInternal.EventInternalClient eventClient)
    {
        _dbContext = dbContext;
        _eventClient = eventClient;
    }

    public async Task<List<Order>> GetMyOrdersAsync(Guid userId)
    {
        return await _dbContext.Orders
            .Include(o => o.Items)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Order>> GetAllOrdersAsync(int page, int size)
    {
        return await _dbContext.Orders
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();
    }

    public async Task<List<TicketDetailDto>> GetTicketsByOrderAsync(Guid orderId, Guid userId)
    {
        var order = await _dbContext.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId && o.Status == OrderStatus.Paid);

        if (order == null)
            throw new NotFoundException("Order not found or not yet paid");

        var response = await _eventClient.GetTicketsByOrderAsync(
            new GetTicketsByOrderRequest { OrderId = orderId.ToString() });

        return response.Tickets.Select(t => new TicketDetailDto(
            t.TicketId,
            t.SeatLabel,
            t.LegendName,
            (decimal)t.Price,
            t.QrCode,
            t.IsCheckedIn,
            DateTime.Parse(t.IssuedAt),
            string.IsNullOrEmpty(t.CheckedInAt) ? null : DateTime.Parse(t.CheckedInAt)
        )).ToList();
    }
}

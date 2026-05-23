using Eventiq.PaymentService.Application.Service.Interface;
using Eventiq.PaymentService.Domain.Entity;

namespace Eventiq.PaymentService.Application.Service.Implement;

public class OrderService : IOrderService
{
    public Task<List<Order>> GetMyOrdersAsync(Guid userId)
    {
        throw new NotImplementedException();
    }
}

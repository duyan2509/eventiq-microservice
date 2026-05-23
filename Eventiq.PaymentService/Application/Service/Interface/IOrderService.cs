using Eventiq.PaymentService.Domain.Entity;

namespace Eventiq.PaymentService.Application.Service.Interface;

public interface IOrderService
{
    Task<List<Order>> GetMyOrdersAsync(Guid userId);
}

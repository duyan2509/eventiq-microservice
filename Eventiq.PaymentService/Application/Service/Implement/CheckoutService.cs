using Eventiq.PaymentService.Application.Service.Interface;

namespace Eventiq.PaymentService.Application.Service.Implement;

public class CheckoutService : ICheckoutService
{
    public Task<string> CreateAsync(Guid userId, Guid sessionId, List<Guid> seatIds)
    {
        throw new NotImplementedException();
    }
}

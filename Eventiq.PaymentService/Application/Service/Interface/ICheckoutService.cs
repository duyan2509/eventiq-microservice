namespace Eventiq.PaymentService.Application.Service.Interface;

public interface ICheckoutService
{
    Task<string> CreateAsync(Guid userId, Guid sessionId, List<Guid> seatIds);
}

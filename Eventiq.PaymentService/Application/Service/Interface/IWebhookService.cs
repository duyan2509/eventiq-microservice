namespace Eventiq.PaymentService.Application.Service.Interface;

public interface IWebhookService
{
    Task HandleAsync(string payload, string stripeSignature);
}

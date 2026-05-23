using Eventiq.PaymentService.Application.Service.Interface;

namespace Eventiq.PaymentService.Application.Service.Implement;

public class WebhookService : IWebhookService
{
    public Task HandleAsync(string payload, string stripeSignature)
    {
        throw new NotImplementedException();
    }
}

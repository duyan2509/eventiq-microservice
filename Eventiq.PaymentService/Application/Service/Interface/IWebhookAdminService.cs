using Eventiq.PaymentService.Domain.Entity;
using Eventiq.PaymentService.Domain.Enums;

namespace Eventiq.PaymentService.Application.Service.Interface;

public interface IWebhookAdminService
{
    Task<List<WebhookEvent>> GetAsync(WebhookEventStatus? status, int page, int size);
    Task<WebhookEvent?> GetByIdAsync(Guid id);
}

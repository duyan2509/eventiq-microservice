using Eventiq.PaymentService.Application.Service.Interface;
using Eventiq.PaymentService.Domain.Entity;
using Eventiq.PaymentService.Domain.Enums;
using Eventiq.PaymentService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Eventiq.PaymentService.Application.Service.Implement;

public class WebhookAdminService : IWebhookAdminService
{
    private readonly PaymentDbContext _dbContext;

    public WebhookAdminService(PaymentDbContext dbContext) => _dbContext = dbContext;

    public async Task<List<WebhookEvent>> GetAsync(WebhookEventStatus? status, int page, int size)
    {
        if (page < 1) page = 1;
        if (size < 1 || size > 100) size = 20;

        var query = _dbContext.WebhookEvents.AsNoTracking();
        if (status.HasValue)
            query = query.Where(w => w.Status == status.Value);

        return await query
            .OrderByDescending(w => w.ReceivedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();
    }

    public Task<WebhookEvent?> GetByIdAsync(Guid id) =>
        _dbContext.WebhookEvents.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id);
}

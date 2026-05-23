using Eventiq.OrganizationService.Domain.Entity;
using Eventiq.OrganizationService.Domain.Repositories;

namespace Eventiq.OrganizationService.Infrastructure.Persistence;

public class PayoutLogRepository : IPayoutLogRepository
{
    private readonly EvtOrganizationDbContext _context;

    public PayoutLogRepository(EvtOrganizationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PayoutLog log, CancellationToken ct = default)
    {
        await _context.PayoutLogs.AddAsync(log, ct);
    }
}

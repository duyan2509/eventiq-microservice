using Eventiq.OrganizationService.Domain.Entity;

namespace Eventiq.OrganizationService.Domain.Repositories;

public interface IPayoutLogRepository
{
    Task AddAsync(PayoutLog log, CancellationToken ct = default);
}

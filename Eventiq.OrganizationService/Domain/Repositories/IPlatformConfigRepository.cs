using Eventiq.OrganizationService.Domain.Entity;

namespace Eventiq.OrganizationService.Domain.Repositories;

public interface IPlatformConfigRepository
{
    Task<PlatformConfig> GetAsync(CancellationToken ct = default);
    Task UpdateAsync(PlatformConfig config, CancellationToken ct = default);
}

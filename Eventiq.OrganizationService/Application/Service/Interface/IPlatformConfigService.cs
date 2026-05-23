using Eventiq.OrganizationService.Dtos;

namespace Eventiq.OrganizationService.Application.Service;

public interface IPlatformConfigService
{
    Task<PlatformConfigResponse> GetAsync(CancellationToken ct = default);
    Task<PlatformConfigResponse> UpdateAsync(Guid adminId, UpdatePlatformConfigRequest request, CancellationToken ct = default);
    Task<InternalPlatformConfigResponse> GetInternalAsync(CancellationToken ct = default);
    Task PromotePendingIfDueAsync(CancellationToken ct = default);
}

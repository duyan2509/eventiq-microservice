using Eventiq.OrganizationService.Domain.Entity;
using Eventiq.OrganizationService.Dtos;

namespace Eventiq.OrganizationService.Domain.Repositories;

public interface IPermissionRepository
{
    Task AddAsync(Permission? permission, CancellationToken cancellationToken = default);
    Task<PaginatedResult<PermissionResponse>> GetByOrgIdUserId(Guid userId, Guid orgId, int page, int size,CancellationToken cancellationToken = default);
    Task DeleteAsync(Permission? permission, CancellationToken cancellationToken = default);
    Task<Permission?> GetByIdAsync(Guid permissionId,CancellationToken cancellationToken = default);
    Task UpdateAsync(Permission? permission, CancellationToken cancellationToken = default);
}
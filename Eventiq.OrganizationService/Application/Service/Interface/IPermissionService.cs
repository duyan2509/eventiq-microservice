using Eventiq.OrganizationService.Dtos;

namespace Eventiq.OrganizationService.Application.Service;

public interface IPermissionService
{
    Task<PaginatedResult<PermissionResponse>> GetPermissionsAsync(Guid userId, Guid orgId, int page, int size, CancellationToken cancellationToken = default);
    Task<PermissionResponse> AddPermissionAsync(Guid userId, Guid orgId, PermissionDto dto , CancellationToken cancellationToken = default);
    Task<PermissionResponse> UpdatePermissionAsync(Guid userId, Guid orgId, Guid permissionId, UpdatePermissionDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeletePermissionAsync(Guid userId, Guid orgId, Guid permissionId, CancellationToken cancellationToken = default);

}


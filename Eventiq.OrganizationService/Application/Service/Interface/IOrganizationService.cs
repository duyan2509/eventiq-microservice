using Eventiq.OrganizationService.Domain.Entity;
using Eventiq.OrganizationService.Dtos;

namespace Eventiq.OrganizationService.Application.Service;

public interface IOrganizationService
{
    Task<OrganizationDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PaginatedResult<OrganizationDetail>> GetAllAsync(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);
    Task<PaginatedResult<OrganizationDetail>> GetMyOrganizationsAsync(Guid userId, int page = 1, int pageSize = 10,  CancellationToken cancellationToken = default);
    Task<OrganizationResponse> AddAsync(Guid userId, string email, OrganizationDto dto, CancellationToken cancellationToken = default);
    Task<OrganizationResponse> UpdateAsync(Guid userId, Guid orgId, UpdateOrganizationDto dto, CancellationToken cancellationToken = default);
}


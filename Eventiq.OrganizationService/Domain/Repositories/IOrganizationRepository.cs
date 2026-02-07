using Eventiq.OrganizationService.Domain.Entity;
using Eventiq.OrganizationService.Dtos;

namespace Eventiq.OrganizationService.Domain.Repositories;

public interface IOrganizationRepository
{
    Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PaginatedResult<OrganizationDetail>> GetAllAsync(int page, int size, CancellationToken cancellationToken = default);
    Task<PaginatedResult<OrganizationDetail>> GetAllMyOrgAsync(Guid userId, int page, int size, CancellationToken cancellationToken = default);

    Task AddAsync(Organization organization, CancellationToken cancellationToken = default);
    Task UpdateAsync(Organization organization, CancellationToken cancellationToken = default);
}

using Eventiq.OrganizationService.Domain.Entity;

namespace Eventiq.OrganizationService.Domain.Repositories;

public interface IOrganizationRepository
{
    Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Organization>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Organization organization, CancellationToken cancellationToken = default);
    Task UpdateAsync(Organization organization, CancellationToken cancellationToken = default);
}

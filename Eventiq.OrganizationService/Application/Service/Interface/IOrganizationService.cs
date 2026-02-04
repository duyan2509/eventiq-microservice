using Eventiq.OrganizationService.Domain.Entity;

namespace Eventiq.OrganizationService.Application.Service;

public interface IOrganizationService
{
    Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Organization>> GetAllAsync(CancellationToken cancellationToken = default);
}

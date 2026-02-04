using Eventiq.OrganizationService.Domain.Entity;
using Eventiq.OrganizationService.Domain.Repositories;

namespace Eventiq.OrganizationService.Application.Service;

public class OrganizationService : IOrganizationService
{
    private readonly IOrganizationRepository _organizationRepository;

    public OrganizationService(IOrganizationRepository organizationRepository)
    {
        _organizationRepository = organizationRepository;
    }

    public Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _organizationRepository.GetByIdAsync(id, cancellationToken);
    }

    public Task<IReadOnlyList<Organization>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _organizationRepository.GetAllAsync(cancellationToken);
    }
}

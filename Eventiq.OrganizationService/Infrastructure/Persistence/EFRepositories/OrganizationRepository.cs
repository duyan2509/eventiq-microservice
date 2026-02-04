using Eventiq.OrganizationService.Domain.Entity;
using Eventiq.OrganizationService.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Eventiq.OrganizationService.Infrastructure.Persistence;

public class OrganizationRepository : IOrganizationRepository
{
    private readonly EvtOrganizationDbContext _context;
    private readonly DbSet<Organization> _organizations;

    public OrganizationRepository(EvtOrganizationDbContext context)
    {
        _context = context;
        _organizations = context.Set<Organization>();
    }

    public async Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Organization>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _organizations
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        _organizations.Add(organization);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        _organizations.Update(organization);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

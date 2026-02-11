using Eventiq.OrganizationService.Domain;

namespace Eventiq.OrganizationService.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly EvtOrganizationDbContext _context;

    public UnitOfWork(EvtOrganizationDbContext context)
    {
        _context = context;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}

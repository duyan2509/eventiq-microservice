using Eventiq.OrganizationService.Domain.Entity;
using Eventiq.OrganizationService.Domain.Repositories;
using Eventiq.OrganizationService.Dtos;
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

    public async Task<PaginatedResult<OrganizationDetail>> GetAllAsync(int page =1, int size =10, CancellationToken cancellationToken = default)
    {
        var query =  _organizations.AsNoTracking()
            .Select(o => new OrganizationDetail
            {
                Name = o.Name,
                Description = o.Description,
                Size = o.Members.Count,
                Id = o.Id,
            });
        int count = query.Count();
        var data = new List<OrganizationDetail>();
        if((page-1)*size<count)
            data = await query.Skip((page-1)*size).Take(count).ToListAsync(cancellationToken);
        return new PaginatedResult<OrganizationDetail>()
        {
            Data = data,
            Page = page,
            Size = size,
            Total = count
        };
    }

    public async Task<PaginatedResult<OrganizationDetail>> GetAllMyOrgAsync(Guid userId, int page, int size, CancellationToken cancellationToken = default)
    {
        var query =  _organizations.AsNoTracking()
            .Select(o => new OrganizationDetail
            {
                Name = o.Name,
                Description = o.Description,
                Size = o.Members.Count,
                Id = o.Id,
                isOwner = userId.Equals(o.OwnerId),
            });
        int count = query.Count();
        var data = new List<OrganizationDetail>();
        if((page-1)*size<count)
            data = await query.Skip((page-1)*size).Take(count).ToListAsync(cancellationToken);
        return new PaginatedResult<OrganizationDetail>()
        {
            Data = data,
            Page = page,
            Size = size,
            Total = count
        };
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

using AutoMapper;
using Eventiq.OrganizationService.Domain.Entity;
using Eventiq.OrganizationService.Domain.Repositories;
using Eventiq.OrganizationService.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Eventiq.OrganizationService.Infrastructure.Persistence;

public class PermissionRepository:IPermissionRepository
{
    private readonly EvtOrganizationDbContext _context;
    private readonly DbSet<Permission?> _permissions;
    private readonly IMapper _mapper;

    public PermissionRepository(EvtOrganizationDbContext context, IMapper mapper)
    {
        _context = context;
        _permissions = _context.Permissions;
        _mapper = mapper;
    }

    public Task AddAsync(Permission? permission, CancellationToken cancellationToken = default)
    {
        return _permissions.AddAsync(permission, cancellationToken).AsTask();
    }

    public async Task<PaginatedResult<PermissionResponse>> GetByOrgIdUserId(Guid userId, Guid orgId, int page, int size, CancellationToken cancellationToken = default)
    {
        var query = _permissions
            .AsNoTracking()
            .Where(p=>p.OrganizationId==orgId &&p.Organization.OwnerId==userId);
        int total = await query.CountAsync(cancellationToken);
        var data = new List<PermissionResponse>();
        if((page-1)*size<total)
            data = await query.Skip((page-1)*size).Take(size)
                .Select(p=>_mapper.Map<PermissionResponse>(p))
                .ToListAsync(cancellationToken);
        return new PaginatedResult<PermissionResponse>()
        {
            Data = data,
            Total = total,
            Page = page,
            Size = size,

        };
    }

    public Task DeleteAsync(Permission? permission, CancellationToken cancellationToken = default)
    {
        _permissions.Remove(permission);
        return Task.CompletedTask;
    }

    public async Task<Permission?> GetByIdAsync(Guid permissionId, CancellationToken cancellationToken = default)
    {
        return await _permissions.Where(p=>p.Id == permissionId).FirstOrDefaultAsync(cancellationToken);
    }

    public Task UpdateAsync(Permission? permission, CancellationToken cancellationToken = default)
    {
        _permissions.Update(permission);
        return Task.CompletedTask;
    }
}
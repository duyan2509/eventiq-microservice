using AutoMapper;
using Eventiq.OrganizationService.Domain.Entity;
using Eventiq.OrganizationService.Domain.Repositories;
using Eventiq.OrganizationService.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Eventiq.OrganizationService.Infrastructure.Persistence;

public class MemberRepository:IMemberRepository
{
    private readonly EvtOrganizationDbContext _context;
    private readonly DbSet<Member?> _members;
    private readonly IMapper _mapper;

    public MemberRepository(EvtOrganizationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
        _members = _context.Members;
    }

    public Task AddAsync(Member? member, CancellationToken cancellationToken = default)
    {
        return _members.AddAsync(member, cancellationToken).AsTask();
    }

    public Task UpdateAsync(Member? member, CancellationToken cancellationToken = default)
    {
        _members.Update(member);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Member? member, CancellationToken cancellationToken = default)
    {
        _members.Remove(member);
        return Task.CompletedTask;
    }

    public async Task<Member?> GetAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        return _members
            .Include(m => m.Permission)
            .Where(m => m.Id == memberId)
            .FirstOrDefault();
    }

    public async Task<PaginatedResult<MemberReponse>> GetOrgMembersAsync(Guid orgId, int page = 1, int size = 10, CancellationToken cancellationToken = default)
    {
        var query = _members.AsNoTracking()
            .Where(m => m.OrganizationId == orgId);
        int total = await query.CountAsync(cancellationToken);
        var data = new List<MemberReponse>();
        if(size*(page-1)<total)
        {
            var members = await query
                .Include(m => m.Permission)
                .Skip((page-1) * size)
                .Take(size)
                .ToListAsync(cancellationToken);
            data = members.Select(m=>_mapper.Map<MemberReponse>(m)).ToList();
        }
        return new PaginatedResult<MemberReponse>()
        {
            Data = data,
            Total = total,
            Size = size,
            Page = page
        };
    }

    public async Task<bool> AnyByPermissionIdAsync(Guid permissionId, CancellationToken cancellationToken = default)
    {
        return await _members.AnyAsync(m => m.PermissionId == permissionId, cancellationToken);
    }

    public async Task<List<Guid>> GetUserIdsByPermissionIdAsync(Guid permissionId, CancellationToken cancellationToken = default)
    {
        return await _members
            .Where(m => m.PermissionId == permissionId && m.UserId.HasValue)
            .Select(m => m.UserId!.Value)
            .ToListAsync(cancellationToken);
    }

    public async Task<MemberReponse?> GetByUserIdAndOrgIdAsync(Guid userId, Guid orgId, CancellationToken cancellationToken = default)
    {
        var member = await _members.AsNoTracking()
            .Include(m => m.Permission)
            .FirstOrDefaultAsync(m => m.UserId == userId && m.OrganizationId == orgId, cancellationToken);
        return member == null ? null : _mapper.Map<MemberReponse>(member);
    }

    public async Task<List<UserOrganizationItem>> GetOrgsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _members.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => new UserOrganizationItem
            {
                OrgId = m.OrganizationId,
                OrgName = m.Organization.Name,
                RoleName = m.Permission.Name
            })
            .ToListAsync(cancellationToken);
    }
}
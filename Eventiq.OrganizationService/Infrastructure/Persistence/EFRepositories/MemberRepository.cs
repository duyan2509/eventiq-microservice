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
            data = await query
                .Skip((page-1) * size)
                .Take(size)
                .Select(m=>_mapper.Map<MemberReponse>(m))
                .ToListAsync();
        return new PaginatedResult<MemberReponse>()
        {
            Data = data,
            Total = total,
            Size = size,
            Page = page
        };
    }
}
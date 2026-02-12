using AutoMapper;
using Eventiq.OrganizationService.Domain.Entity;
using Eventiq.OrganizationService.Domain.Repositories;
using Eventiq.OrganizationService.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Eventiq.OrganizationService.Infrastructure.Persistence;

public class InvitationRepository:IInvitationRepository
{
    private readonly EvtOrganizationDbContext _context;
    private readonly IMapper _mapper;
    private readonly DbSet<Invitation?> _invitations;

    public InvitationRepository(EvtOrganizationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
        _invitations = _context.Invitations;
    }

    public async Task<PaginatedResult<InviationResponse>> GetOrgInvitationsAsync(Guid orgId, int page, int size, CancellationToken cancellationToken = default)
    {
        var query = _invitations.AsNoTracking()
            .Where(i => i.OrganizationId == orgId);
        int total = await query.CountAsync(cancellationToken);
        var data = new List<InviationResponse>();
        if(size*(page-1)<total)
            data = await query.Skip((page-1)*size).Take(size)
                .Select(i=>_mapper.Map<InviationResponse>(i)).ToListAsync(cancellationToken);

        return new PaginatedResult<InviationResponse>()
        {
            Data = data,
            Total = total,
            Size = size,
            Page = page,
        };
    }

    public async Task<PaginatedResult<InviationResponse>> GetUserInvitationsAsync(Guid userId, int page, int size, CancellationToken cancellationToken = default)
    {
        var query = _invitations.AsNoTracking()
            .Where(i => i.UserId == userId);
        int total = await query.CountAsync(cancellationToken);
        var data = new List<InviationResponse>();
        if(size*(page-1)<total)
            data = await query.Skip((page-1)*size).Take(size)
                .Select(i=>_mapper.Map<InviationResponse>(i)).ToListAsync(cancellationToken);

        return new PaginatedResult<InviationResponse>()
        {
            Data = data,
            Total = total,
            Size = size,
            Page = page,
        };
    }

    public async Task<Invitation?> GetInvitatioByIDAsync(Guid invitationId, CancellationToken cancellationToken = default)
    {
        return await  _invitations.SingleOrDefaultAsync(i => i.Id == invitationId, cancellationToken);
    }

    public Task UpdateAsync(Invitation? invitation, CancellationToken cancellationToken = default)
    {
        _invitations.Update(invitation);
        return Task.CompletedTask;
    }
    public Task AddAsync(Invitation? invitation, CancellationToken cancellationToken = default)
    {
        return _invitations.AddAsync(invitation, cancellationToken).AsTask();
    }

    public async Task<Invitation?> GetInvitationByEmailAndOrgId(string userEmail, Guid orgId, CancellationToken cancellationToken)
    {
        return await _invitations.AsNoTracking().Where(i=>i.UserEmail == userEmail && i.OrganizationId == orgId).FirstOrDefaultAsync(cancellationToken);
    }
}
using AutoMapper;
using Eventiq.OrganizationService.Domain.Entity;
using Eventiq.OrganizationService.Domain.Enum;
using Eventiq.OrganizationService.Domain.Repositories;
using Eventiq.OrganizationService.Dtos;
using Eventiq.OrganizationService.Guards;

namespace Eventiq.OrganizationService.Application.Service;

public class InvitationService : IInvitationService
{
    private readonly IMapper _mapper;
    private readonly IInvitationRepository _invitationRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IMemberRepository _memberRepository;

    public InvitationService(IMapper mapper, IInvitationRepository invitationRepository, IOrganizationRepository organizationRepository, IMemberRepository memberRepository)
    {
        _mapper = mapper;
        _invitationRepository = invitationRepository;
        _organizationRepository = organizationRepository;
        _memberRepository = memberRepository;
    }


    public Task<InviationResponse> AddInvitationAsync(string userEmail, Guid orgId, InvitationDto dto, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public async  Task<PaginatedResult<InviationResponse>> GetOrgInvitationsAsync(Guid ownerId, Guid orgId, int page=1, int size =10, CancellationToken cancellationToken = default)
    {
        var org = await _organizationRepository.GetByIdAsync(orgId, cancellationToken);
        OrgGuards.EnsureExists(org);
        OwnerGuards.EnsureOwner(org, ownerId);
        return await _invitationRepository.GetOrgInvitationsAsync(orgId, page, size, cancellationToken);
    }

    public async Task<PaginatedResult<InviationResponse>> GetUserInvitationsAsync(Guid userId, int page=1, int size =10, CancellationToken cancellationToken = default)
    {
        return await _invitationRepository.GetUserInvitationsAsync(userId, page, size, cancellationToken);
    }


    public async Task<InviationResponse> AcceptInvitationAsync(Guid userId, Guid orgId, Guid invitationId, CancellationToken cancellationToken = default)
    {
        var invitation = await _invitationRepository.GetInvitatioByIDAsync(invitationId, cancellationToken);
        InvitationGuards.EnsureExist(invitation);
        InvitationGuards.EnsureCanResponse(invitation);
        invitation.Status = InvitationStatus.ACCEPTED;
        invitation.UserId =  userId;
        await _invitationRepository.UpdateAsync(invitation, cancellationToken);
        await _memberRepository.AddAsync(new Member()
        {
            UserId = userId,
            Email = invitation.UserEmail,
            OrganizationId = orgId,
            PermissionId = invitation.PermissionId
        });
        // send message

        return _mapper.Map<InviationResponse>(invitation);
    }

    public async Task<InviationResponse> RejectInvitationAsync(Guid userId, Guid orgId, Guid invitationId, CancellationToken cancellationToken = default)
    {
        var invitation = await _invitationRepository.GetInvitatioByIDAsync(invitationId, cancellationToken);
        InvitationGuards.EnsureExist(invitation);
        InvitationGuards.EnsureCanResponse(invitation);
        invitation.Status = InvitationStatus.REJECTED;
        invitation.UserId =  userId;
        await _invitationRepository.UpdateAsync(invitation, cancellationToken);
        return _mapper.Map<InviationResponse>(invitation);
    }

    public async Task<InviationResponse> CancelInvitationAsync(Guid userId, Guid orgId, Guid invitationId, CancellationToken cancellationToken = default)
    {
        var org = await _organizationRepository.GetByIdAsync(orgId, cancellationToken);
        OrgGuards.EnsureExists(org);
        OwnerGuards.EnsureOwner(org, userId);
        var invitation = await _invitationRepository.GetInvitatioByIDAsync(invitationId, cancellationToken);
        InvitationGuards.EnsureExist(invitation);
        InvitationGuards.EnsureCanResponse(invitation);
        invitation.Status = InvitationStatus.CANCELED;
        await  _invitationRepository.UpdateAsync(invitation, cancellationToken);
        return _mapper.Map<InviationResponse>(invitation);
    }
}
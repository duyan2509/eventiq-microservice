using AutoMapper;
using Eventiq.Contracts;
using Eventiq.OrganizationService.Domain;
using Eventiq.OrganizationService.Domain.Entity;
using Eventiq.OrganizationService.Domain.Enum;
using Eventiq.OrganizationService.Domain.Repositories;
using Eventiq.OrganizationService.Dtos;
using Eventiq.OrganizationService.Guards;
using MassTransit;

namespace Eventiq.OrganizationService.Application.Service;

public class InvitationService : IInvitationService
{
    private readonly IMapper _mapper;
    private readonly IInvitationRepository _invitationRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IMemberRepository _memberRepository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IUnitOfWork _unitOfWork;

    public InvitationService(IMapper mapper,
        IInvitationRepository invitationRepository,
        IOrganizationRepository organizationRepository,
        IMemberRepository memberRepository,
        IPublishEndpoint publishEndpoint,
        IUnitOfWork unitOfWork)
    {
        _mapper = mapper;
        _invitationRepository = invitationRepository;
        _organizationRepository = organizationRepository;
        _memberRepository = memberRepository;
        _publishEndpoint = publishEndpoint;
        _unitOfWork = unitOfWork;
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
        InvitationGuards.EnsureOrgInvitation(invitation, orgId);
        invitation.Status = InvitationStatus.ACCEPTED;
        invitation.UserId =  userId;
        await _invitationRepository.UpdateAsync(invitation, cancellationToken);
        await _memberRepository.AddAsync(new Member()
        {
            UserId = userId,
            Email = invitation.UserEmail,
            OrganizationId = orgId,
            PermissionId = invitation.PermissionId
        }, cancellationToken);
        // send message
        await _publishEndpoint.Publish(new StaffAccepted
        {
            UserId = userId,
            OrganizationId = orgId,
        });
        await _unitOfWork.SaveChangesAsync(cancellationToken);
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
        await _unitOfWork.SaveChangesAsync(cancellationToken);
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
        await _invitationRepository.UpdateAsync(invitation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<InviationResponse>(invitation);
    }
}
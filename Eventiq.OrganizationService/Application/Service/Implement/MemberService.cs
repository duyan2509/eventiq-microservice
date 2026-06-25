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

public class MemberService : IMemberService
{
    private readonly IMemberRepository _memberRepository;
    private readonly ILogger<MemberService> _logger;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IMapper _mapper;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IInvitationRepository _invitationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public MemberService(IMemberRepository memberRepository,
        ILogger<MemberService> logger,
        IPermissionRepository permissionRepository,
        IMapper mapper,
        IOrganizationRepository organizationRepository,
        IPublishEndpoint publishEndpoint,
        IInvitationRepository invitationRepository,
        IUnitOfWork unitOfWork)
    {
        _memberRepository = memberRepository;
        _invitationRepository = invitationRepository;
        _logger = logger;
        _permissionRepository = permissionRepository;
        _mapper = mapper;
        _organizationRepository = organizationRepository;
        _publishEndpoint = publishEndpoint;
        _unitOfWork = unitOfWork;
    }


    public async  Task<PaginatedResult<MemberReponse>> GetMembersAsync(Guid orgId, int page = 1, int size = 10,
        CancellationToken cancellationToken = default)
    {
        return await _memberRepository.GetOrgMembersAsync(orgId, page, size, cancellationToken);
    }

    public async Task<MemberReponse> ChangeMemberPermissionsAsync(Guid ownerId, Guid memberId, Guid orgId, ChangePermission dto,
        CancellationToken cancellationToken = default)
    {
        var org = await _organizationRepository.GetByIdAsync(orgId, cancellationToken);
        OrgGuards.EnsureExists(org);
        OwnerGuards.EnsureOwner(org,ownerId);
        var member = await _memberRepository.GetAsync(memberId, cancellationToken);
        MemberGuards.EnsureExists(member);
        MemberGuards.EnsureNotOwner(member);
        var permission = await  _permissionRepository.GetByIdAsync(dto.PermissionId, cancellationToken);
        PermissionGuards.EnsureExists(permission);
        PermissionGuards.EnsureNotDuplicatePermission(dto.PermissionId, member.PermissionId);
        PermissionGuards.EnsureNotOwnerPermission(permission);
        member.PermissionId = dto.PermissionId;
        await _memberRepository.UpdateAsync(member, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (member.UserId.HasValue)
        {
            await _publishEndpoint.Publish(new StaffRoleChanged
            {
                UserId = member.UserId.Value,
                OrganizationId = orgId,
                NewRoleName = permission.Name
            }, cancellationToken);
        }

        return _mapper.Map<MemberReponse>(member);
    }

    public async Task<MemberReponse?> GetMyMembershipAsync(Guid userId, Guid orgId, CancellationToken cancellationToken = default)
    {
        return await _memberRepository.GetByUserIdAndOrgIdAsync(userId, orgId, cancellationToken);
    }

    public async Task<bool> DeleteMemberAsync(Guid ownerId, Guid memberId, Guid orgId, CancellationToken cancellationToken = default)
    {
        var org = await _organizationRepository.GetByIdAsync(orgId, cancellationToken);
        OrgGuards.EnsureExists(org);
        OwnerGuards.EnsureOwner(org,ownerId);
        var member = await _memberRepository.GetAsync(memberId, cancellationToken);
        MemberGuards.EnsureExists(member);
        MemberGuards.EnsureNotOwner(member);
        await _memberRepository.RemoveAsync(member, cancellationToken);

        var invitation = await _invitationRepository.GetInvitationByEmailAndOrgId(member.Email, orgId, cancellationToken);
        if (invitation != null && invitation.Status == InvitationStatus.ACCEPTED)
            await _invitationRepository.RemoveAsync(invitation, cancellationToken);

        await _publishEndpoint.Publish(new StaffRemoved()
        {
            OrganizationId = orgId,
            UserId = member.UserId.Value,
        });
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

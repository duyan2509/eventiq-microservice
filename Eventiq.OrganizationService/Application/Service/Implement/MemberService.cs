using AutoMapper;
using Eventiq.Contracts;
using Eventiq.OrganizationService.Domain;
using Eventiq.OrganizationService.Domain.Entity;
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
    private readonly IUnitOfWork _unitOfWork;

    public MemberService(IMemberRepository memberRepository,
        ILogger<MemberService> logger,
        IPermissionRepository permissionRepository,
        IMapper mapper,
        IOrganizationRepository organizationRepository,
        IPublishEndpoint publishEndpoint,
        IUnitOfWork unitOfWork)
    {
        _memberRepository = memberRepository;
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
        var permission = await  _permissionRepository.GetByIdAsync(dto.PermissionId, cancellationToken);
        PermissionGuards.EnsureExists(permission);
        PermissionGuards.EnsureNotDuplicatePermission(permission, dto.PermissionId);
        PermissionGuards.EnsureNotOwnerPermission(permission);
        member.PermissionId = dto.PermissionId;
        await _memberRepository.UpdateAsync(member, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<MemberReponse>(member);
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
        // send message
        _publishEndpoint.Publish(new StaffRemoved()
        {
            OrganizationId = orgId,
            UserId = member.UserId.Value,
        });
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

using AutoMapper;
using Eventiq.OrganizationService.Domain.Entity;
using Eventiq.OrganizationService.Domain.Repositories;
using Eventiq.OrganizationService.Dtos;
using Eventiq.OrganizationService.Guards;

namespace Eventiq.OrganizationService.Application.Service;

public class MemberService : IMemberService
{
    private readonly IMemberRepository _memberRepository;
    private readonly ILogger<MemberService> _logger;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IMapper  _mapper;

    public MemberService(IMemberRepository memberRepository, ILogger<MemberService> logger, IPermissionRepository permissionRepository, IMapper mapper, IOrganizationRepository organizationRepository)
    {
        _memberRepository = memberRepository;
        _logger = logger;
        _permissionRepository = permissionRepository;
        _mapper = mapper;
        _organizationRepository = organizationRepository;
    }

    private readonly IOrganizationRepository _organizationRepository;


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
        member.PermissionId = dto.PermissionId;
        await _memberRepository.UpdateAsync(member, cancellationToken);
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
        await _memberRepository.RemoveAsync(member,cancellationToken);
        // send message
        return true;
    }
}

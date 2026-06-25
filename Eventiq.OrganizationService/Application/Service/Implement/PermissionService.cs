using AutoMapper;
using Eventiq.Contracts;
using Eventiq.OrganizationService.Domain;
using Eventiq.OrganizationService.Domain.Entity;
using Eventiq.OrganizationService.Domain.Repositories;
using Eventiq.OrganizationService.Dtos;
using Eventiq.OrganizationService.Guards;
using MassTransit;


namespace Eventiq.OrganizationService.Application.Service;

public class PermissionService : IPermissionService
{
    private readonly IPermissionRepository _permissionRepository;
    private readonly ILogger<PermissionService> _logger;
    private readonly IMapper _mapper;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberRepository _memberRepository;
    private readonly IPublishEndpoint _publishEndpoint;

    public PermissionService(IPermissionRepository permissionRepository, ILogger<PermissionService> logger, IMapper mapper, IOrganizationRepository organizationRepository, IUnitOfWork unitOfWork, IMemberRepository memberRepository, IPublishEndpoint publishEndpoint)
    {
        _permissionRepository = permissionRepository;
        _logger = logger;
        _mapper = mapper;
        _organizationRepository = organizationRepository;
        _unitOfWork = unitOfWork;
        _memberRepository = memberRepository;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<PaginatedResult<PermissionResponse>> GetPermissionsAsync(Guid userId, Guid orgId, int page = 1 , int size = 10, CancellationToken cancellationToken = default)
    {
        return await _permissionRepository.GetByOrgIdUserId(userId, orgId, page,size, cancellationToken);
    }

    public async Task<PermissionResponse> AddPermissionAsync(Guid userId, Guid orgId, PermissionDto dto, CancellationToken cancellationToken = default)
    {
        var org = await _organizationRepository.GetByIdAsync(orgId);
        OrgGuards.EnsureExists(org);
        OwnerGuards.EnsureOwner(org,userId);
        var nameExists = await _permissionRepository.ExistsByNameAsync(orgId, dto.Name, cancellationToken);
        PermissionGuards.EnsureNameNotDuplicate(nameExists);
        var permission = _mapper.Map<PermissionDto, Permission>(dto);
        permission.OrganizationId = orgId;
        await _permissionRepository.AddAsync(permission, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<PermissionResponse>(permission);
        
    }

    public async Task<PermissionResponse> UpdatePermissionAsync(Guid userId, Guid orgId, Guid permissionId, UpdatePermissionDto dto,
        CancellationToken cancellationToken = default)
    {
        var org = await _organizationRepository.GetByIdAsync(orgId);
        OrgGuards.EnsureExists(org);
        OwnerGuards.EnsureOwner(org,userId);
        var permission = await _permissionRepository.GetByIdAsync(permissionId);
        PermissionGuards.EnsureNotOwnerPermission(permission);
        if(dto.Name != null)
            permission.Name = dto.Name;
        bool isDesignerChanged = dto.IsDesigner.HasValue && dto.IsDesigner.Value != permission.IsDesigner;
        if(dto.IsDesigner!=null)
            permission.IsDesigner = dto.IsDesigner.Value;
        await _permissionRepository.UpdateAsync(permission, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (isDesignerChanged)
        {
            var affectedUserIds = await _memberRepository.GetUserIdsByPermissionIdAsync(permission.Id, cancellationToken);
            foreach (var affectedUserId in affectedUserIds)
            {
                await _publishEndpoint.Publish(new StaffRoleChanged
                {
                    UserId = affectedUserId,
                    OrganizationId = orgId,
                    NewRoleName = permission.Name
                }, cancellationToken);
            }
        }

        return _mapper.Map<PermissionResponse>(permission);
    }

    public async Task<bool> DeletePermissionAsync(Guid userId, Guid orgId, Guid permissionId, CancellationToken cancellationToken = default)
    {
        var org = await _organizationRepository.GetByIdAsync(orgId);
        OrgGuards.EnsureExists(org);
        OwnerGuards.EnsureOwner(org,userId);
        var permission = await _permissionRepository.GetByIdAsync(permissionId);
        PermissionGuards.EnsureExists(permission);
        PermissionGuards.EnsureNotOwnerPermission(permission);
        var hasMembersWithPermission = await _memberRepository.AnyByPermissionIdAsync(permissionId, cancellationToken);
        PermissionGuards.EnsureNoMembersAssigned(hasMembersWithPermission);
        await _permissionRepository.DeleteAsync(permission, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
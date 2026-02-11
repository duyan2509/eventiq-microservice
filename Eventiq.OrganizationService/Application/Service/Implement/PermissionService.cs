using AutoMapper;
using Eventiq.OrganizationService.Domain;
using Eventiq.OrganizationService.Domain.Entity;
using Eventiq.OrganizationService.Domain.Repositories;
using Eventiq.OrganizationService.Dtos;
using Eventiq.OrganizationService.Guards;

namespace Eventiq.OrganizationService.Application.Service;

public class PermissionService : IPermissionService
{
    private readonly IPermissionRepository _permissionRepository;
    private readonly ILogger<PermissionService> _logger;
    private readonly IMapper _mapper;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PermissionService(IPermissionRepository permissionRepository, ILogger<PermissionService> logger, IMapper mapper, IOrganizationRepository organizationRepository, IUnitOfWork unitOfWork)
    {
        _permissionRepository = permissionRepository;
        _logger = logger;
        _mapper = mapper;
        _organizationRepository = organizationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<PaginatedResult<PermissionResponse>> GetPermissionsAsync(Guid userId, Guid orgId, int page = 1 , int size = 10, CancellationToken cancellationToken = default)
    {
        return await _permissionRepository.GetByOrgIdUserId(orgId, userId, page,size, cancellationToken);
    }

    public async Task<PermissionResponse> AddPermissionAsync(Guid userId, Guid orgId, PermissionDto dto, CancellationToken cancellationToken = default)
    {
        var org = await _organizationRepository.GetByIdAsync(orgId);
        OrgGuards.EnsureExists(org);
        OwnerGuards.EnsureOwner(org,userId);
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
        if(dto.IsDesigner!=null)
            permission.IsDesigner = dto.IsDesigner.Value ;
        await _permissionRepository.UpdateAsync(permission, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
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
        await _permissionRepository.DeleteAsync(permission, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
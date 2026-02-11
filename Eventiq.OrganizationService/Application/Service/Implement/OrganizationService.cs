using AutoMapper;
using Eventiq.Contracts;
using Eventiq.OrganizationService.Domain;
using Eventiq.OrganizationService.Domain.Entity;
using Eventiq.OrganizationService.Domain.Repositories;
using Eventiq.OrganizationService.Dtos;
using Eventiq.OrganizationService.Extensions;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Eventiq.OrganizationService.Application.Service;

public class OrganizationService : IOrganizationService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<OrganizationService> _logger;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IMemberRepository _memberRepository;
    private readonly IUnitOfWork _unitOfWork;

    public OrganizationService(IOrganizationRepository organizationRepository, IMapper mapper, ILogger<OrganizationService> logger, IPublishEndpoint publishEndpoint, IMemberRepository memberRepository, IUnitOfWork unitOfWork)
    {
        _organizationRepository = organizationRepository;
        _mapper = mapper;
        _logger = logger;
        _publishEndpoint = publishEndpoint;
        _memberRepository = memberRepository;
        _unitOfWork = unitOfWork;
    }


    public Task<OrganizationDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public async Task<PaginatedResult<OrganizationDetail>> GetAllAsync(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        return await _organizationRepository.GetAllAsync(page, pageSize, cancellationToken);
    }

    public async Task<PaginatedResult<OrganizationDetail>> GetMyOrganizationsAsync(Guid userId, int page = 1, int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        return await _organizationRepository.GetAllMyOrgAsync(userId, page, pageSize, cancellationToken);
    }

    public async Task<OrganizationResponse> AddAsync(Guid userId, string email, OrganizationDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var organization = _mapper.Map<Organization>(dto);
            organization.Permissions = new List<Permission>();
            organization.Permissions.Add(new Permission()
            {
                Name = "Owner",
                IsDesigner = true
            });
            await _organizationRepository.AddAsync(organization, cancellationToken);
            await _memberRepository.AddAsync(new Member()
            {
                UserId = userId,
                Email = email,
                OrganizationId = organization.Id,
                PermissionId = organization.Permissions.First().Id
            }, cancellationToken);
            await _publishEndpoint.Publish(new OrganizationCreated
            {
                OwnerId = userId,
                OrganizationId = organization.Id,
            });
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return _mapper.Map<OrganizationResponse>(organization);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            throw new ConflictException(
                "Organization name already exists for this owner",
                "ORG_NAME_DUPLICATED"
            );
        }
        
    }

    public async Task<OrganizationResponse> UpdateAsync(Guid userId, Guid orgId, UpdateOrganizationDto dto, CancellationToken cancellationToken = default)
    {
        var organization = await _organizationRepository.GetByIdAsync(orgId);
        if(organization == null)
            throw new NotFoundException($"Organization with id {orgId} does not exist");
        if(organization.OwnerId != userId)
            throw new ForbiddenException($"You are not the owner of this organization");
        if(dto.Name!=null)
            organization.Name = dto.Name;
        if(dto.Description!=null)
            organization.Description = dto.Description;
        await _organizationRepository.UpdateAsync(organization, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return _mapper.Map<OrganizationResponse>(organization);
    }
}

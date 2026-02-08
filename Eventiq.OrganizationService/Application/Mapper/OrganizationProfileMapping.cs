using AutoMapper;
using Eventiq.OrganizationService.Domain.Entity;
using Eventiq.OrganizationService.Dtos;

namespace Eventiq.OrganizationService.Mapper;

public class OrganizationProfileMapping:Profile
{
    public OrganizationProfileMapping()
    {
        CreateMap<Organization, OrganizationDto>();
        CreateMap<OrganizationDto, Organization>();
    }
}
using AutoMapper;

namespace Eventiq.OrganizationService.Mapper;

public class OrganizationProfileMapping:Profile
{
    public OrganizationProfileMapping()
    {
        CreateMap<Organization, OrganizationDto>();
        CreateMap<OrganizationDto, Organization>();
    }
}
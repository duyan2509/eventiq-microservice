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
        CreateMap<Organization, OrganizationResponse>();
        CreateMap<PermissionDto, Permission>();
        CreateMap<Permission, PermissionResponse>();
        CreateMap<Invitation, InviationResponse>()
            .ForMember(dest => dest.OrganizationName, opt => opt.MapFrom(src => src.Organization.Name))
            .ForMember(dest =>dest.PermissionName, opt => opt.MapFrom(src => src.Permission.Name))
            .ForMember(dest=>dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));
        CreateMap<Member, MemberReponse>()
            .ForMember(dest => dest.PermissionName, opt => opt.MapFrom(src => src.Permission.Name));
    }
}
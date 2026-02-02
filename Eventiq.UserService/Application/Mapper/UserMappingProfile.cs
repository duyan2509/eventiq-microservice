using AutoMapper;
using Eventiq.UserService.Application.Dto;
using Eventiq.UserService.Domain.Entity;
using Eventiq.UserService.Model;

namespace Eventiq.UserService.Mapper;

public class UserMappingProfile : Profile
{
    public UserMappingProfile()
    {
        CreateMap<LoginUserModel, UserDto>()
            .ForMember(dest=>dest.Roles, opt=>opt.MapFrom(src=>src.Roles));
        CreateMap<RegisterDto, User>();
        CreateMap<User, UserDto>();
    }
}

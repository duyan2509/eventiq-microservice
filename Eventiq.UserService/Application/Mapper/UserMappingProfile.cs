using AutoMapper;
using Eventiq.UserService.Application.Dto;
using Eventiq.UserService.Domain.Entity;
using Eventiq.UserService.Guards;
using Eventiq.UserService.Helper;
using Eventiq.UserService.Model;
using Microsoft.OpenApi;

namespace Eventiq.UserService.Mapper;

public class UserMappingProfile : Profile
{
    public UserMappingProfile()
    {
        CreateMap<LoginUserModel, UserDto>()
            .ForMember(dest=>dest.Roles, opt=>opt.MapFrom(src=>src.Roles));
        CreateMap<RegisterDto, User>()
            .ForMember(dest=>dest.Avatar, opt=>opt.MapFrom(_=>string.Empty))
            .ForMember(dest=>dest.Username, opt=>opt.MapFrom(src=>src.Email))
            .ForMember(dest=>dest.PasswordHash, opt=>opt.MapFrom(src=>PasswordHash.SHA256Hash(src.Password)));
        CreateMap<User, UserDto>();
    }
}

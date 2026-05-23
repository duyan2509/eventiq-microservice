using AutoMapper;
using Eventiq.SeatService.Application.Dtos;
using Eventiq.SeatService.Domain.Entity;

namespace Eventiq.SeatService.Application.Mapper;

public class SeatMapProfile : Profile
{
    public SeatMapProfile()
    {
        // SeatMap
        CreateMap<CreateSeatMapDto, SeatMap>();
        CreateMap<SeatMap, SeatMapResponse>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));
        CreateMap<SeatMap, SeatMapDetailResponse>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));
        CreateMap<SeatMap, SeatMapLayoutResponse>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));

        // Seat
        CreateMap<AddSeatDto, Seat>();
        CreateMap<Seat, SeatResponse>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));
        CreateMap<Seat, SeatLayoutResponse>();

        // SeatObject
        CreateMap<AddObjectDto, SeatObject>();
        CreateMap<SeatObject, SeatObjectResponse>()
            .ForMember(d => d.ObjectType, o => o.MapFrom(s => s.ObjectType.ToString()));

        // Version
        CreateMap<SeatMapVersion, SeatMapVersionResponse>();
        CreateMap<SeatMapVersion, SeatMapVersionDetailResponse>();
    }
}

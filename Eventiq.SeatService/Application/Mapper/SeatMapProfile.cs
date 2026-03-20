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

        // Section
        CreateMap<AddSectionDto, SeatSection>();
        CreateMap<SeatSection, SeatSectionResponse>()
            .ForMember(d => d.SectionType, o => o.MapFrom(s => s.SectionType.ToString()));

        // Row
        CreateMap<AddRowDto, SeatRow>();
        CreateMap<SeatRow, SeatRowResponse>();

        // Seat
        CreateMap<AddSeatDto, Seat>();
        CreateMap<Seat, SeatResponse>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.SeatType, o => o.MapFrom(s => s.SeatType.ToString()));

        // SeatObject
        CreateMap<AddObjectDto, SeatObject>();
        CreateMap<SeatObject, SeatObjectResponse>()
            .ForMember(d => d.ObjectType, o => o.MapFrom(s => s.ObjectType.ToString()));

        // Version
        CreateMap<SeatMapVersion, SeatMapVersionResponse>();
        CreateMap<SeatMapVersion, SeatMapVersionDetailResponse>();
    }
}

using AutoMapper;
using Eventiq.EventService.Domain.Entity;

namespace Eventiq.EventService.Application.Mapper;

public class EventProfileMapping : Profile
{
    public EventProfileMapping()
    {
        CreateMap<Event, Event>();
        CreateMap<Submission, Submission>();
        CreateMap<Legend, Legend>();
        CreateMap<Session, Session>();
        CreateMap<Chart, Chart>();
    }
}

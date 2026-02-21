using AutoMapper;
using Eventiq.EventService.Domain.Entity;
using Eventiq.EventService.Dtos;
using Eventiq.EventService.Infrastructure.Persistence.ReadModel;

namespace Eventiq.EventService.Application.Mapper;

public class EventProfileMapping : Profile
{
    public EventProfileMapping()
    {
        CreateMap<Event, Event>();
        CreateMap<Submission, SubmissionResponse>();
        CreateMap<SubmissionModel, SubmissionResponse>();

        CreateMap<LegendModel, LegendResponse>();
        CreateMap<CreateLegendDto, Legend>();
        CreateMap<CreateChartDto, Chart>();
        CreateMap<ChartModel, ChartResponse>();
        CreateMap<CreateSessionDto, Session> ();
        CreateMap<SessionModel, SessionResponse>();
        CreateMap<Session, SessionResponse>();
        CreateMap<Chart, Chart>();
    }
}

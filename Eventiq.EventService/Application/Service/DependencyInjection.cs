using Eventiq.EventService.Application.Service.Implement;
using Eventiq.EventService.Application.Service.Interface;
using Eventiq.EventService.Infrastructure.Http;
using Eventiq.EventService.Infrastructure.Persistence;

namespace Eventiq.EventService.Application.Service;

public static class DependencyInjection
{
    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<ISubmissionService, SubmissionService>();
        services.AddScoped<ILegendService, LegendService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IChartService, ChartService>();
        services.AddScoped<ITicketService, TicketService>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}

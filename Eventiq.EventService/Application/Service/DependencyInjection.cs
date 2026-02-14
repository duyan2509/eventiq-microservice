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
        return services;
    }
}

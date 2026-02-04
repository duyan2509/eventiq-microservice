namespace Eventiq.OrganizationService.Application.Service;

public static class DependencyInjection
{
    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<IOrganizationService, OrganizationService>();
        return services;
    }
}

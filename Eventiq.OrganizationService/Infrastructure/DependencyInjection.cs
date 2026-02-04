using Eventiq.OrganizationService.Domain.Repositories;
using Eventiq.OrganizationService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Eventiq.OrganizationService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<EvtOrganizationDbContext>(opt =>
        {
            opt.UseNpgsql(
                config.GetConnectionString("Postgres"),
                npgsql => npgsql.EnableRetryOnFailure(5));
        });
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        return services;
    }
}

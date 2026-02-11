using Eventiq.OrganizationService.Domain;
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
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IMemberRepository, MemberRepository>();
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        return services;
    }
}

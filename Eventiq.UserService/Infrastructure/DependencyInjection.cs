using Eventiq.UserService.Domain.Repositories;
using Eventiq.UserService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Eventiq.UserService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services,IConfiguration config)
    {
        services.AddDbContext<EvtUserDbContext>(opt =>
        {
            opt.UseNpgsql(
                config.GetConnectionString("Postgres"),
                npgsql => npgsql.EnableRetryOnFailure(5)
            );
        });
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshRepository>();
        services.AddScoped<IBanHistoryRepository, BanHistoryRepository>();
        services.AddScoped<IUserRoleRepository, UserRoleRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        return services;
    }
    
}
using Eventiq.UserService.Application.Service;
using Eventiq.UserService.Domain.Repositories;
using Eventiq.UserService.Infrastructure.Blob;
using Eventiq.UserService.Infrastructure.Cache;
using Eventiq.UserService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace Eventiq.UserService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<EvtUserDbContext>(opt =>
        {
            opt.UseNpgsql(
                config.GetConnectionString("Postgres"),
                npgsql =>
                {
                    npgsql.EnableRetryOnFailure(5);
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "user_service");
                }
            );
        });

        var redisConn = config["Redis"];
        if (!string.IsNullOrEmpty(redisConn))
        {
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConn));
            services.AddSingleton<IBanBlacklistService, RedisBanBlacklistService>();
        }

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshRepository>();
        services.AddScoped<IBanHistoryRepository, BanHistoryRepository>();
        services.AddScoped<IUserRoleRepository, UserRoleRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IBlobService, AzureBlobService>();
        return services;
    }
}
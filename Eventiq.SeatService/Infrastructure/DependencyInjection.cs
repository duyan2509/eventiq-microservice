using Eventiq.SeatService.Application.Service.Interface;
using Eventiq.SeatService.Domain.Repositories;
using Eventiq.SeatService.Infrastructure.Persistence;
using Eventiq.SeatService.Infrastructure.Persistence.Repositories;
using Eventiq.SeatService.Infrastructure.Redis;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace Eventiq.SeatService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // PostgreSQL
        var connectionString = config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' not found.");

        services.AddDbContext<SeatDbContext>(opt =>
        {
            opt.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.EnableRetryOnFailure(5);
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "seat_service");
            }).UseSnakeCaseNamingConvention();
        });

        // Redis (lazy connection to avoid startup crashes)
        var redisConnectionString = config.GetConnectionString("Redis") ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var options = ConfigurationOptions.Parse(redisConnectionString);
            options.AbortOnConnectFail = false;
            options.ConnectTimeout = 10000;
            return ConnectionMultiplexer.Connect(options);
        });

        // Repositories
        services.AddScoped<ISeatMapRepository, SeatMapRepository>();
        services.AddScoped<ISeatSectionRepository, SeatSectionRepository>();
        services.AddScoped<ISeatRowRepository, SeatRowRepository>();
        services.AddScoped<ISeatRepository, SeatRepository>();
        services.AddScoped<ISeatObjectRepository, SeatObjectRepository>();
        services.AddScoped<ISeatMapVersionRepository, SeatMapVersionRepository>();

        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Presence
        services.AddSingleton<IPresenceService, RedisPresenceService>();

        return services;
    }
}

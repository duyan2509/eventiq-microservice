using Eventiq.SeatService.Application.Service.Interface;
using Eventiq.SeatService.Domain.Repositories;
using Eventiq.SeatService.Infrastructure.BackgroundServices;
using Eventiq.SeatService.Infrastructure.Persistence;
using Eventiq.SeatService.Infrastructure.Persistence.Repositories;
using Eventiq.SeatService.Infrastructure.Redis;
using Eventiq.SeatService.Infrastructure.SignalR;
using Microsoft.EntityFrameworkCore;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
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
        services.AddScoped<ISeatRepository, SeatRepository>();
        services.AddScoped<ISeatObjectRepository, SeatObjectRepository>();
        services.AddScoped<ISeatMapVersionRepository, SeatMapVersionRepository>();

        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Presence
        services.AddSingleton<IPresenceService, RedisPresenceService>();

        // Output Cache (Redis-backed)
        services.AddStackExchangeRedisOutputCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = "SeatService:OutputCache:";
        });
        services.AddOutputCache(options =>
        {
            options.AddPolicy(OutputCachePolicies.SeatMapLayout, builder =>
                builder.Expire(TimeSpan.FromHours(1))
                       .SetVaryByRouteValue("sessionId")
                       .Tag(OutputCachePolicies.SeatMapLayoutTag));
        });

        // Redlock
        services.AddSingleton<RedLockNet.IDistributedLockFactory>(sp =>
        {
            var multiplexer = sp.GetRequiredService<IConnectionMultiplexer>();
            return RedLockFactory.Create(new List<RedLockMultiplexer>
            {
                new RedLockMultiplexer(multiplexer)
            });
        });

        // Seat reservation
        services.AddScoped<Application.Service.Interface.ISeatReservationService,
            Application.Service.Implement.SeatReservationService>();

        // Status broadcaster (SignalR → booking hub)
        services.AddScoped<ISeatStatusBroadcaster, SignalRSeatStatusBroadcaster>();

        // Background cleanup
        services.AddHostedService<HoldExpiryCleanupService>();

        return services;
    }
}

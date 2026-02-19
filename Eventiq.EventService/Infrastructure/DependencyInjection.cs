using System.Data;
using Eventiq.EventService.Domain.Repositories;
using Eventiq.EventService.Infrastructure.Persistence;
using Eventiq.EventService.Infrastructure.Persistence.DapperRepositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Eventiq.EventService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Postgres") ?? throw new InvalidOperationException("Connection string 'Postgres' not found.");

        services.AddDbContext<EvtEventDbContext>(opt =>
        {
            opt.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(5))
                .UseSnakeCaseNamingConvention();
        });

        services.AddScoped<IDbConnection>(_ =>
        {
            var conn = new NpgsqlConnection(connectionString);
            conn.Open();
            return conn;
        });

        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<ISubmissionRepository, SubmissionRepository>();
        services.AddScoped<ILegendRepository, LegendRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IChartRepository, ChartRepository>();

        return services;
    }
}

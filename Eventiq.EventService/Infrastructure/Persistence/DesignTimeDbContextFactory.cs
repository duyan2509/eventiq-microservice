using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Eventiq.EventService.Infrastructure.Persistence;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<EvtEventDbContext>
{
    public EvtEventDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<EvtEventDbContext>();
        optionsBuilder.UseNpgsql(
            configuration.GetConnectionString("Postgres"),
            npgsql => npgsql.EnableRetryOnFailure(5))
            .UseSnakeCaseNamingConvention();

        return new EvtEventDbContext(optionsBuilder.Options);
    }
}

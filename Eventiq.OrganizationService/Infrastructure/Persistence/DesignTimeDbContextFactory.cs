using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Eventiq.OrganizationService.Infrastructure.Persistence;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<EvtOrganizationDbContext>
{
    public EvtOrganizationDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<EvtOrganizationDbContext>();
        optionsBuilder.UseNpgsql(
            configuration.GetConnectionString("Postgres"),
            npgsql =>
            {
                npgsql.EnableRetryOnFailure(5);
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "org_service");
            });

        return new EvtOrganizationDbContext(optionsBuilder.Options);
    }
}

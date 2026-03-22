using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Eventiq.SeatService.Infrastructure.Persistence;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SeatDbContext>
{
    public SeatDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<SeatDbContext>();
        optionsBuilder.UseNpgsql(
            configuration.GetConnectionString("Postgres"),
            npgsql =>
            {
                npgsql.EnableRetryOnFailure(5);
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "seat_service");
            })
            .UseSnakeCaseNamingConvention();

        return new SeatDbContext(optionsBuilder.Options);
    }
}

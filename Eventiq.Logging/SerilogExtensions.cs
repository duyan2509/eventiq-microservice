using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace Eventiq.Logging;

public static class SerilogExtensions
{
    public static IHostBuilder UseEventiqSerilog(this IHostBuilder host)
    {
        return host.UseSerilog((ctx, services, cfg) =>
        {
            cfg
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)

                .ReadFrom.Configuration(ctx.Configuration)
                .ReadFrom.Services(services)

                .Enrich.FromLogContext()
                .Enrich.WithProperty("Service", ctx.HostingEnvironment.ApplicationName)
                .Enrich.WithProperty("Environment", ctx.HostingEnvironment.EnvironmentName)

                .WriteTo.Console();
        });
    }
}
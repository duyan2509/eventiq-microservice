using Eventiq.OrganizationService.Application.Service;
using Eventiq.OrganizationService.Infrastructure;
using Eventiq.Logging;

namespace Eventiq.OrganizationService.Extensions;

public static class Extensions
{
    public static void AddApplicationServices(this WebApplicationBuilder builder)
    {
        builder.Host.UseEventiqSerilog();
        builder.Services
            .AddServices(builder.Configuration)
            .AddInfrastructure(builder.Configuration);
    }
}

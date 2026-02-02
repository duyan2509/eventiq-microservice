using Eventiq.Logging;
using Eventiq.UserService.Application.Service;
using Eventiq.UserService.Infrastructure;

namespace Eventiq.UserService.Extensions;

public static class Extensions
{
    public static void AddApplicationServices(this WebApplicationBuilder builder)
    {
        builder.Host.UseEventiqSerilog();
        builder.Services.AddServices(builder.Configuration)
            .AddInfrastructure(builder.Configuration);
        builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

    }
}
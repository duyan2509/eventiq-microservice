using Eventiq.Logging;
using Eventiq.UserService.Application.Service;

namespace Eventiq.UserService.Extensions;

public static class Extensions
{
    public static void AddApplicationServices(this WebApplicationBuilder builder)
    {
        builder.Host.UseEventiqSerilog();
        builder.Services.AddServices(builder.Configuration);
    }
}
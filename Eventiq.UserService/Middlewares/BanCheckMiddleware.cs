using Eventiq.UserService.Infrastructure.Cache;

namespace Eventiq.UserService.Middlewares;

public class BanCheckMiddleware
{
    private readonly RequestDelegate _next;

    public BanCheckMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IBanBlacklistService? blacklist = null)
    {
        if (blacklist != null && context.User.Identity?.IsAuthenticated == true)
        {
            var sub = context.User.FindFirst("sub")?.Value;
            if (Guid.TryParse(sub, out var userId) && await blacklist.IsBannedAsync(userId))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { message = "Account is banned." });
                return;
            }
        }
        await _next(context);
    }
}

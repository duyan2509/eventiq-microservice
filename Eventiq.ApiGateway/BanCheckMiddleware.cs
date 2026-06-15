using System.Security.Claims;
using StackExchange.Redis;

namespace Eventiq.ApiGateway;

/// <summary>
/// Blocks banned users at the gateway, before any request is proxied to a
/// downstream service. JWTs are stateless and live ~5 min, so a ban must be
/// enforced per-request against the shared Redis blacklist (key "ban:user:{id}",
/// written by UserService). Running here covers every service in one place
/// instead of relying on each service to carry its own ban middleware.
/// </summary>
public class BanCheckMiddleware
{
    private const string KeyPrefix = "ban:user:";
    private readonly RequestDelegate _next;

    public BanCheckMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IConnectionMultiplexer? redis = null)
    {
        if (redis != null && context.User.Identity?.IsAuthenticated == true)
        {
            // JwtBearer maps the inbound "sub" claim to NameIdentifier, so read that
            // (fall back to "sub" in case mapping is ever disabled).
            var sub = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? context.User.FindFirst("sub")?.Value;

            if (Guid.TryParse(sub, out var userId)
                && await redis.GetDatabase().KeyExistsAsync($"{KeyPrefix}{userId}"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { message = "Account is banned." });
                return;
            }
        }
        await _next(context);
    }
}

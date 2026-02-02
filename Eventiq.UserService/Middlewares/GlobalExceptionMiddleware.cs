using System.Net;
using System.Security.Authentication;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace Eventiq.UserService;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");

            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(
        HttpContext context,
        Exception exception)
    {
        var statusCode = HttpStatusCode.InternalServerError;
        var message = exception.Message;

        switch (exception)
        {
            case NotFoundException:
                statusCode = HttpStatusCode.NotFound;
                break;

            case ForbiddenException:
                statusCode = HttpStatusCode.Forbidden;
                break;

            case InvalidCredentialException:
            case SecurityTokenException:
                statusCode = HttpStatusCode.Unauthorized;
                break;

            case ArgumentException:
            case BadRequestException:
                statusCode = HttpStatusCode.BadRequest;
                break;
        }

        var response = new ErrorResponse
        {
            StatusCode = (int)statusCode,
            Message = message,
            TraceId = context.TraceIdentifier
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        return context.Response.WriteAsync(
            JsonSerializer.Serialize(response));
    }
}
public class ErrorResponse
{
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string TraceId { get; set; } = string.Empty;
}

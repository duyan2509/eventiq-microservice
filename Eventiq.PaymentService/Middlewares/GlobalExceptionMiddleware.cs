using System.Net;
using System.Security.Authentication;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace Eventiq.PaymentService;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
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

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var statusCode = HttpStatusCode.InternalServerError;
        var message = exception.Message;

        if (exception is AppException appEx)
        {
            statusCode = (HttpStatusCode)appEx.StatusCode;
            message = appEx.Message;
        }
        else switch (exception)
        {
            case SecurityTokenException:
            case InvalidCredentialException:
                statusCode = HttpStatusCode.Unauthorized;
                message = "Unauthorized";
                break;
            case ArgumentException:
                statusCode = HttpStatusCode.BadRequest;
                message = exception.Message;
                break;
            case NotImplementedException:
                statusCode = HttpStatusCode.NotImplemented;
                message = "Not implemented";
                break;
        }

        var response = new { StatusCode = (int)statusCode, Message = message, TraceId = context.TraceIdentifier };
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;
        return context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}

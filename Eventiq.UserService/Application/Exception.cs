namespace Eventiq.UserService;

abstract class AppException : Exception
{
    public int StatusCode { get; }
    public string ErrorCode { get; }

    protected AppException(string message, int statusCode, string errorCode)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }
}
class BusinessException : AppException
{
    public BusinessException(string message, string errorCode = "CONFLICT")
        : base(message, 400, errorCode) { }
}
class NotFoundException : AppException
{
    public NotFoundException(string message)
        : base(message, 404, "NOT_FOUND") {}
}

class UnauthorizedException : AppException
{
    public UnauthorizedException(string message)
        : base(message, 401, "UNAUTHORIZED") {}
}

class ForbiddenException : AppException
{
    public ForbiddenException(string message)
        : base(message, 403, "FORBIDDEN") {}
}

class BadRequestException : AppException
{
    public BadRequestException(string message)
        : base(message, 400, "BAD_REQUEST") {}
}

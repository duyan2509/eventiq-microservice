namespace Eventiq.PaymentService;

public abstract class AppException : Exception
{
    public int StatusCode { get; }
    protected AppException(string message, int statusCode) : base(message) => StatusCode = statusCode;
}

public class BusinessException : AppException
{
    public BusinessException(string message) : base(message, 400) { }
}

public class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message, 404) { }
}

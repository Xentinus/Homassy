using Homassy.Data.Enums;

namespace Homassy.Data.Exceptions;

public class RequestTimeoutException : Exception
{
    public string ErrorCode { get; } = ErrorCodes.SystemRequestTimeout;

    public RequestTimeoutException(string message = "Request timed out")
        : base(message)
    {
    }
}

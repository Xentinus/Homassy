using Homassy.API.Enums;

namespace Homassy.API.Exceptions;

public class RequestTimeoutException : Exception
{
    public string ErrorCode { get; } = ErrorCodes.SystemRequestTimeout;

    public RequestTimeoutException(string message = "Request timed out")
        : base(message)
    {
    }
}

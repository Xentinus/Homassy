namespace Homassy.API.Exceptions;

public class RequestTimeoutException : Exception
{
    public RequestTimeoutException(string message = "Request timed out")
        : base(message)
    {
    }
}

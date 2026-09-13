namespace Homassy.Data.Exceptions
{
    /// <summary>
    /// The handful of HTTP status codes the domain exceptions in this namespace carry.
    /// </summary>
    /// <remarks>
    /// These used to come from <c>Microsoft.AspNetCore.Http.StatusCodes</c>. They are five integer
    /// constants, and taking them from there would be the only reason this library referenced
    /// ASP.NET Core at all - which is exactly the dependency #91 exists to remove. The names match
    /// the framework's on purpose, so the exceptions read the same as they always did.
    /// <para>
    /// The status lives on the exception because the exception is where the API's
    /// <c>GlobalExceptionMiddleware</c> reads it: a "product not found" is a 404 wherever it is
    /// thrown from, and a worker that never answers an HTTP request simply never asks.
    /// </para>
    /// </remarks>
    internal static class HttpStatus
    {
        public const int Status400BadRequest = 400;
        public const int Status401Unauthorized = 401;
        public const int Status403Forbidden = 403;
        public const int Status404NotFound = 404;
        public const int Status429TooManyRequests = 429;
    }
}

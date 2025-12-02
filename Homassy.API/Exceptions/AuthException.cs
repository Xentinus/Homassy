namespace Homassy.API.Exceptions
{
    public abstract class AuthException : Exception
    {
        public int StatusCode { get; }

        protected AuthException(string message, int statusCode) : base(message)
        {
            StatusCode = statusCode;
        }
    }

    public class UnauthorizedException : AuthException
    {
        public UnauthorizedException(string message = "Unauthorized")
            : base(message, StatusCodes.Status401Unauthorized)
        {
        }
    }

    public class UserNotFoundException : AuthException
    {
        public UserNotFoundException(string message = "User not found")
            : base(message, StatusCodes.Status401Unauthorized)
        {
        }
    }

    public class FamilyNotFoundException : AuthException
    {
        public FamilyNotFoundException(string message = "Family not found")
            : base(message, StatusCodes.Status404NotFound)
        {
        }
    }

    public class InvalidCredentialsException : AuthException
    {
        public InvalidCredentialsException(string message = "Invalid email or code")
            : base(message, StatusCodes.Status401Unauthorized)
        {
        }
    }

    public class ExpiredCredentialsException : AuthException
    {
        public ExpiredCredentialsException(string message = "Invalid or expired code")
            : base(message, StatusCodes.Status401Unauthorized)
        {
        }
    }

    public class BadRequestException : AuthException
    {
        public BadRequestException(string message = "Invalid request data")
            : base(message, StatusCodes.Status400BadRequest)
        {
        }
    }

    public class ForbiddenException : AuthException
    {
        public ForbiddenException(string message = "Access forbidden")
            : base(message, StatusCodes.Status403Forbidden)
        {
        }
    }
}

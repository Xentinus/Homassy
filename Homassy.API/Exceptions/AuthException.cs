using Homassy.API.Enums;

namespace Homassy.API.Exceptions
{
    public abstract class AuthException : Exception
    {
        public int StatusCode { get; }
        public string ErrorCode { get; }

        protected AuthException(string message, int statusCode, string errorCode) : base(message)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
        }
    }

    public class UnauthorizedException : AuthException
    {
        public UnauthorizedException(string message = "Unauthorized", string errorCode = ErrorCodes.AuthUnauthorized)
            : base(message, StatusCodes.Status401Unauthorized, errorCode)
        {
        }
    }

    public class UserNotFoundException : AuthException
    {
        public UserNotFoundException(string message = "User not found", string errorCode = ErrorCodes.UserNotFound)
            : base(message, StatusCodes.Status401Unauthorized, errorCode)
        {
        }
    }

    public class FamilyNotFoundException : AuthException
    {
        public FamilyNotFoundException(string message = "Family not found", string errorCode = ErrorCodes.FamilyNotFound)
            : base(message, StatusCodes.Status404NotFound, errorCode)
        {
        }
    }

    public class InvalidCredentialsException : AuthException
    {
        public InvalidCredentialsException(string message = "Invalid email or code", string errorCode = ErrorCodes.AuthInvalidCredentials)
            : base(message, StatusCodes.Status401Unauthorized, errorCode)
        {
        }
    }

    public class ExpiredCredentialsException : AuthException
    {
        public ExpiredCredentialsException(string message = "Invalid or expired code", string errorCode = ErrorCodes.AuthExpiredCredentials)
            : base(message, StatusCodes.Status401Unauthorized, errorCode)
        {
        }
    }

    public class BadRequestException : AuthException
    {
        public BadRequestException(string message = "Invalid request data", string errorCode = ErrorCodes.ValidationInvalidRequest)
            : base(message, StatusCodes.Status400BadRequest, errorCode)
        {
        }
    }

    public class ForbiddenException : AuthException
    {
        public ForbiddenException(string message = "Access forbidden", string errorCode = ErrorCodes.AuthForbidden)
            : base(message, StatusCodes.Status403Forbidden, errorCode)
        {
        }
    }
}

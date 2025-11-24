namespace Homassy.API.Models.Common
{
    public class ApiResponse<T>
    {
        public bool Success { get; init; }
        public T? Data { get; init; }
        public string? Message { get; init; }
        public List<string>? Errors { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        // Success response with data
        public static ApiResponse<T> SuccessResponse(T data, string? message = null)
        {
            return new ApiResponse<T>
            {
                Success = true,
                Data = data,
                Message = message
            };
        }

        // Success response without data
        public static ApiResponse<T> SuccessResponse(string message)
        {
            return new ApiResponse<T>
            {
                Success = true,
                Message = message
            };
        }

        // Error response with single error
        public static ApiResponse<T> ErrorResponse(string error)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Errors = [error]
            };
        }

        // Error response with multiple errors
        public static ApiResponse<T> ErrorResponse(List<string> errors)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Errors = errors
            };
        }

        // Error response with message and errors
        public static ApiResponse<T> ErrorResponse(string message, List<string> errors)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = message,
                Errors = errors
            };
        }
    }

    // Responses without data
    public class ApiResponse : ApiResponse<object>
    {
    }
}

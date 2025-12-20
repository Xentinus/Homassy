namespace Homassy.API.Models.Common
{
    public class ApiResponse<T>
    {
        public bool Success { get; init; }
        public T? Data { get; init; }
        public List<string>? ErrorCodes { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        public static ApiResponse<T> SuccessResponse(T data)
        {
            return new ApiResponse<T>
            {
                Success = true,
                Data = data
            };
        }

        public static ApiResponse<T> ErrorResponse(string errorCode)
        {
            return new ApiResponse<T>
            {
                Success = false,
                ErrorCodes = [errorCode]
            };
        }

        public static ApiResponse<T> ErrorResponse(List<string> errorCodes)
        {
            return new ApiResponse<T>
            {
                Success = false,
                ErrorCodes = errorCodes
            };
        }

        public static ApiResponse<T> ErrorResponse(params string[] errorCodes)
        {
            return new ApiResponse<T>
            {
                Success = false,
                ErrorCodes = [.. errorCodes]
            };
        }
    }

    public class ApiResponse : ApiResponse<object>
    {
        public static ApiResponse SuccessResponse()
        {
            return new ApiResponse
            {
                Success = true
            };
        }
    }
}

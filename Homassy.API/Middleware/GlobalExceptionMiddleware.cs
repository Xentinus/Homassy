using Homassy.API.Enums;
using Homassy.API.Exceptions;
using Homassy.API.Models.Common;
using Serilog;
using System.Text.Json;

namespace Homassy.API.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public GlobalExceptionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var correlationId = context.Items[CorrelationIdMiddleware.CorrelationIdItemKey]?.ToString() 
                ?? Guid.NewGuid().ToString();
            var (statusCode, errorCode) = MapExceptionToResponse(exception);

            // Log based on severity
            if (statusCode >= 500)
            {
                Log.Error(exception, $"Unhandled exception [CorrelationId: {correlationId}]: {exception.Message}");
            }
            else
            {
                Log.Warning($"Handled exception [CorrelationId: {correlationId}]: {exception.GetType().Name} - {exception.Message} - ErrorCode: {errorCode}");
            }

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            var response = ApiResponse.ErrorResponse(errorCode);
            var jsonResponse = JsonSerializer.Serialize(response, JsonOptions);

            await context.Response.WriteAsync(jsonResponse);
        }

        private static (int StatusCode, string ErrorCode) MapExceptionToResponse(Exception exception)
        {
            return exception switch
            {
                // Auth exceptions with built-in status code and error code
                AuthException authEx => (authEx.StatusCode, authEx.ErrorCode),

                // Product exceptions
                ProductNotFoundException ex => (StatusCodes.Status404NotFound, ex.ErrorCode),
                ProductInventoryItemNotFoundException ex => (StatusCodes.Status404NotFound, ex.ErrorCode),
                ProductAccessDeniedException ex => (StatusCodes.Status403Forbidden, ex.ErrorCode),

                // Location exceptions
                LocationNotFoundException ex => (StatusCodes.Status404NotFound, ex.ErrorCode),
                ShoppingLocationNotFoundException ex => (StatusCodes.Status404NotFound, ex.ErrorCode),
                StorageLocationNotFoundException ex => (StatusCodes.Status404NotFound, ex.ErrorCode),
                LocationAccessDeniedException ex => (StatusCodes.Status403Forbidden, ex.ErrorCode),

                // Shopping list exceptions
                ShoppingListNotFoundException ex => (StatusCodes.Status404NotFound, ex.ErrorCode),
                ShoppingListItemNotFoundException ex => (StatusCodes.Status404NotFound, ex.ErrorCode),
                ShoppingListAccessDeniedException ex => (StatusCodes.Status403Forbidden, ex.ErrorCode),
                InvalidShoppingListItemException ex => (StatusCodes.Status400BadRequest, ex.ErrorCode),

                // Timeout exceptions
                RequestTimeoutException ex => (StatusCodes.Status504GatewayTimeout, ex.ErrorCode),

                // Standard exceptions
                ArgumentNullException => (StatusCodes.Status400BadRequest, ErrorCodes.ValidationRequiredField),
                ArgumentException => (StatusCodes.Status400BadRequest, ErrorCodes.ValidationInvalidRequest),
                InvalidOperationException => (StatusCodes.Status400BadRequest, ErrorCodes.ValidationInvalidRequest),
                UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, ErrorCodes.SystemUnauthorizedAccess),
                OperationCanceledException => (StatusCodes.Status499ClientClosedRequest, ErrorCodes.SystemRequestCancelled),

                // Default: Internal Server Error
                _ => (StatusCodes.Status500InternalServerError, ErrorCodes.SystemUnexpectedError)
            };
        }
    }
}

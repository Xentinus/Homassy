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
            var (statusCode, message) = MapExceptionToResponse(exception);

            // Log based on severity
            if (statusCode >= 500)
            {
                Log.Error(exception, $"Unhandled exception [CorrelationId: {correlationId}]: {exception.Message}");
            }
            else
            {
                Log.Warning($"Handled exception [CorrelationId: {correlationId}]: {exception.GetType().Name} - {exception.Message}");
            }

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            var response = ApiResponse.ErrorResponse(message);
            var jsonResponse = JsonSerializer.Serialize(response, JsonOptions);

            await context.Response.WriteAsync(jsonResponse);
        }

        private static (int StatusCode, string Message) MapExceptionToResponse(Exception exception)
        {
            return exception switch
            {
                // Auth exceptions with built-in status code
                AuthException authEx => (authEx.StatusCode, authEx.Message),

                // Product exceptions
                ProductNotFoundException => (StatusCodes.Status404NotFound, exception.Message),
                ProductInventoryItemNotFoundException => (StatusCodes.Status404NotFound, exception.Message),
                ProductAccessDeniedException => (StatusCodes.Status403Forbidden, exception.Message),

                // Location exceptions
                LocationNotFoundException => (StatusCodes.Status404NotFound, exception.Message),
                ShoppingLocationNotFoundException => (StatusCodes.Status404NotFound, exception.Message),
                StorageLocationNotFoundException => (StatusCodes.Status404NotFound, exception.Message),
                LocationAccessDeniedException => (StatusCodes.Status403Forbidden, exception.Message),

                // Shopping list exceptions
                ShoppingListNotFoundException => (StatusCodes.Status404NotFound, exception.Message),
                ShoppingListItemNotFoundException => (StatusCodes.Status404NotFound, exception.Message),
                ShoppingListAccessDeniedException => (StatusCodes.Status403Forbidden, exception.Message),
                InvalidShoppingListItemException => (StatusCodes.Status400BadRequest, exception.Message),

                // Timeout exceptions
                RequestTimeoutException => (StatusCodes.Status504GatewayTimeout, exception.Message),

                // Standard exceptions
                ArgumentNullException => (StatusCodes.Status400BadRequest, "Required parameter is missing"),
                ArgumentException => (StatusCodes.Status400BadRequest, exception.Message),
                InvalidOperationException => (StatusCodes.Status400BadRequest, exception.Message),
                UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized access"),
                OperationCanceledException => (StatusCodes.Status499ClientClosedRequest, "Request was cancelled"),

                // Default: Internal Server Error
                _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
            };
        }
    }
}

using Homassy.API.Middleware;
using Homassy.API.Models;

namespace Homassy.API.Extensions;

public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app, IConfiguration configuration)
    {
        var options = configuration.GetSection("RequestLogging").Get<RequestLoggingOptions>() 
            ?? new RequestLoggingOptions();
        
        return app.UseMiddleware<RequestLoggingMiddleware>(options);
    }
}

using Homassy.API.Context;
using Homassy.API.Models.Kratos;
using Homassy.API.Services;
using Serilog;
using System.Security.Claims;

namespace Homassy.API.Middleware
{
    /// <summary>
    /// Middleware that validates Kratos sessions and populates authentication context.
    /// This replaces the JWT-based authentication with Kratos session validation.
    /// </summary>
    public class KratosSessionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IServiceProvider _serviceProvider;

        public KratosSessionMiddleware(RequestDelegate next, IServiceProvider serviceProvider)
        {
            _next = next;
            _serviceProvider = serviceProvider;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Skip Kratos auth for health endpoints and other public paths
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
            if (ShouldSkipAuthentication(path))
            {
                await _next(context);
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var kratosService = scope.ServiceProvider.GetRequiredService<IKratosService>();

            // Get session token from X-Session-Token header or ory_kratos_session cookie
            var sessionToken = context.Request.Headers["X-Session-Token"].FirstOrDefault();
            var cookieHeader = context.Request.Headers["Cookie"].FirstOrDefault();
            var kratosSessionCookie = KratosService.ExtractSessionCookie(cookieHeader);

            var session = await kratosService.GetSessionAsync(kratosSessionCookie, sessionToken, context.RequestAborted);

            if (session != null && session.Active)
            {
                // Build claims from Kratos session
                var claims = BuildClaims(session);
                var identity = new ClaimsIdentity(claims, "Kratos");
                context.User = new ClaimsPrincipal(identity);

                // Store session in HttpContext.Items for later use
                context.Items["KratosSession"] = session;

                Log.Debug($"Kratos session validated for identity {session.Identity.Id}");
            }

            await _next(context);
        }

        /// <summary>
        /// Determines if authentication should be skipped for a given path.
        /// </summary>
        private static bool ShouldSkipAuthentication(string path)
        {
            // Public endpoints that don't require authentication
            var publicPaths = new[]
            {
                "/health",
                "/openapi",
                "/api/v1/health",
                "/api/v1/version",
                "/api/v1/errorcodes"
            };

            foreach (var publicPath in publicPaths)
            {
                if (path.StartsWith(publicPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Builds ClaimsPrincipal claims from a Kratos session.
        /// </summary>
        private static List<Claim> BuildClaims(KratosSession session)
        {
            var claims = new List<Claim>
            {
                // Use Kratos identity ID as the primary identifier
                new Claim(ClaimTypes.NameIdentifier, session.Identity.Id),
                new Claim("kratos_session_id", session.Id),
            };

            // Add email claim if available
            if (!string.IsNullOrEmpty(session.Identity.Traits.Email))
            {
                claims.Add(new Claim(ClaimTypes.Email, session.Identity.Traits.Email));
            }

            // Add name claim if available
            if (!string.IsNullOrEmpty(session.Identity.Traits.Name))
            {
                claims.Add(new Claim(ClaimTypes.Name, session.Identity.Traits.Name));
            }

            // Add family ID if available
            if (session.Identity.Traits.FamilyId.HasValue)
            {
                claims.Add(new Claim("FamilyId", session.Identity.Traits.FamilyId.Value.ToString()));
            }

            // Add language claim
            if (!string.IsNullOrEmpty(session.Identity.Traits.DefaultLanguage))
            {
                claims.Add(new Claim("Language", session.Identity.Traits.DefaultLanguage));
            }

            // Add authentication method info
            if (session.AuthenticationMethods?.Count > 0)
            {
                var primaryMethod = session.AuthenticationMethods[0];
                claims.Add(new Claim("auth_method", primaryMethod.Method));
            }

            return claims;
        }
    }

    /// <summary>
    /// Extension methods for Kratos session middleware.
    /// </summary>
    public static class KratosSessionMiddlewareExtensions
    {
        /// <summary>
        /// Gets the Kratos session from HttpContext if available.
        /// </summary>
        public static KratosSession? GetKratosSession(this HttpContext context)
        {
            return context.Items.TryGetValue("KratosSession", out var session) 
                ? session as KratosSession 
                : null;
        }
    }
}

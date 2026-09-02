using Homassy.API.Context;
using Homassy.API.Models.Kratos;
using Homassy.API.Services;
using Microsoft.AspNetCore.Authorization;
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

        public KratosSessionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (ShouldSkipAuthentication(context))
            {
                await _next(context);
                return;
            }

            // Get session token from X-Session-Token header or ory_kratos_session cookie
            var sessionToken = context.Request.Headers["X-Session-Token"].FirstOrDefault();
            var cookieHeader = context.Request.Headers["Cookie"].FirstOrDefault();
            var kratosSessionCookie = KratosService.ExtractSessionCookie(cookieHeader);

            // Nothing to validate. whoami without a cookie or token can only answer 401, so
            // the round trip would cost a Kratos call to learn what is already known.
            if (string.IsNullOrEmpty(sessionToken) && string.IsNullOrEmpty(kratosSessionCookie))
            {
                await _next(context);
                return;
            }

            // context.RequestServices is already the scope for this request; creating another
            // one here built a second set of scoped services per request for nothing.
            var kratosService = context.RequestServices.GetRequiredService<IKratosService>();

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
        /// Whether the matched endpoint opts out of session validation.
        /// </summary>
        /// <remarks>
        /// Route-driven rather than string-driven. The previous version compared the request
        /// path against a hard-coded list ("/api/v1/health", ...) that never matched anything
        /// the application serves: controllers route as
        /// <c>api/v{version:apiVersion}/[controller]</c> with <c>SubstituteApiVersionInUrl</c>,
        /// so the real path is <c>/api/v1.0/health</c>. Every health probe therefore made a
        /// full whoami round trip to Kratos, and nothing failed loudly enough to notice.
        /// Reading <see cref="IAllowAnonymous"/> off the endpoint cannot drift out of sync with
        /// the routes, and it cannot match a path by accident the way a StartsWith prefix
        /// could ("/api/v1.0/healthy-secrets" starts with "/api/v1.0/health").
        ///
        /// Requires the middleware to run after <c>UseRouting</c>, which Program.cs calls
        /// explicitly.
        /// </remarks>
        private static bool ShouldSkipAuthentication(HttpContext context)
        {
            return context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null;
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

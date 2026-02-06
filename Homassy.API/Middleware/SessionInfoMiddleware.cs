using Homassy.API.Context;
using Homassy.API.Models.Kratos;
using System.Security.Claims;

namespace Homassy.API.Middleware
{
    public class SessionInfoMiddleware
    {
        private readonly RequestDelegate _next;

        public SessionInfoMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // Check if we have a Kratos session from KratosSessionMiddleware
                var kratosSession = context.GetKratosSession();
                
                if (kratosSession != null)
                {
                    // New Kratos-based authentication
                    SessionInfo.SetFromKratosSession(kratosSession);
                }
                else
                {
                    // Fallback: Check for legacy JWT claims (for backward compatibility during migration)
                    var publicIdClaim = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    var familyIdClaim = context.User?.FindFirst("FamilyId")?.Value;

                    if (publicIdClaim != null && Guid.TryParse(publicIdClaim, out var publicId))
                    {
                        var familyId = familyIdClaim != null && int.TryParse(familyIdClaim, out var fId) ? fId : (int?)null;
#pragma warning disable CS0618 // Type or member is obsolete
                        SessionInfo.SetUser(publicId, familyId);
#pragma warning restore CS0618
                    }
                }

                await _next(context);
            }
            finally
            {
                SessionInfo.Clear();
            }
        }
    }
}

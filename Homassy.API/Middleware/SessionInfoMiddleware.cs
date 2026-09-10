using Homassy.API.Context;
using Homassy.API.Functions;
using Homassy.API.Models.Kratos;
using Homassy.API.Services;
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
                    // New Kratos-based authentication. Resolved from the request's own scope —
                    // the Functions layer is scoped, and this is that scope.
                    var userFunctions = context.RequestServices.GetRequiredService<UserFunctions>();
                    SessionInfo.SetFromKratosSession(kratosSession, userFunctions);

                    StampLastSeen(context);
                }
                else
                {
                    // Fallback: Check for legacy JWT claims (for backward compatibility during migration)
                    var publicIdClaim = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    var familyIdClaim = context.User?.FindFirst("FamilyId")?.Value;

                    if (publicIdClaim != null && Guid.TryParse(publicIdClaim, out var publicId))
                    {
                        var familyId = familyIdClaim != null && int.TryParse(familyIdClaim, out var fId) ? fId : (int?)null;
                        var userFunctions = context.RequestServices.GetRequiredService<UserFunctions>();
#pragma warning disable CS0618 // Type or member is obsolete
                        SessionInfo.SetUser(publicId, userFunctions, familyId);
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

        /// <summary>
        /// Records that the caller was seen, for the away-delta feature (#127).
        /// </summary>
        /// <remarks>
        /// Here rather than in a middleware of its own, because this is where the session has just
        /// been resolved to a local user id - anywhere earlier has no user, and anywhere later would
        /// mean resolving it twice. The whole cost on the request path is one dictionary write:
        /// <see cref="LastSeenTracker"/> keeps the stamp in memory and
        /// <see cref="Services.Background.LastSeenFlushService"/> writes it in batches, which is
        /// exactly what #127 asks for instead of a row update per request.
        /// <para>
        /// A session that authenticated but does not resolve to a local user row (see
        /// <see cref="SessionInfo.SetFromKratosSession"/>'s "doesn't exist locally yet" branch) has
        /// nothing to stamp, so it is skipped rather than defaulted to anything.
        /// </para>
        /// </remarks>
        private static void StampLastSeen(HttpContext context)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                return;
            }

            context.RequestServices.GetRequiredService<LastSeenTracker>().Stamp(userId.Value, DateTime.UtcNow);
        }
    }
}

using Homassy.API.Context;
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
                var userIdClaim = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var email = context.User?.FindFirst(ClaimTypes.Email)?.Value;
                var familyIdClaim = context.User?.FindFirst("FamilyId")?.Value;

                if (userIdClaim != null && int.TryParse(userIdClaim, out var userId))
                {
                    var familyId = familyIdClaim != null && int.TryParse(familyIdClaim, out var fId) ? fId : (int?)null;
                    SessionInfo.SetUser(userId, email, familyId);
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

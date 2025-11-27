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
                var publicIdClaim = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var familyIdClaim = context.User?.FindFirst("FamilyId")?.Value;

                if (publicIdClaim != null && Guid.TryParse(publicIdClaim, out var publicId))
                {
                    var familyId = familyIdClaim != null && int.TryParse(familyIdClaim, out var fId) ? fId : (int?)null;
                    SessionInfo.SetUser(publicId, familyId);
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

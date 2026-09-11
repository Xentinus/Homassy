using Asp.Versioning;
using Homassy.API.Enums;
using Homassy.API.Functions;
using Homassy.API.Models.Common;
using Homassy.API.Models.Notification;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homassy.API.Controllers;

/// <summary>
/// The notification centre (#116): the caller's own inbox of everything
/// <c>Homassy.Notifications</c> has sent them, and its read state.
/// </summary>
/// <remarks>
/// A controller of its own rather than more endpoints on <see cref="UserController"/>, which
/// already carries the notification *preferences* (<c>GET/PUT /user/notification</c>). Those are
/// settings; these are content, cursor-paged and mutated per row, and hanging them off the same
/// prefix is how <c>NotificationsDrawer</c> came to mean "the preferences panel" in the first
/// place.
/// <para>
/// Every endpoint is scoped to the caller by <c>SessionInfo.GetUserId()</c> inside
/// <see cref="NotificationFunctions"/>, never by an id from the request - read state is personal,
/// and one family member must not be able to read or dismiss another's notifications.
/// </para>
/// </remarks>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly NotificationFunctions _notificationFunctions;

    public NotificationController(NotificationFunctions notificationFunctions)
    {
        _notificationFunctions = notificationFunctions;
    }

    /// <summary>
    /// One page of the caller's notifications, newest first, with their total unread count.
    /// </summary>
    /// <param name="cursor">Cursor from the previous page's <c>nextCursor</c>; omit for the first page.</param>
    /// <param name="pageSize">Rows to return, clamped to <see cref="NotificationFunctions.MaxPageSize"/>.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    [HttpGet]
    [MapToApiVersion(1.0)]
    [ProducesResponseType(typeof(ApiResponse<NotificationPage>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] string? cursor,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        try
        {
            var page = await _notificationFunctions.GetNotificationsAsync(cursor, pageSize, cancellationToken);
            return Ok(ApiResponse<NotificationPage>.SuccessResponse(page));
        }
        catch (ArgumentException)
        {
            // An undecodable cursor is the client's mistake, not a server fault — answer 400
            // rather than letting it surface as a 500 from the middleware. Mirrors how the
            // activity timeline treats the same input.
            return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
        }
    }

    /// <summary>
    /// The caller's unread notification count.
    /// </summary>
    /// <remarks>
    /// Separate from the list because it is fetched far more often than a page is: at boot, and
    /// again whenever a push arrives while the app is open. Fetching a page of rows to learn a
    /// number would be the wasteful way round.
    /// </remarks>
    [HttpGet("unread-count")]
    [MapToApiVersion(1.0)]
    [ProducesResponseType(typeof(ApiResponse<UnreadCountResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        var count = await _notificationFunctions.GetUnreadCountAsync(cancellationToken);
        return Ok(ApiResponse<UnreadCountResponse>.SuccessResponse(new UnreadCountResponse { UnreadCount = count }));
    }

    /// <summary>Marks one notification read. Idempotent.</summary>
    [HttpPost("{publicId:guid}/read")]
    [MapToApiVersion(1.0)]
    [ProducesResponseType(typeof(ApiResponse<UnreadCountResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(Guid publicId, CancellationToken cancellationToken)
    {
        var remaining = await _notificationFunctions.MarkReadAsync(publicId, cancellationToken);
        return Ok(ApiResponse<UnreadCountResponse>.SuccessResponse(new UnreadCountResponse { UnreadCount = remaining }));
    }

    /// <summary>Marks every unread notification of the caller's read.</summary>
    [HttpPost("read-all")]
    [MapToApiVersion(1.0)]
    [ProducesResponseType(typeof(ApiResponse<UnreadCountResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        await _notificationFunctions.MarkAllReadAsync(cancellationToken);
        return Ok(ApiResponse<UnreadCountResponse>.SuccessResponse(new UnreadCountResponse { UnreadCount = 0 }));
    }

    /// <summary>
    /// Dismisses one notification — what swipe-to-dismiss in the drawer sends. A soft delete, so
    /// the row stays out of the inbox without being erased before the retention sweep gets to it.
    /// </summary>
    [HttpDelete("{publicId:guid}")]
    [MapToApiVersion(1.0)]
    [ProducesResponseType(typeof(ApiResponse<UnreadCountResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Dismiss(Guid publicId, CancellationToken cancellationToken)
    {
        var remaining = await _notificationFunctions.DismissAsync(publicId, cancellationToken);
        return Ok(ApiResponse<UnreadCountResponse>.SuccessResponse(new UnreadCountResponse { UnreadCount = remaining }));
    }
}

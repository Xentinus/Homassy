using Asp.Versioning;
using Homassy.API.Enums;
using Homassy.API.Functions;
using Homassy.API.Models.Common;
using Homassy.API.Extensions;
using Homassy.API.Models.FamilyChat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homassy.API.Controllers;

/// <summary>
/// The family conversation (#144): its history, and the two writes that change it.
/// </summary>
/// <remarks>
/// A controller of its own rather than more endpoints on <see cref="FamilyController"/>, which
/// manages the family as a *thing* - its members, its share code, its picture. A conversation is
/// content: cursor-paged, written to constantly, and paired with a realtime hub. Hanging it off
/// the same prefix would make <c>/family</c> mean two unrelated jobs.
/// <para>
/// No endpoint here takes a family id. <see cref="FamilyChatFunctions"/> resolves the caller's own
/// family from the session, so there is no id a caller could substitute for somebody else's.
/// </para>
/// <para>
/// The realtime side lives on <c>/hubs/family-chat</c>; these endpoints are what a client uses
/// when the socket is down, and what pages back through history.
/// </para>
/// </remarks>
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize]
public class FamilyChatController : ControllerBase
{
    private readonly FamilyChatFunctions _chatFunctions;

    public FamilyChatController(FamilyChatFunctions chatFunctions)
    {
        _chatFunctions = chatFunctions;
    }

    /// <summary>
    /// One page of the caller's family conversation, newest first.
    /// </summary>
    /// <param name="before">Cursor from the previous page's <c>nextCursor</c>; omit for the newest page.</param>
    /// <param name="limit">Messages to return, clamped to <see cref="FamilyChatFunctions.MaxPageSize"/>.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    [HttpGet("messages")]
    [MapToApiVersion(1.0)]
    [ProducesResponseType(typeof(ApiResponse<FamilyChatPage>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMessages(
        [FromQuery] string? before,
        [FromQuery] int limit,
        CancellationToken cancellationToken)
    {
        var page = await _chatFunctions.GetMessagesAsync(before, limit, cancellationToken);
        return Ok(ApiResponse<FamilyChatPage>.SuccessResponse(page));
    }

    /// <summary>Posts a text message to the caller's family conversation.</summary>
    [HttpPost("messages")]
    [MapToApiVersion(1.0)]
    [ProducesResponseType(typeof(ApiResponse<FamilyChatMessageInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> SendMessage(
        [FromBody] SendFamilyChatMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
        }

        var message = await _chatFunctions.SendMessageAsync(request, cancellationToken);
        return Ok(ApiResponse<FamilyChatMessageInfo>.SuccessResponse(message));
    }

    /// <summary>Posts a picture to the caller's family conversation (#147).</summary>
    /// <remarks>
    /// The message and its bytes commit together and the broadcast follows the commit, so no
    /// client ever renders a message whose image 404s.
    /// </remarks>
    [HttpPost("messages/image")]
    [MapToApiVersion(1.0)]
    [ProducesResponseType(typeof(ApiResponse<FamilyChatMessageInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> SendImageMessage(
        [FromBody] SendFamilyChatImageRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
        }

        var message = await _chatFunctions.SendImageMessageAsync(request, cancellationToken);
        return Ok(ApiResponse<FamilyChatMessageInfo>.SuccessResponse(message));
    }

    /// <summary>
    /// Serves an image message's picture as image bytes (#147).
    /// </summary>
    /// <remarks>
    /// Behaves exactly like the avatar and product-image endpoints - ETag, a year-long private
    /// cache lifetime, `?size=thumb|full`, `?v=` for versioning and `Vary: Accept` for the WebP /
    /// JPEG negotiation. Read `UserController`'s notes for why those rules are what they are.
    /// <para>
    /// Addressed by the message's public id: whether you may see the picture is the same question
    /// as whether you may see the message, so one id answers both. A message in another family is
    /// a 404 here.
    /// </para>
    /// </remarks>
    [HttpGet("messages/{publicId:guid}/image")]
    [MapToApiVersion(1.0)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMessageImage(
        Guid publicId,
        [FromQuery] ImageVariant size = ImageVariant.Thumb,
        CancellationToken cancellationToken = default)
    {
        var image = await _chatFunctions.GetMessageImageAsync(publicId, size, this.AcceptsWebp(), cancellationToken);

        if (image == null)
        {
            return NotFound(ApiResponse.ErrorResponse(ErrorCodes.FamilyChatMessageNotFound));
        }

        return this.CacheableImage(image);
    }

    /// <summary>
    /// Deletes one of the caller's own messages (a soft delete), then tells the family.
    /// </summary>
    /// <remarks>
    /// Own messages only. Somebody else's answers 404, indistinguishably from a message that does
    /// not exist - see <c>FamilyChatMessageNotFoundException</c>.
    /// </remarks>
    [HttpDelete("messages/{publicId:guid}")]
    [MapToApiVersion(1.0)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMessage(Guid publicId, CancellationToken cancellationToken)
    {
        await _chatFunctions.DeleteMessageAsync(publicId, cancellationToken);
        return Ok(ApiResponse.SuccessResponse());
    }
}

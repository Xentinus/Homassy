using Asp.Versioning;
using Homassy.API.Enums;
using Homassy.API.Functions;
using Homassy.API.Models;
using Homassy.API.Models.Common;
using Homassy.API.Models.PushNotification;
using Homassy.API.Models.User;
using Homassy.API.Models.ImageUpload;
using Homassy.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Homassy.API.Controllers
{
    /// <summary>
    /// User profile and settings management endpoints.
    /// </summary>
    [ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IImageProcessingService _imageProcessingService;
        private readonly IProgressTrackerService _progressTrackerService;
        private readonly IWebPushService _webPushService;
        private readonly IKratosService _kratosService;

        public UserController(
            IImageProcessingService imageProcessingService, 
            IProgressTrackerService progressTrackerService, 
            IWebPushService webPushService,
            IKratosService kratosService)
        {
            _imageProcessingService = imageProcessingService;
            _progressTrackerService = progressTrackerService;
            _webPushService = webPushService;
            _kratosService = kratosService;
        }

        /// <summary>
        /// Gets the current user's profile information.
        /// </summary>
        [HttpGet("profile")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<UserProfileResponse>), StatusCodes.Status200OK)]
        public IActionResult GetProfile()
        {
            var profileResponse = new UserFunctions().GetProfileAsync();
            return Ok(ApiResponse<UserProfileResponse>.SuccessResponse(profileResponse));
        }

        /// <summary>
        /// Updates the current user's settings and preferences.
        /// </summary>
        [HttpPut("settings")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateSettings([FromBody] UpdateUserSettingsRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            await new UserFunctions().UpdateUserProfileAsync(request, _kratosService, cancellationToken);
            return Ok(ApiResponse.SuccessResponse());
        }

        /// <summary>
        /// Uploads and processes a new profile picture for the current user (synchronous - legacy).
        /// </summary>
        [HttpPost("profile-picture")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<UserProfileImageInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UploadProfilePicture([FromBody] UploadUserProfileImageRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var imageInfo = await new ImageFunctions(_imageProcessingService, _kratosService).UploadUserProfileImageAsync(request, null, cancellationToken);
            return Ok(ApiResponse<UserProfileImageInfo>.SuccessResponse(imageInfo));
        }

        /// <summary>
        /// Uploads and processes a new profile picture asynchronously with progress tracking.
        /// </summary>
        [HttpPost("profile-picture/upload-async")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<UploadJobResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public IActionResult UploadProfilePictureAsync([FromBody] UploadUserProfileImageRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var jobId = _progressTrackerService.CreateJob();

            // Start background task
            _ = Task.Run(async () =>
            {
                try
                {
                    var cancellationToken = _progressTrackerService.GetCancellationToken(jobId);

                    var progress = new Progress<ProgressInfo>(info =>
                    {
                        _progressTrackerService.UpdateProgress(jobId, info.Percentage, info.Stage, info.Status);
                    });

                    await new ImageFunctions(_imageProcessingService, _kratosService).UploadUserProfileImageAsync(request, progress, cancellationToken);
                    
                    _progressTrackerService.CompleteJob(jobId);
                }
                catch (OperationCanceledException)
                {
                    _progressTrackerService.CancelJob(jobId);
                    Log.Information($"Profile picture upload cancelled for job {jobId}");
                }
                catch (Exception ex)
                {
                    _progressTrackerService.FailJob(jobId, ex.Message);
                    Log.Error(ex, $"Failed to upload profile picture for job {jobId}");
                }
            });

            return Ok(ApiResponse<UploadJobResponse>.SuccessResponse(new UploadJobResponse { JobId = jobId }));
        }

        /// <summary>
        /// Deletes the current user's profile picture.
        /// </summary>
        [HttpDelete("profile-picture")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteProfilePicture(CancellationToken cancellationToken)
        {
            await new ImageFunctions(_imageProcessingService, _kratosService).DeleteUserProfileImageAsync(cancellationToken);
            return Ok(ApiResponse.SuccessResponse());
        }

        /// <summary>
        /// Gets the current user's notification preferences.
        /// </summary>
        [HttpGet("notification")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<NotificationPreferencesResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public IActionResult GetNotificationPreferences()
        {
            var preferencesResponse = new UserFunctions().GetNotificationPreferencesAsync();
            return Ok(ApiResponse<NotificationPreferencesResponse>.SuccessResponse(preferencesResponse));
        }

        /// <summary>
        /// Updates the current user's notification preferences.
        /// </summary>
        [HttpPut("notification")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateNotificationPreferences([FromBody] UpdateNotificationPreferencesRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            await new UserFunctions().UpdateNotificationPreferencesAsync(request, cancellationToken);
            return Ok(ApiResponse.SuccessResponse());
        }

        /// <summary>
        /// Gets multiple users by their public IDs.
        /// </summary>
        /// <param name="publicIds">Comma-separated list of user public IDs</param>
        [HttpGet("bulk")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<List<UserInfo>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public IActionResult GetUsersByPublicIds([FromQuery] string publicIds)
        {
            if (string.IsNullOrWhiteSpace(publicIds))
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            // Parse comma-separated GUIDs
            var guidList = publicIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => Guid.TryParse(s.Trim(), out var guid) ? guid : (Guid?)null)
                .Where(g => g.HasValue)
                .Select(g => g!.Value)
                .ToList();

            if (guidList.Count == 0)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest, "No valid user IDs provided"));
            }

            if (guidList.Count > 100)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest, "Maximum 100 user IDs allowed per request"));
            }

            var users = new UserFunctions().GetUsersByPublicIds(guidList);
            return Ok(ApiResponse<List<UserInfo>>.SuccessResponse(users));
        }

        /// <summary>
        /// Gets paginated activity history with optional filtering.
        /// </summary>
        [HttpGet("activities")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<Models.Activity.ActivityInfo>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetActivities(
            [FromQuery] int? activityType,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] Guid? userPublicId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] bool returnAll = false,
            CancellationToken cancellationToken = default)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            var request = new Models.Activity.GetActivitiesRequest
            {
                ActivityType = activityType.HasValue ? (Enums.ActivityType)activityType.Value : null,
                StartDate = startDate,
                EndDate = endDate,
                UserPublicId = userPublicId,
                PageNumber = pageNumber,
                PageSize = pageSize,
                ReturnAll = returnAll
            };

            var result = await new ActivityFunctions().GetActivitiesAsync(request, cancellationToken);
            return Ok(ApiResponse<PagedResult<Models.Activity.ActivityInfo>>.SuccessResponse(result));
        }

        /// <summary>
        /// Gets the VAPID public key for push notification subscription.
        /// </summary>
        [HttpGet("push/vapid-key")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse<VapidPublicKeyResponse>), StatusCodes.Status200OK)]
        public IActionResult GetVapidPublicKey()
        {
            var publicKey = _webPushService.GetVapidPublicKey();
            return Ok(ApiResponse<VapidPublicKeyResponse>.SuccessResponse(
                new VapidPublicKeyResponse { PublicKey = publicKey }));
        }

        /// <summary>
        /// Subscribes the current device for push notifications.
        /// </summary>
        [HttpPost("push/subscribe")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SubscribePush([FromBody] CreatePushSubscriptionRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            await new PushNotificationFunctions().SubscribeAsync(
                request.Endpoint, request.P256dh, request.Auth, request.UserAgent, cancellationToken);
            return Ok(ApiResponse.SuccessResponse());
        }

        /// <summary>
        /// Unsubscribes the current device from push notifications.
        /// </summary>
        [HttpPost("push/unsubscribe")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UnsubscribePush([FromBody] UnsubscribePushRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest));
            }

            await new PushNotificationFunctions().UnsubscribeAsync(request.Endpoint, cancellationToken);
            return Ok(ApiResponse.SuccessResponse());
        }

        /// <summary>
        /// Sends a test push notification to the current device.
        /// </summary>
        [HttpPost("push/test")]
        [MapToApiVersion(1.0)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SendTestPushNotification(CancellationToken cancellationToken)
        {
            try
            {
                await new PushNotificationFunctions().SendTestNotificationAsync(_webPushService, cancellationToken);
                return Ok(ApiResponse.SuccessResponse());
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest, ex.Message));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse.ErrorResponse(ErrorCodes.ValidationInvalidRequest, ex.Message));
            }
        }
    }
}
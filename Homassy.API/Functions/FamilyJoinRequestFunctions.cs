using Homassy.API.Context;
using Homassy.API.Entities.Family;
using Homassy.API.Enums;
using Homassy.API.Exceptions;
using Homassy.API.Models.Family;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Homassy.API.Functions
{
    /// <summary>
    /// Business logic for family join requests. Joining a family requires approval from an
    /// existing member; a user may only have one pending request at a time.
    /// </summary>
    public class FamilyJoinRequestFunctions
    {
        /// <summary>
        /// Creates a pending request for the current user to join the family identified by the share code.
        /// </summary>
        public async Task<MyJoinRequestResponse> CreateJoinRequestAsync(JoinFamilyRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UnauthorizedException("Invalid authentication", ErrorCodes.AuthUnauthorized);
            }

            var user = new UserFunctions().GetUserById(userId.Value);
            if (user == null)
            {
                Log.Warning($"User not found for userId {userId.Value}");
                throw new UserNotFoundException("User not found", ErrorCodes.UserNotFound);
            }

            if (user.FamilyId.HasValue)
            {
                throw new BadRequestException("You are already a member of a family. Please leave your current family first.", ErrorCodes.FamilyAlreadyMember);
            }

            if (string.IsNullOrWhiteSpace(request.ShareCode))
            {
                throw new BadRequestException("Share code is required", ErrorCodes.ValidationShareCodeRequired);
            }

            var family = new FamilyFunctions().GetFamilyByShareCode(request.ShareCode);
            if (family == null)
            {
                throw new FamilyNotFoundException("Family not found with the provided share code", ErrorCodes.FamilyInvalidShareCode);
            }

            var context = new HomassyDbContext();

            var hasPending = await context.FamilyJoinRequests
                .AnyAsync(r => r.UserId == userId.Value && r.Status == FamilyJoinRequestStatus.Pending, cancellationToken);
            if (hasPending)
            {
                throw new BadRequestException("You already have a pending join request.", ErrorCodes.FamilyJoinRequestPending);
            }

            var joinRequest = new FamilyJoinRequest
            {
                UserId = userId.Value,
                FamilyId = family.Id,
                Status = FamilyJoinRequestStatus.Pending,
                RequestedAt = DateTime.UtcNow
            };

            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                context.FamilyJoinRequests.Add(joinRequest);
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId.Value} created join request {joinRequest.Id} for family {family.Id}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Error creating join request for user {userId.Value}");
                throw;
            }

            // Record activity (drives the family-member push notification).
            var requesterName = await GetDisplayNameAsync(context, userId.Value, user.Name, cancellationToken);
            await RecordActivitySafelyAsync(userId.Value, family.Id, ActivityType.FamilyJoinRequestCreate, joinRequest.Id, requesterName, cancellationToken);

            return new MyJoinRequestResponse
            {
                PublicId = joinRequest.PublicId,
                FamilyName = family.Name,
                Status = joinRequest.Status.ToString(),
                RequestedAt = joinRequest.RequestedAt
            };
        }

        /// <summary>
        /// Returns the current user's pending join request, or null if they have none.
        /// </summary>
        public MyJoinRequestResponse? GetMyJoinRequest()
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                throw new UnauthorizedException("Invalid authentication", ErrorCodes.AuthUnauthorized);
            }

            var context = new HomassyDbContext();
            var request = context.FamilyJoinRequests
                .Where(r => r.UserId == userId.Value && r.Status == FamilyJoinRequestStatus.Pending)
                .OrderByDescending(r => r.RequestedAt)
                .FirstOrDefault();

            if (request == null)
            {
                return null;
            }

            var family = new FamilyFunctions().GetFamilyById(request.FamilyId);

            return new MyJoinRequestResponse
            {
                PublicId = request.PublicId,
                FamilyName = family?.Name ?? string.Empty,
                Status = request.Status.ToString(),
                RequestedAt = request.RequestedAt
            };
        }

        /// <summary>
        /// Withdraws the current user's pending join request.
        /// </summary>
        public async Task CancelMyJoinRequestAsync(CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                throw new UnauthorizedException("Invalid authentication", ErrorCodes.AuthUnauthorized);
            }

            var context = new HomassyDbContext();
            var request = await context.FamilyJoinRequests
                .FirstOrDefaultAsync(r => r.UserId == userId.Value && r.Status == FamilyJoinRequestStatus.Pending, cancellationToken);

            if (request == null)
            {
                throw new BadRequestException("No pending join request to withdraw.", ErrorCodes.FamilyJoinRequestNotFound);
            }

            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                request.Status = FamilyJoinRequestStatus.Cancelled;
                request.RespondedAt = DateTime.UtcNow;
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId.Value} withdrew join request {request.Id}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Error withdrawing join request for user {userId.Value}");
                throw;
            }
        }

        /// <summary>
        /// Lists the pending join requests for the current user's family.
        /// </summary>
        public List<FamilyJoinRequestResponse> GetFamilyJoinRequests()
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                throw new UnauthorizedException("Invalid authentication", ErrorCodes.AuthUnauthorized);
            }

            var user = new UserFunctions().GetUserById(userId.Value);
            if (user == null || !user.FamilyId.HasValue)
            {
                throw new FamilyNotFoundException("You are not a member of any family", ErrorCodes.FamilyNotMember);
            }

            var context = new HomassyDbContext();
            return context.FamilyJoinRequests
                .Include(r => r.User)
                    .ThenInclude(u => u!.Profile)
                .Where(r => r.FamilyId == user.FamilyId.Value && r.Status == FamilyJoinRequestStatus.Pending)
                .OrderBy(r => r.RequestedAt)
                .Select(r => new FamilyJoinRequestResponse
                {
                    PublicId = r.PublicId,
                    Name = r.User!.Name,
                    DisplayName = r.User.Profile != null ? r.User.Profile.DisplayName : r.User.Name,
                    ProfilePictureBase64 = r.User.Profile != null ? r.User.Profile.ProfilePictureBase64 : null,
                    RequestedAt = r.RequestedAt
                })
                .ToList();
        }

        /// <summary>
        /// Approves a pending join request, adding the requester to the family.
        /// </summary>
        public async Task ApproveJoinRequestAsync(Guid publicId, CancellationToken cancellationToken = default)
        {
            var (approverId, request, context) = await LoadActionableRequestAsync(publicId, cancellationToken);

            var requester = await context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
            if (requester == null)
            {
                throw new UserNotFoundException("Requesting user not found", ErrorCodes.UserNotFound);
            }

            if (requester.FamilyId.HasValue)
            {
                // Already a member of a family – nothing to approve into.
                throw new BadRequestException("The requesting user already belongs to a family.", ErrorCodes.FamilyAlreadyMember);
            }

            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                requester.FamilyId = request.FamilyId;
                request.Status = FamilyJoinRequestStatus.Approved;
                request.RespondedAt = DateTime.UtcNow;
                request.RespondedByUserId = approverId;

                context.Users.Update(requester);
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {approverId} approved join request {request.Id}; user {requester.Id} joined family {request.FamilyId}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Error approving join request {request.Id}");
                throw;
            }

            var requesterName = await GetDisplayNameAsync(context, requester.Id, requester.Name, cancellationToken);
            await RecordActivitySafelyAsync(approverId, request.FamilyId, ActivityType.FamilyJoinRequestApprove, request.Id, requesterName, cancellationToken);
        }

        /// <summary>
        /// Declines a pending join request.
        /// </summary>
        public async Task RejectJoinRequestAsync(Guid publicId, CancellationToken cancellationToken = default)
        {
            var (approverId, request, context) = await LoadActionableRequestAsync(publicId, cancellationToken);

            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                request.Status = FamilyJoinRequestStatus.Rejected;
                request.RespondedAt = DateTime.UtcNow;
                request.RespondedByUserId = approverId;
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {approverId} declined join request {request.Id}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Error declining join request {request.Id}");
                throw;
            }

            var requesterName = await GetDisplayNameAsync(context, request.UserId, null, cancellationToken);
            await RecordActivitySafelyAsync(approverId, request.FamilyId, ActivityType.FamilyJoinRequestDecline, request.Id, requesterName, cancellationToken);
        }

        /// <summary>
        /// Loads a pending request by public id and verifies the current user may act on it
        /// (must be a member of the request's family). Returns the approver id, the tracked
        /// request, and its context.
        /// </summary>
        private async Task<(int approverId, FamilyJoinRequest request, HomassyDbContext context)> LoadActionableRequestAsync(
            Guid publicId, CancellationToken cancellationToken)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                throw new UnauthorizedException("Invalid authentication", ErrorCodes.AuthUnauthorized);
            }

            var approver = new UserFunctions().GetUserById(userId.Value);
            if (approver == null || !approver.FamilyId.HasValue)
            {
                throw new FamilyNotFoundException("You are not a member of any family", ErrorCodes.FamilyNotMember);
            }

            var context = new HomassyDbContext();
            var request = await context.FamilyJoinRequests
                .FirstOrDefaultAsync(r => r.PublicId == publicId, cancellationToken);

            if (request == null)
            {
                throw new FamilyNotFoundException("Join request not found", ErrorCodes.FamilyJoinRequestNotFound);
            }

            if (request.FamilyId != approver.FamilyId.Value)
            {
                throw new BadRequestException("This join request does not belong to your family.", ErrorCodes.FamilyJoinRequestAccessDenied);
            }

            if (request.Status != FamilyJoinRequestStatus.Pending)
            {
                throw new BadRequestException("This join request has already been handled.", ErrorCodes.FamilyJoinRequestNotFound);
            }

            return (userId.Value, request, context);
        }

        private static async Task<string> GetDisplayNameAsync(HomassyDbContext context, int userId, string? fallback, CancellationToken cancellationToken)
        {
            var displayName = await context.UserProfiles
                .Where(p => p.UserId == userId)
                .Select(p => p.DisplayName)
                .FirstOrDefaultAsync(cancellationToken);

            return !string.IsNullOrWhiteSpace(displayName) ? displayName : (fallback ?? string.Empty);
        }

        private static async Task RecordActivitySafelyAsync(
            int userId, int familyId, ActivityType activityType, int recordId, string recordName, CancellationToken cancellationToken)
        {
            try
            {
                await new ActivityFunctions().RecordActivityAsync(
                    userId, familyId, activityType, recordId, recordName, null, null, cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to record {activityType} activity for join request {recordId}");
            }
        }
    }
}

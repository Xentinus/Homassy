using Homassy.API.Constants;
using Homassy.API.Context;
using Homassy.API.Entities.Common;
using Homassy.API.Entities.User;
using Homassy.API.Enums;
using Homassy.API.Exceptions;
using Homassy.API.Models;
using Homassy.API.Models.ImageUpload;
using Homassy.API.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Homassy.API.Functions
{
    public class ImageFunctions
    {
        private readonly FunctionsRuntime _runtime;
        private readonly IDbContextFactory<HomassyDbContext> _contextFactory;
        private readonly IImageProcessingService _imageProcessingService;
        private readonly IKratosService? _kratosService;

        public ImageFunctions(
            FunctionsRuntime runtime,
            IImageProcessingService imageProcessingService,
            IKratosService? kratosService = null)
        {
            _runtime = runtime;
            _contextFactory = runtime.ContextFactory;
            _imageProcessingService = imageProcessingService;
            _kratosService = kratosService;
        }

        public async Task<ProductImageInfo> UploadProductImageAsync(UploadProductImageRequest request, IProgress<ProgressInfo>? progress = null, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var productFunctions = new ProductFunctions(_runtime);
            var product = productFunctions.GetProductByPublicId(request.ProductPublicId);
            if (product == null)
            {
                throw new ProductNotFoundException();
            }

            var options = new ImageProcessingOptions
            {
                MaxWidth = 800,
                MaxHeight = 800,
                MinWidth = 50,
                MinHeight = 50,
                MaxFileSizeBytes = 5 * 1024 * 1024,
                JpegQuality = 80,
                AllowedFormats = [ImageFormat.Jpeg, ImageFormat.Png, ImageFormat.WebP]
            };

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new ProgressInfo { Percentage = 5, Stage = ProgressStage.Validating, Status = ProgressStatus.InProgress, UpdatedAt = DateTime.UtcNow });

            var validationResult = _imageProcessingService.ValidateImage(request.ImageBase64, options);
            if (!validationResult.IsValid)
            {
                throw new BadRequestException($"Image validation failed: {validationResult.ErrorMessage}");
            }

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new ProgressInfo { Percentage = 15, Stage = ProgressStage.Compressing, Status = ProgressStatus.InProgress, UpdatedAt = DateTime.UtcNow });

            var processedImage = await _imageProcessingService.ProcessImageAsync(request.ImageBase64, options, cancellationToken);
            if (processedImage == null)
            {
                throw new BadRequestException("Failed to process image");
            }

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new ProgressInfo { Percentage = 60, Stage = ProgressStage.Uploading, Status = ProgressStatus.InProgress, UpdatedAt = DateTime.UtcNow });

            using var context = _contextFactory.CreateDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new ProgressInfo { Percentage = 80, Stage = ProgressStage.Processing, Status = ProgressStatus.InProgress, UpdatedAt = DateTime.UtcNow });

                var trackedProduct = await context.Products.FirstOrDefaultAsync(p => p.Id == product.Id, cancellationToken);
                if (trackedProduct == null)
                {
                    throw new ProductNotFoundException();
                }

                trackedProduct.ProductPictureBase64 = processedImage.Base64;
                
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new ProgressInfo { Percentage = 90, Stage = ProgressStage.Saving, Status = ProgressStatus.InProgress, UpdatedAt = DateTime.UtcNow });
                
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId} uploaded image for product {product.Id} (PublicId: {product.PublicId})");

                // Record activity
                try
                {
                    var familyId = SessionInfo.GetFamilyId();
                    await new ActivityFunctions(_contextFactory).RecordActivityAsync(
                        userId.Value,
                        familyId,
                        Enums.ActivityType.ProductPhotoUpload,
                        product.Id,
                        product.Name,
                        null,
                        null,
                        cancellationToken
                    );
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Failed to record ProductPhotoUpload activity for product {product.Name}");
                }

                return new ProductImageInfo
                {
                    ProductPublicId = product.PublicId,
                    ImageBase64 = processedImage.Base64,
                    Format = processedImage.Format,
                    Width = processedImage.Width,
                    Height = processedImage.Height,
                    FileSizeBytes = processedImage.FileSizeBytes
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to upload image for product {request.ProductPublicId}");
                throw;
            }
        }

        public async Task DeleteProductImageAsync(Guid productPublicId, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var productFunctions = new ProductFunctions(_runtime);
            var product = productFunctions.GetProductByPublicId(productPublicId);
            if (product == null)
            {
                throw new ProductNotFoundException();
            }

            using var context = _contextFactory.CreateDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var trackedProduct = await context.Products.FirstOrDefaultAsync(p => p.Id == product.Id, cancellationToken);
                if (trackedProduct == null)
                {
                    throw new ProductNotFoundException();
                }

                trackedProduct.ProductPictureBase64 = null;
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId} deleted image for product {product.Id} (PublicId: {product.PublicId})");

                // Record activity
                try
                {
                    var familyId = SessionInfo.GetFamilyId();
                    await new ActivityFunctions(_contextFactory).RecordActivityAsync(
                        userId.Value,
                        familyId,
                        Enums.ActivityType.ProductPhotoDelete,
                        product.Id,
                        product.Name,
                        null,
                        null,
                        cancellationToken
                    );
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Failed to record ProductPhotoDelete activity for product {product.Name}");
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to delete image for product {productPublicId}");
                throw;
            }
        }

        public async Task<UserProfileImageInfo> UploadUserProfileImageAsync(UploadUserProfileImageRequest request, IProgress<ProgressInfo>? progress = null, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var options = new ImageProcessingOptions
            {
                MaxWidth = 400,
                MaxHeight = 400,
                MinWidth = 50,
                MinHeight = 50,
                MaxFileSizeBytes = 2 * 1024 * 1024,
                JpegQuality = 85,
                AllowedFormats = [ImageFormat.Jpeg, ImageFormat.Png, ImageFormat.WebP]
            };

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new ProgressInfo { Percentage = 5, Stage = ProgressStage.Validating, Status = ProgressStatus.InProgress, UpdatedAt = DateTime.UtcNow });

            var validationResult = _imageProcessingService.ValidateImage(request.ImageBase64, options);
            if (!validationResult.IsValid)
            {
                throw new BadRequestException($"Image validation failed: {validationResult.ErrorMessage}");
            }

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new ProgressInfo { Percentage = 15, Stage = ProgressStage.Compressing, Status = ProgressStatus.InProgress, UpdatedAt = DateTime.UtcNow });

            var processedImage = await _imageProcessingService.ProcessImageAsync(request.ImageBase64, options, cancellationToken);
            if (processedImage == null)
            {
                throw new BadRequestException("Failed to process image");
            }

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new ProgressInfo { Percentage = 60, Stage = ProgressStage.Uploading, Status = ProgressStatus.InProgress, UpdatedAt = DateTime.UtcNow });

            using var context = _contextFactory.CreateDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new ProgressInfo { Percentage = 80, Stage = ProgressStage.Processing, Status = ProgressStatus.InProgress, UpdatedAt = DateTime.UtcNow });

                var profile = await context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
                if (profile == null)
                {
                    throw new UserNotFoundException($"UserProfile not found for user {userId}");
                }

                var picture = await context.UserProfilePictures.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
                var version = ContentVersion(processedImage.Data);
                var thumbnail = _imageProcessingService.CreateSquareThumbnail(processedImage.Data, ImageSizes.AvatarThumbnail);

                if (picture == null)
                {
                    picture = new UserProfilePicture
                    {
                        UserId = userId.Value,
                        Data = processedImage.Data,
                        Version = version
                    };
                    context.UserProfilePictures.Add(picture);
                }

                Apply(picture, processedImage, thumbnail, version);
                profile.ProfilePictureVersion = version;

                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new ProgressInfo { Percentage = 90, Stage = ProgressStage.Saving, Status = ProgressStatus.InProgress, UpdatedAt = DateTime.UtcNow });

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId} uploaded profile picture (version {version})");

                // Sync to Kratos (non-blocking)
                if (_kratosService != null)
                {
                    await SyncProfileTraitsToKratosAsync(userId.Value, cancellationToken);
                }

                return new UserProfileImageInfo
                {
                    ProfilePictureUrl = MediaUrls.ProfilePicture(SessionInfo.GetPublicId() ?? Guid.Empty, version)!,
                    Format = processedImage.Format,
                    Width = processedImage.Width,
                    Height = processedImage.Height,
                    FileSizeBytes = processedImage.FileSizeBytes
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to upload profile picture for user {userId}");
                throw;
            }
        }

        public async Task DeleteUserProfileImageAsync(CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            using var context = _contextFactory.CreateDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var profile = await context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
                if (profile == null)
                {
                    throw new UserNotFoundException($"UserProfile not found for user {userId}");
                }

                var picture = await context.UserProfilePictures.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
                if (picture == null && profile.ProfilePictureVersion == null)
                {
                    throw new BadRequestException("No profile picture to delete");
                }

                if (picture != null)
                {
                    context.UserProfilePictures.Remove(picture);
                }

                profile.ProfilePictureVersion = null;
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId} deleted profile picture");

                // Sync to Kratos (non-blocking)
                if (_kratosService != null)
                {
                    await SyncProfileTraitsToKratosAsync(userId.Value, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to delete profile picture for user {userId}");
                throw;
            }
        }

        /// <summary>
        /// Serves one rendition of a user's avatar, or null when they have no picture.
        /// </summary>
        /// <param name="userPublicId">The user whose avatar is being asked for.</param>
        /// <param name="variant">Which stored rendition to answer with.</param>
        /// <param name="acceptsWebp">
        /// Whether the requesting client accepts <c>image/webp</c>. Thumbnails are stored as WebP;
        /// a client that does not accept it gets a JPEG transcode instead.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task<StoredImageResponse?> GetUserProfilePictureAsync(
            Guid userPublicId,
            ImageVariant variant,
            bool acceptsWebp,
            CancellationToken cancellationToken = default)
        {
            var user = new UserFunctions(_contextFactory).GetUserByPublicId(userPublicId);
            if (user == null)
            {
                return null;
            }

            using var context = _contextFactory.CreateForReading();
            var picture = await context.UserProfilePictures
                .FirstOrDefaultAsync(p => p.UserId == user.Id, cancellationToken);

            if (picture == null)
            {
                return null;
            }

            if (variant == ImageVariant.Thumb && picture.ThumbnailData == null)
            {
                await BackfillThumbnailAsync(picture.Id, ImageSizes.AvatarThumbnail, cancellationToken);
                picture = await context.UserProfilePictures
                    .FirstOrDefaultAsync(p => p.UserId == user.Id, cancellationToken) ?? picture;
            }

            return Render(picture, variant, acceptsWebp);
        }

        /// <summary>
        /// Generates and stores the thumbnail a row is missing, for pictures uploaded before
        /// thumbnails existed.
        /// </summary>
        /// <remarks>
        /// A separate write context, deliberately: the caller is on a read context, and this is a
        /// cache fill, not part of answering the request. If it fails the request still succeeds —
        /// <see cref="Render"/> falls back to the full-size bytes.
        /// </remarks>
        private async Task BackfillThumbnailAsync(int pictureId, int size, CancellationToken cancellationToken)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                var picture = await context.UserProfilePictures.FirstOrDefaultAsync(p => p.Id == pictureId, cancellationToken);
                if (picture == null || picture.ThumbnailData != null)
                {
                    return;
                }

                var thumbnail = _imageProcessingService.CreateSquareThumbnail(picture.Data, size);
                if (thumbnail == null)
                {
                    return;
                }

                picture.ThumbnailData = thumbnail.Data;
                picture.ThumbnailFormat = thumbnail.Format;
                await context.SaveChangesAsync(cancellationToken);

                Log.Information("Backfilled a {Size}px thumbnail for user profile picture {PictureId}", size, pictureId);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to backfill the thumbnail for user profile picture {PictureId}", pictureId);
            }
        }

        /// <summary>
        /// Picks the bytes to answer with, negotiating the encoding, and builds the ETag.
        /// </summary>
        private StoredImageResponse? Render(StoredImageEntity picture, ImageVariant variant, bool acceptsWebp)
        {
            var wantsThumb = variant == ImageVariant.Thumb && picture.ThumbnailData != null;
            var data = wantsThumb ? picture.ThumbnailData! : picture.Data;
            var format = wantsThumb ? picture.ThumbnailFormat : picture.Format;
            // A row whose thumbnail could not be generated answers with the full image. The ETag
            // has to say so, or a client that cached the fallback would keep it after a
            // successful backfill.
            var served = wantsThumb ? "thumb" : "full";

            if (format == ImageFormat.WebP && !acceptsWebp)
            {
                var jpeg = _imageProcessingService.TranscodeToJpeg(data);
                if (jpeg == null)
                {
                    return null;
                }

                data = jpeg.Data;
                format = jpeg.Format;
                served += "-jpeg";
            }

            return new StoredImageResponse
            {
                Data = data,
                ContentType = ContentTypeOf(format),
                ETag = $"\"{picture.Version}-{served}\""
            };
        }

        /// <summary>
        /// Copies a freshly processed image (and its thumbnail, if one could be made) onto a
        /// stored-image row.
        /// </summary>
        private static void Apply(StoredImageEntity picture, ProcessedImage image, ProcessedImage? thumbnail, string version)
        {
            picture.Data = image.Data;
            picture.Format = image.Format;
            picture.Width = image.Width;
            picture.Height = image.Height;
            picture.ThumbnailData = thumbnail?.Data;
            picture.ThumbnailFormat = thumbnail?.Format ?? ImageFormat.Unknown;
            picture.Version = version;
            picture.UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// The token that goes in an image URL and comes back as its ETag: the first 16 hex
        /// characters of the bytes' SHA-256.
        /// </summary>
        /// <remarks>
        /// Content-derived rather than random, so re-uploading the same picture does not bust
        /// every client's cache. 64 bits is far more than enough to tell two of a user's own
        /// pictures apart — this is a cache key, not a security boundary.
        /// </remarks>
        private static string ContentVersion(byte[] data)
        {
            var hash = System.Security.Cryptography.SHA256.HashData(data);
            return Convert.ToHexStringLower(hash.AsSpan(0, 8));
        }

        private static string ContentTypeOf(ImageFormat format) => format switch
        {
            ImageFormat.Jpeg => "image/jpeg",
            ImageFormat.Png => "image/png",
            ImageFormat.WebP => "image/webp",
            _ => "application/octet-stream"
        };

        /// <summary>
        /// Pushes the user's traits to their Kratos identity after an avatar change.
        /// Non-blocking: logs a warning on failure but doesn't throw.
        /// </summary>
        /// <remarks>
        /// The traits no longer carry the picture itself — <c>BuildKratosTraitsFromProfile</c>
        /// leaves <c>profile_picture_base64</c> null — so what this call does after an upload or a
        /// delete is clear the legacy trait, which is what a <c>/sessions/whoami</c> response was
        /// shipping the image in.
        /// </remarks>
        private async Task SyncProfileTraitsToKratosAsync(int userId, CancellationToken cancellationToken)
        {
            try
            {
                using var context = _contextFactory.CreateForReading();
                var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
                
                if (user == null || string.IsNullOrEmpty(user.KratosIdentityId))
                {
                    Log.Debug($"User {userId} has no Kratos identity ID, skipping Kratos sync");
                    return;
                }

                var profile = await context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
                if (profile == null)
                {
                    Log.Debug($"UserProfile not found for user {userId}, skipping Kratos sync");
                    return;
                }

                var traits = UserFunctions.BuildKratosTraitsFromProfile(user, profile);
                var result = await _kratosService!.UpdateIdentityTraitsAsync(user.KratosIdentityId, traits, cancellationToken);
                
                if (result != null)
                {
                    Log.Debug($"Synced user {userId} profile picture to Kratos identity {user.KratosIdentityId}");
                }
                else
                {
                    Log.Warning($"Failed to sync user {userId} profile picture to Kratos identity {user.KratosIdentityId}");
                }
            }
            catch (Exception ex)
            {
                // Non-blocking: log warning but don't fail the request
                Log.Warning(ex, $"Error syncing user {userId} profile picture to Kratos: {ex.Message}");
            }
        }
    }
}

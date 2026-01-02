using Homassy.API.Context;
using Homassy.API.Enums;
using Homassy.API.Exceptions;
using Homassy.API.Models.ImageUpload;
using Homassy.API.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Homassy.API.Functions
{
    public class ImageFunctions
    {
        private readonly IImageProcessingService _imageProcessingService;

        public ImageFunctions(IImageProcessingService imageProcessingService)
        {
            _imageProcessingService = imageProcessingService;
        }

        public async Task<ProductImageInfo> UploadProductImageAsync(UploadProductImageRequest request, CancellationToken cancellationToken = default)
        {
            var userId = SessionInfo.GetUserId();
            if (!userId.HasValue)
            {
                Log.Warning("Invalid session: User ID not found");
                throw new UserNotFoundException("User not found");
            }

            var productFunctions = new ProductFunctions();
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

            var validationResult = _imageProcessingService.ValidateImage(request.ImageBase64, options);
            if (!validationResult.IsValid)
            {
                throw new BadRequestException($"Image validation failed: {validationResult.ErrorMessage}");
            }

            var processedImage = await _imageProcessingService.ProcessImageAsync(request.ImageBase64, options, cancellationToken);
            if (processedImage == null)
            {
                throw new BadRequestException("Failed to process image");
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var trackedProduct = await context.Products.FirstOrDefaultAsync(p => p.Id == product.Id, cancellationToken);
                if (trackedProduct == null)
                {
                    throw new ProductNotFoundException();
                }

                trackedProduct.ProductPictureBase64 = processedImage.Base64;
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId} uploaded image for product {product.Id} (PublicId: {product.PublicId})");

                // Record activity
                try
                {
                    var familyId = SessionInfo.GetFamilyId();
                    await new ActivityFunctions().RecordActivityAsync(
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

            var productFunctions = new ProductFunctions();
            var product = productFunctions.GetProductByPublicId(productPublicId);
            if (product == null)
            {
                throw new ProductNotFoundException();
            }

            var context = new HomassyDbContext();
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
                    await new ActivityFunctions().RecordActivityAsync(
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

        public async Task<UserProfileImageInfo> UploadUserProfileImageAsync(UploadUserProfileImageRequest request, CancellationToken cancellationToken = default)
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

            var validationResult = _imageProcessingService.ValidateImage(request.ImageBase64, options);
            if (!validationResult.IsValid)
            {
                throw new BadRequestException($"Image validation failed: {validationResult.ErrorMessage}");
            }

            var processedImage = await _imageProcessingService.ProcessImageAsync(request.ImageBase64, options, cancellationToken);
            if (processedImage == null)
            {
                throw new BadRequestException("Failed to process image");
            }

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var profile = await context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
                if (profile == null)
                {
                    throw new UserNotFoundException($"UserProfile not found for user {userId}");
                }

                profile.ProfilePictureBase64 = processedImage.Base64;
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId} uploaded profile picture");

                return new UserProfileImageInfo
                {
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

            var context = new HomassyDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var profile = await context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
                if (profile == null)
                {
                    throw new UserNotFoundException($"UserProfile not found for user {userId}");
                }

                if (string.IsNullOrEmpty(profile.ProfilePictureBase64))
                {
                    throw new BadRequestException("No profile picture to delete");
                }

                profile.ProfilePictureBase64 = null;
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                Log.Information($"User {userId} deleted profile picture");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Log.Error(ex, $"Failed to delete profile picture for user {userId}");
                throw;
            }
        }
    }
}

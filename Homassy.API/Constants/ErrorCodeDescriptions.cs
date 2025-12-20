using Homassy.API.Enums;
using Homassy.API.Models.Common;
using System.Collections.Frozen;

namespace Homassy.API.Constants;

public static class ErrorCodeDescriptions
{
    public static readonly FrozenDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        // Authentication Errors
        [ErrorCodes.AuthUnauthorized] = "Authentication required but not provided.",
        [ErrorCodes.AuthInvalidCredentials] = "Invalid email or verification code.",
        [ErrorCodes.AuthExpiredCredentials] = "Verification code has expired.",
        [ErrorCodes.AuthAccountLocked] = "Account is temporarily locked due to too many failed login attempts.",
        [ErrorCodes.AuthForbidden] = "Access denied to the requested resource.",
        [ErrorCodes.AuthInvalidRefreshToken] = "Invalid or expired refresh token.",
        [ErrorCodes.AuthTokenTheftDetected] = "Potential token theft detected. All sessions invalidated.",
        [ErrorCodes.AuthRegistrationDisabled] = "Registration is currently disabled.",

        // User Errors
        [ErrorCodes.UserNotFound] = "User not found.",
        [ErrorCodes.UserProfileNotFound] = "User profile not found.",
        [ErrorCodes.UserAuthNotFound] = "User authentication data not found.",
        [ErrorCodes.UserEmailInUse] = "Email address is already in use.",
        [ErrorCodes.UserNoProfilePicture] = "No profile picture to delete.",

        // Family Errors
        [ErrorCodes.FamilyNotFound] = "Family not found.",
        [ErrorCodes.FamilyAlreadyMember] = "User is already a member of a family.",
        [ErrorCodes.FamilyNotMember] = "User is not a member of any family.",
        [ErrorCodes.FamilyInvalidShareCode] = "Invalid or expired family share code.",

        // Product Errors
        [ErrorCodes.ProductNotFound] = "Product not found.",
        [ErrorCodes.ProductAccessDenied] = "Access denied to this product.",
        [ErrorCodes.ProductInventoryNotFound] = "Product inventory item not found.",

        // Location Errors
        [ErrorCodes.LocationNotFound] = "Location not found.",
        [ErrorCodes.LocationShoppingNotFound] = "Shopping location not found.",
        [ErrorCodes.LocationStorageNotFound] = "Storage location not found.",
        [ErrorCodes.LocationAccessDenied] = "Access denied to this location.",

        // Shopping List Errors
        [ErrorCodes.ShoppingListNotFound] = "Shopping list not found.",
        [ErrorCodes.ShoppingListItemNotFound] = "Shopping list item not found.",
        [ErrorCodes.ShoppingListAccessDenied] = "Access denied to this shopping list.",
        [ErrorCodes.ShoppingListItemInvalid] = "Invalid shopping list item data.",

        // Validation Errors
        [ErrorCodes.ValidationInvalidRequest] = "Invalid request data.",
        [ErrorCodes.ValidationRequiredField] = "Required field is missing.",
        [ErrorCodes.ValidationInvalidEmail] = "Invalid email address format.",
        [ErrorCodes.ValidationDangerousContent] = "Input contains potentially dangerous content.",
        [ErrorCodes.ValidationInvalidBarcode] = "Invalid barcode format.",
        [ErrorCodes.ValidationShareCodeRequired] = "Share code is required.",
        [ErrorCodes.ValidationNameRequired] = "Name is required.",
        [ErrorCodes.ValidationEmailCodeRequired] = "Email and code are required.",
        [ErrorCodes.ValidationRefreshTokenRequired] = "Refresh token is required.",
        [ErrorCodes.ValidationProfilePictureRequired] = "Profile picture data is required.",
        [ErrorCodes.ValidationBarcodeRequired] = "Barcode is required.",

        // Image Processing Errors
        [ErrorCodes.ImageEmpty] = "Image file is empty.",
        [ErrorCodes.ImageInvalidBase64] = "Invalid Base64 image data.",
        [ErrorCodes.ImageInvalidFormat] = "Unsupported image format. Only JPEG, PNG, and WebP are supported.",
        [ErrorCodes.ImageFileTooLarge] = "Image file size exceeds maximum allowed.",
        [ErrorCodes.ImageDimensionsTooSmall] = "Image dimensions are too small.",
        [ErrorCodes.ImageDimensionsTooLarge] = "Image dimensions are too large.",
        [ErrorCodes.ImageUnsupportedFormat] = "Image format is not in the allowed list.",
        [ErrorCodes.ImageMaliciousContent] = "Image appears to be corrupted or malicious.",
        [ErrorCodes.ImageProcessingFailed] = "Image processing failed.",

        // External API Errors
        [ErrorCodes.ExternalOpenFoodFactsNotFound] = "Product not found in Open Food Facts database.",

        // Rate Limiting Errors
        [ErrorCodes.RateLimitExceeded] = "Rate limit exceeded. Too many requests.",

        // System Errors
        [ErrorCodes.SystemUnexpectedError] = "An unexpected error occurred.",
        [ErrorCodes.SystemRequestTimeout] = "Request timed out.",
        [ErrorCodes.SystemRequestCancelled] = "Request was cancelled by the client.",
        [ErrorCodes.SystemUnauthorizedAccess] = "Unauthorized access."
    }.ToFrozenDictionary();

    public static IReadOnlyList<ErrorCodeInfo> GetAllErrorCodes()
    {
        return Descriptions
            .Select(kvp => new ErrorCodeInfo(kvp.Key, kvp.Value))
            .OrderBy(e => e.Code)
            .ToList();
    }

    public static string GetDescription(string code)
    {
        return Descriptions.TryGetValue(code, out var description) ? description : code;
    }

    public static IReadOnlyList<ErrorCodeInfo> GetErrorCodesByGroup(string prefix)
    {
        return Descriptions
            .Where(kvp => kvp.Key.StartsWith($"{prefix}-", StringComparison.OrdinalIgnoreCase))
            .Select(kvp => new ErrorCodeInfo(kvp.Key, kvp.Value))
            .OrderBy(e => e.Code)
            .ToList();
    }
}

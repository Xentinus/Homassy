namespace Homassy.API.Enums;

public static class ErrorCodes
{
    #region Authentication Errors (AUTH-0xxx)
    public const string AuthUnauthorized = "AUTH-0001";
    public const string AuthInvalidCredentials = "AUTH-0002";
    public const string AuthExpiredCredentials = "AUTH-0003";
    public const string AuthAccountLocked = "AUTH-0004";
    public const string AuthForbidden = "AUTH-0005";
    public const string AuthInvalidRefreshToken = "AUTH-0006";
    public const string AuthTokenTheftDetected = "AUTH-0007";
    public const string AuthRegistrationDisabled = "AUTH-0008";
    #endregion

    #region User Errors (USER-0xxx)
    public const string UserNotFound = "USER-0001";
    public const string UserProfileNotFound = "USER-0002";
    public const string UserAuthNotFound = "USER-0003";
    public const string UserEmailInUse = "USER-0004";
    public const string UserNoProfilePicture = "USER-0005";
    #endregion

    #region Family Errors (FAMILY-0xxx)
    public const string FamilyNotFound = "FAMILY-0001";
    public const string FamilyAlreadyMember = "FAMILY-0002";
    public const string FamilyNotMember = "FAMILY-0003";
    public const string FamilyInvalidShareCode = "FAMILY-0004";
    #endregion

    #region Product Errors (PRODUCT-0xxx)
    public const string ProductNotFound = "PRODUCT-0001";
    public const string ProductAccessDenied = "PRODUCT-0002";
    public const string ProductInventoryNotFound = "PRODUCT-0003";
    #endregion

    #region Location Errors (LOCATION-0xxx)
    public const string LocationNotFound = "LOCATION-0001";
    public const string LocationShoppingNotFound = "LOCATION-0002";
    public const string LocationStorageNotFound = "LOCATION-0003";
    public const string LocationAccessDenied = "LOCATION-0004";
    #endregion

    #region Shopping List Errors (SHOPPINGLIST-0xxx)
    public const string ShoppingListNotFound = "SHOPPINGLIST-0001";
    public const string ShoppingListItemNotFound = "SHOPPINGLIST-0002";
    public const string ShoppingListAccessDenied = "SHOPPINGLIST-0003";
    public const string ShoppingListItemInvalid = "SHOPPINGLIST-0004";
    #endregion

    #region Validation Errors (VALIDATION-0xxx)
    public const string ValidationInvalidRequest = "VALIDATION-0001";
    public const string ValidationRequiredField = "VALIDATION-0002";
    public const string ValidationInvalidEmail = "VALIDATION-0003";
    public const string ValidationDangerousContent = "VALIDATION-0004";
    public const string ValidationInvalidBarcode = "VALIDATION-0005";
    public const string ValidationShareCodeRequired = "VALIDATION-0006";
    public const string ValidationNameRequired = "VALIDATION-0007";
    public const string ValidationEmailCodeRequired = "VALIDATION-0008";
    public const string ValidationRefreshTokenRequired = "VALIDATION-0009";
    public const string ValidationProfilePictureRequired = "VALIDATION-0010";
    public const string ValidationBarcodeRequired = "VALIDATION-0011";
    #endregion

    #region Image Processing Errors (IMAGE-0xxx)
    public const string ImageEmpty = "IMAGE-0001";
    public const string ImageInvalidBase64 = "IMAGE-0002";
    public const string ImageInvalidFormat = "IMAGE-0003";
    public const string ImageFileTooLarge = "IMAGE-0004";
    public const string ImageDimensionsTooSmall = "IMAGE-0005";
    public const string ImageDimensionsTooLarge = "IMAGE-0006";
    public const string ImageUnsupportedFormat = "IMAGE-0007";
    public const string ImageMaliciousContent = "IMAGE-0008";
    public const string ImageProcessingFailed = "IMAGE-0009";
    #endregion

    #region External API Errors (EXTERNAL-0xxx)
    public const string ExternalOpenFoodFactsNotFound = "EXTERNAL-0001";
    #endregion

    #region Rate Limiting Errors (RATELIMIT-0xxx)
    public const string RateLimitExceeded = "RATELIMIT-0001";
    #endregion

    #region System Errors (SYSTEM-0xxx)
    public const string SystemUnexpectedError = "SYSTEM-0001";
    public const string SystemRequestTimeout = "SYSTEM-0002";
    public const string SystemRequestCancelled = "SYSTEM-0003";
    public const string SystemUnauthorizedAccess = "SYSTEM-0004";
    #endregion

    #region Progress Tracking Errors (PROGRESS-0xxx)
    public const string ProgressJobNotFound = "PROGRESS-0001";
    #endregion
}

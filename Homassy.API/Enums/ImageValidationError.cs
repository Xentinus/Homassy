namespace Homassy.API.Enums
{
    public enum ImageValidationError
    {
        None = 0,
        EmptyFile = 1,
        InvalidBase64 = 2,
        InvalidFormat = 3,
        FileTooLarge = 4,
        DimensionsTooSmall = 5,
        DimensionsTooLarge = 6,
        UnsupportedFormat = 7,
        MaliciousContent = 8,
        ProcessingFailed = 9
    }
}

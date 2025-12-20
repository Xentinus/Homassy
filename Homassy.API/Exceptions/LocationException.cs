using Homassy.API.Enums;

namespace Homassy.API.Exceptions
{
    public class LocationNotFoundException : Exception
    {
        public string ErrorCode { get; }

        public LocationNotFoundException(string message = "Location not found", string errorCode = ErrorCodes.LocationNotFound)
            : base(message)
        {
            ErrorCode = errorCode;
        }
    }

    public class ShoppingLocationNotFoundException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.LocationShoppingNotFound;

        public ShoppingLocationNotFoundException(string message = "Shopping location not found") : base(message) { }
    }

    public class StorageLocationNotFoundException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.LocationStorageNotFound;

        public StorageLocationNotFoundException(string message = "Storage location not found") : base(message) { }
    }

    public class LocationAccessDeniedException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.LocationAccessDenied;

        public LocationAccessDeniedException(string message = "You do not have access to this location") : base(message) { }
    }
}

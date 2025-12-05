namespace Homassy.API.Exceptions
{
    public class LocationNotFoundException : Exception
    {
        public LocationNotFoundException(string message = "Location not found") : base(message) { }
    }

    public class ShoppingLocationNotFoundException : Exception
    {
        public ShoppingLocationNotFoundException(string message = "Shopping location not found") : base(message) { }
    }

    public class StorageLocationNotFoundException : Exception
    {
        public StorageLocationNotFoundException(string message = "Storage location not found") : base(message) { }
    }

    public class LocationAccessDeniedException : Exception
    {
        public LocationAccessDeniedException(string message = "You do not have access to this location") : base(message) { }
    }
}

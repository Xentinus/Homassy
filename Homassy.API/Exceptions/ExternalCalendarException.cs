using Homassy.API.Enums;

namespace Homassy.API.Exceptions
{
    public class ExternalCalendarNotFoundException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.ExternalCalendarNotFound;

        public ExternalCalendarNotFoundException(string message = "External calendar not found") : base(message) { }
    }

    public class ExternalCalendarAccessDeniedException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.ExternalCalendarAccessDenied;

        public ExternalCalendarAccessDeniedException(string message = "Access denied to this external calendar") : base(message) { }
    }

    public class ExternalCalendarInvalidUrlException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.ExternalCalendarInvalidUrl;

        public ExternalCalendarInvalidUrlException(string message = "Invalid iCal URL") : base(message) { }
    }

    public class ExternalCalendarFetchFailedException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.ExternalCalendarFetchFailed;

        public ExternalCalendarFetchFailedException(string message = "Failed to fetch iCal feed") : base(message) { }
    }

    public class ExternalCalendarRequiresFamilyException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.ExternalCalendarRequiresFamily;

        public ExternalCalendarRequiresFamilyException(string message = "You must be a member of a family to manage external calendars") : base(message) { }
    }
}

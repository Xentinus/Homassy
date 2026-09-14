using Homassy.Data.Enums;

namespace Homassy.Data.Exceptions
{
    public class CalendarNoteNotFoundException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.CalendarNoteNotFound;

        public CalendarNoteNotFoundException(string message = "Calendar note not found") : base(message) { }
    }

    public class CalendarNoteAccessDeniedException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.CalendarNoteAccessDenied;

        public CalendarNoteAccessDeniedException(string message = "Access denied to this calendar note") : base(message) { }
    }

    /// <summary>
    /// A note belongs to a family, so a user who is not in one has nowhere to put it. Thrown rather
    /// than silently creating a personal note: a note nobody else can see is a reminder, and that is
    /// a different feature.
    /// </summary>
    public class CalendarNoteRequiresFamilyException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.CalendarNoteRequiresFamily;

        public CalendarNoteRequiresFamilyException(string message = "You must be a member of a family to manage calendar notes") : base(message) { }
    }

    public class CalendarNoteInvalidReminderException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.CalendarNoteInvalidReminder;

        public CalendarNoteInvalidReminderException(string message = "The reminder time is invalid") : base(message) { }
    }
}

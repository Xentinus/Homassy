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

    // CALNOTE-0003 used to live here: "you must be in a family to manage calendar notes". Writing a
    // note is not a household feature — a user with no family keeps personal notes instead of being
    // refused one (see CalendarNote) — so the rule, its error code and its three translations are
    // gone rather than left behind for someone to wire back up.

    public class CalendarNoteInvalidReminderException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.CalendarNoteInvalidReminder;

        public CalendarNoteInvalidReminderException(string message = "The reminder time is invalid") : base(message) { }
    }
}

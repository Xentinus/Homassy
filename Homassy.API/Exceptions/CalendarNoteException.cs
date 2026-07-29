using Homassy.API.Enums;

namespace Homassy.API.Exceptions
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

    public class CalendarNoteRequiresFamilyException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.CalendarNoteRequiresFamily;

        public CalendarNoteRequiresFamilyException(string message = "You must be a member of a family to manage calendar notes") : base(message) { }
    }

    public class CalendarNoteInvalidDateException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.CalendarNoteInvalidDate;

        public CalendarNoteInvalidDateException(string message = "Invalid note date") : base(message) { }
    }

    public class CalendarNoteInvalidReminderException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.CalendarNoteInvalidReminder;

        public CalendarNoteInvalidReminderException(string message = "Invalid reminder settings") : base(message) { }
    }

    /// <summary>
    /// The note changed between the client reading it and submitting its edit. Surfaces as 409 so the client
    /// can reload rather than silently overwriting the other member's change.
    /// </summary>
    public class CalendarNoteConcurrencyException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.CalendarNoteConcurrencyConflict;

        public CalendarNoteConcurrencyException(string message = "The calendar note was modified by someone else") : base(message) { }
    }
}

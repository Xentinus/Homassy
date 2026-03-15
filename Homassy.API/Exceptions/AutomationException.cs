using Homassy.API.Enums;

namespace Homassy.API.Exceptions
{
    public class AutomationNotFoundException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.AutomationNotFound;

        public AutomationNotFoundException(string message = "Automation rule not found") : base(message) { }
    }

    public class AutomationAccessDeniedException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.AutomationAccessDenied;

        public AutomationAccessDeniedException(string message = "Access denied to this automation rule") : base(message) { }
    }

    public class AutomationInvalidScheduleException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.AutomationInvalidSchedule;

        public AutomationInvalidScheduleException(string message = "Invalid automation schedule configuration") : base(message) { }
    }

    public class AutomationItemFullyConsumedException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.AutomationItemFullyConsumed;

        public AutomationItemFullyConsumedException(string message = "Cannot execute automation: item is fully consumed") : base(message) { }
    }

    public class AutomationInsufficientQuantityException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.AutomationInsufficientQuantity;

        public AutomationInsufficientQuantityException(string message = "Insufficient quantity to execute automation") : base(message) { }
    }

    public class AutomationShoppingListNotFoundException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.AutomationShoppingListNotFound;

        public AutomationShoppingListNotFoundException(string message = "Shopping list not found for automation") : base(message) { }
    }

    public class AutomationProductNotFoundException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.AutomationProductNotFound;

        public AutomationProductNotFoundException(string message = "Product not found for automation") : base(message) { }
    }
}

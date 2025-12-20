using Homassy.API.Enums;

namespace Homassy.API.Exceptions
{
    public class ShoppingListNotFoundException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.ShoppingListNotFound;

        public ShoppingListNotFoundException(string message = "Shopping list not found") : base(message) { }
    }

    public class ShoppingListItemNotFoundException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.ShoppingListItemNotFound;

        public ShoppingListItemNotFoundException(string message = "Shopping list item not found") : base(message) { }
    }

    public class ShoppingListAccessDeniedException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.ShoppingListAccessDenied;

        public ShoppingListAccessDeniedException(string message = "You do not have access to this shopping list") : base(message) { }
    }

    public class InvalidShoppingListItemException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.ShoppingListItemInvalid;

        public InvalidShoppingListItemException(string message) : base(message) { }
    }
}

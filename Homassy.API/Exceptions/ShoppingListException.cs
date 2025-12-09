namespace Homassy.API.Exceptions
{
    public class ShoppingListNotFoundException : Exception
    {
        public ShoppingListNotFoundException(string message = "Shopping list not found") : base(message) { }
    }

    public class ShoppingListItemNotFoundException : Exception
    {
        public ShoppingListItemNotFoundException(string message = "Shopping list item not found") : base(message) { }
    }

    public class ShoppingListAccessDeniedException : Exception
    {
        public ShoppingListAccessDeniedException(string message = "You do not have access to this shopping list") : base(message) { }
    }

    public class InvalidShoppingListItemException : Exception
    {
        public InvalidShoppingListItemException(string message) : base(message) { }
    }
}

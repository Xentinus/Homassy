using Homassy.Data.Enums;

namespace Homassy.Data.Exceptions
{
    public class ProductNotFoundException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.ProductNotFound;

        public ProductNotFoundException(string message = "Product not found") : base(message) { }
    }

    public class ProductInventoryItemNotFoundException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.ProductInventoryNotFound;

        public ProductInventoryItemNotFoundException(string message = "Product inventory item not found") : base(message) { }
    }

    public class ProductAccessDeniedException : Exception
    {
        public string ErrorCode { get; } = ErrorCodes.ProductAccessDenied;

        public ProductAccessDeniedException(string message = "You do not have access to this product") : base(message) { }
    }
}

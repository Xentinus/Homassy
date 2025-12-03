using Microsoft.AspNetCore.Http;

namespace Homassy.API.Exceptions
{
    public class ProductNotFoundException : Exception
    {
        public ProductNotFoundException(string message = "Product not found") : base(message) { }
    }

    public class ProductInventoryItemNotFoundException : Exception
    {
        public ProductInventoryItemNotFoundException(string message = "Product inventory item not found") : base(message) { }
    }

    public class ProductAccessDeniedException : Exception
    {
        public ProductAccessDeniedException(string message = "You do not have access to this product") : base(message) { }
    }
}

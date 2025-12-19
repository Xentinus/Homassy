using Homassy.API.Services;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Attributes.Validation
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
    public class ValidBarcodeAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is null)
            {
                return ValidationResult.Success;
            }

            if (value is not string barcode)
            {
                return new ValidationResult($"The field {validationContext.DisplayName} must be a string.");
            }

            if (string.IsNullOrWhiteSpace(barcode))
            {
                return ValidationResult.Success;
            }

            var validationService = validationContext.GetService(typeof(IBarcodeValidationService)) as IBarcodeValidationService;

            validationService ??= new BarcodeValidationService();

            var result = validationService.Validate(barcode);

            if (!result.IsValid)
            {
                return new ValidationResult(result.ErrorMessage);
            }

            return ValidationResult.Success;
        }
    }
}

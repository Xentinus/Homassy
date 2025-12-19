using Homassy.API.Services.Sanitization;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Attributes.Validation
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
    public class SanitizedStringAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is null)
            {
                return ValidationResult.Success;
            }

            if (value is not string stringValue)
            {
                return new ValidationResult($"The field {validationContext.DisplayName} must be a string.");
            }

            if (string.IsNullOrWhiteSpace(stringValue))
            {
                return ValidationResult.Success;
            }

            if (ContainsDangerousPatterns(stringValue))
            {
                return new ValidationResult($"The field {validationContext.DisplayName} contains potentially dangerous content.");
            }

            var sanitizationService = validationContext.GetService(typeof(IInputSanitizationService)) as IInputSanitizationService;

            if (sanitizationService is null)
            {
                return ValidationResult.Success;
            }

            var sanitized = sanitizationService.SanitizePlainText(stringValue);

            if (sanitized != stringValue && ContainsHtmlPatterns(stringValue))
            {
                return new ValidationResult($"The field {validationContext.DisplayName} contains potentially dangerous content.");
            }

            return ValidationResult.Success;
        }

        private static bool ContainsDangerousPatterns(string input)
        {
            var dangerousPatterns = new[]
            {
                "<script",
                "javascript:",
                "onerror=",
                "onload=",
                "onclick=",
                "onmouseover=",
                "onfocus=",
                "onblur=",
                "eval(",
                "expression(",
                "vbscript:",
                "data:text/html"
            };

            var lowerInput = input.ToLowerInvariant();

            foreach (var pattern in dangerousPatterns)
            {
                if (lowerInput.Contains(pattern))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsHtmlPatterns(string input)
        {
            return input.Contains('<') || input.Contains('>');
        }
    }
}

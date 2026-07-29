using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Attributes.Validation
{
    /// <summary>
    /// Blocks the script-injection patterns <see cref="SanitizedStringAttribute"/> blocks, but — unlike it —
    /// tolerates bare <c>&lt;</c> and <c>&gt;</c> characters.
    /// <para>
    /// <see cref="SanitizedStringAttribute"/> rejects a value whenever HTML-encoding would change it *and* it
    /// contains an angle bracket, which is correct for a short label but wrong for prose: a legitimate note
    /// reading "tartsd 5 °C &lt; alatt" or "apply if x &gt; 3" would be refused with an opaque
    /// "potentially dangerous content" error. Use this attribute on free-text fields instead.
    /// </para>
    /// <para>
    /// Nothing is lost by allowing the brackets through: the values are rendered as text (Vue escapes by
    /// default and never uses <c>v-html</c> for them) and reach push notifications as plain text. The
    /// dangerous-pattern blocklist below is what actually stops an injection attempt.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
    public class SafeFreeTextAttribute : ValidationAttribute
    {
        private static readonly string[] DangerousPatterns =
        [
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
        ];

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

            var lowerValue = stringValue.ToLowerInvariant();

            foreach (var pattern in DangerousPatterns)
            {
                if (lowerValue.Contains(pattern))
                {
                    return new ValidationResult($"The field {validationContext.DisplayName} contains potentially dangerous content.");
                }
            }

            return ValidationResult.Success;
        }
    }
}

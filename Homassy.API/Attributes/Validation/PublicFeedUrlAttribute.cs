using Homassy.API.Security;
using System.ComponentModel.DataAnnotations;

namespace Homassy.API.Attributes.Validation
{
    /// <summary>
    /// Requires a URL the server may safely fetch: https (http is tolerated only in
    /// Development), pointing at a fully qualified public host. Loopback, link-local, private
    /// and reserved addresses are rejected, as are unqualified single-label hosts.
    /// </summary>
    /// <remarks>
    /// Validating at the model boundary is what turns "the sync quietly fails later" into a
    /// clear 400 at the moment the user saves the feed. It is not the security boundary on its
    /// own — DNS can be re-pointed after the value is stored — so
    /// <see cref="ExternalUrlGuard.CreateConnectCallback"/> re-screens the host at connect time.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
    public class PublicFeedUrlAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is null)
            {
                return ValidationResult.Success;
            }

            if (value is not string url)
            {
                return new ValidationResult($"The field {validationContext.DisplayName} must be a string.");
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                return ValidationResult.Success;
            }

            if (!ExternalUrlGuard.TryValidate(url, ExternalUrlGuard.AllowInsecureScheme, out _, out var error))
            {
                return new ValidationResult($"{validationContext.DisplayName}: {error}");
            }

            return ValidationResult.Success;
        }
    }
}

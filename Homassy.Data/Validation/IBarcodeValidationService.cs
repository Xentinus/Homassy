using Homassy.Data.Enums;
using Homassy.Data.Models.Barcode;
using Homassy.Data.Validation;

namespace Homassy.Data.Validation
{
    public interface IBarcodeValidationService
    {
        BarcodeValidationResult Validate(string? barcode);
        BarcodeFormat DetectFormat(string barcode);
        bool ValidateChecksum(string barcode, BarcodeFormat format);
    }
}

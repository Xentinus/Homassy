using Homassy.API.Enums;
using Homassy.API.Models.Barcode;

namespace Homassy.API.Services
{
    public interface IBarcodeValidationService
    {
        BarcodeValidationResult Validate(string? barcode);
        BarcodeFormat DetectFormat(string barcode);
        bool ValidateChecksum(string barcode, BarcodeFormat format);
    }
}

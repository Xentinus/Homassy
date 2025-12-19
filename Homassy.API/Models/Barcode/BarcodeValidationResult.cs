using Homassy.API.Enums;

namespace Homassy.API.Models.Barcode
{
    public class BarcodeValidationResult
    {
        public bool IsValid { get; init; }
        public BarcodeFormat Format { get; init; }
        public string? ErrorMessage { get; init; }
        public string? NormalizedBarcode { get; init; }

        public static BarcodeValidationResult Success(BarcodeFormat format, string normalizedBarcode)
        {
            return new BarcodeValidationResult
            {
                IsValid = true,
                Format = format,
                NormalizedBarcode = normalizedBarcode
            };
        }

        public static BarcodeValidationResult Failure(string errorMessage)
        {
            return new BarcodeValidationResult
            {
                IsValid = false,
                Format = BarcodeFormat.Unknown,
                ErrorMessage = errorMessage
            };
        }
    }
}

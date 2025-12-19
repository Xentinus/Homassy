using Homassy.API.Enums;
using Homassy.API.Models.Barcode;

namespace Homassy.API.Services
{
    public class BarcodeValidationService : IBarcodeValidationService
    {
        public BarcodeValidationResult Validate(string? barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode))
            {
                return BarcodeValidationResult.Success(BarcodeFormat.Unknown, string.Empty);
            }

            var normalizedBarcode = barcode.Trim();

            if (!IsValidBarcodeCharacters(normalizedBarcode, out var format))
            {
                return BarcodeValidationResult.Failure($"Barcode '{normalizedBarcode}' contains invalid characters. Only digits are allowed for EAN/UPC formats.");
            }

            format = DetectFormat(normalizedBarcode);

            if (format == BarcodeFormat.Unknown)
            {
                return BarcodeValidationResult.Failure($"Barcode '{normalizedBarcode}' has invalid length. Supported formats: EAN-13 (13 digits), EAN-8 (8 digits), UPC-A (12 digits), UPC-E (6-8 digits).");
            }

            if (!ValidateChecksum(normalizedBarcode, format))
            {
                return BarcodeValidationResult.Failure($"Barcode '{normalizedBarcode}' has invalid checksum for {GetFormatDisplayName(format)} format.");
            }

            return BarcodeValidationResult.Success(format, normalizedBarcode);
        }

        public BarcodeFormat DetectFormat(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode))
            {
                return BarcodeFormat.Unknown;
            }

            var length = barcode.Length;

            return length switch
            {
                13 => BarcodeFormat.EAN13,
                8 when IsEan8Pattern(barcode) => BarcodeFormat.EAN8,
                8 when IsUpcePattern(barcode) => BarcodeFormat.UPCE,
                12 => BarcodeFormat.UPCA,
                6 => BarcodeFormat.UPCE,
                _ => BarcodeFormat.Unknown
            };
        }

        public bool ValidateChecksum(string barcode, BarcodeFormat format)
        {
            return format switch
            {
                BarcodeFormat.EAN13 => ValidateEan13Checksum(barcode),
                BarcodeFormat.EAN8 => ValidateEan8Checksum(barcode),
                BarcodeFormat.UPCA => ValidateUpcaChecksum(barcode),
                BarcodeFormat.UPCE => ValidateUpceChecksum(barcode),
                BarcodeFormat.Code128 => true,
                _ => false
            };
        }

        private static bool IsValidBarcodeCharacters(string barcode, out BarcodeFormat format)
        {
            format = BarcodeFormat.Unknown;

            foreach (var c in barcode)
            {
                if (!char.IsDigit(c))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsEan8Pattern(string barcode)
        {
            if (barcode.Length != 8)
            {
                return false;
            }

            return barcode[0] != '0' || barcode[1] != '0';
        }

        private static bool IsUpcePattern(string barcode)
        {
            if (barcode.Length != 8 && barcode.Length != 6)
            {
                return false;
            }

            if (barcode.Length == 8)
            {
                return barcode[0] == '0' && barcode[7] >= '0' && barcode[7] <= '9';
            }

            return true;
        }

        private static bool ValidateEan13Checksum(string barcode)
        {
            if (barcode.Length != 13)
            {
                return false;
            }

            var sum = 0;
            for (var i = 0; i < 12; i++)
            {
                var digit = barcode[i] - '0';
                sum += i % 2 == 0 ? digit : digit * 3;
            }

            var checkDigit = (10 - sum % 10) % 10;
            return checkDigit == barcode[12] - '0';
        }

        private static bool ValidateEan8Checksum(string barcode)
        {
            if (barcode.Length != 8)
            {
                return false;
            }

            var sum = 0;
            for (var i = 0; i < 7; i++)
            {
                var digit = barcode[i] - '0';
                sum += i % 2 == 0 ? digit * 3 : digit;
            }

            var checkDigit = (10 - sum % 10) % 10;
            return checkDigit == barcode[7] - '0';
        }

        private static bool ValidateUpcaChecksum(string barcode)
        {
            if (barcode.Length != 12)
            {
                return false;
            }

            var sum = 0;
            for (var i = 0; i < 11; i++)
            {
                var digit = barcode[i] - '0';
                sum += i % 2 == 0 ? digit * 3 : digit;
            }

            var checkDigit = (10 - sum % 10) % 10;
            return checkDigit == barcode[11] - '0';
        }

        private static bool ValidateUpceChecksum(string barcode)
        {
            if (barcode.Length != 6 && barcode.Length != 8)
            {
                return false;
            }

            var expandedUpc = ExpandUpceToUpca(barcode);
            if (string.IsNullOrEmpty(expandedUpc))
            {
                return false;
            }

            return ValidateUpcaChecksum(expandedUpc);
        }

        private static string ExpandUpceToUpca(string upce)
        {
            string digits;
            
            if (upce.Length == 8)
            {
                digits = upce[1..7];
            }
            else if (upce.Length == 6)
            {
                digits = upce;
            }
            else
            {
                return string.Empty;
            }

            var lastDigit = digits[5];
            var expanded = lastDigit switch
            {
                '0' or '1' or '2' => $"0{digits[0]}{digits[1]}{lastDigit}0000{digits[2]}{digits[3]}{digits[4]}",
                '3' => $"0{digits[0]}{digits[1]}{digits[2]}00000{digits[3]}{digits[4]}",
                '4' => $"0{digits[0]}{digits[1]}{digits[2]}{digits[3]}00000{digits[4]}",
                _ => $"0{digits[0]}{digits[1]}{digits[2]}{digits[3]}{digits[4]}0000{lastDigit}"
            };

            var sum = 0;
            for (var i = 0; i < 11; i++)
            {
                var digit = expanded[i] - '0';
                sum += i % 2 == 0 ? digit * 3 : digit;
            }

            var checkDigit = (10 - sum % 10) % 10;
            return $"{expanded}{checkDigit}";
        }

        private static string GetFormatDisplayName(BarcodeFormat format)
        {
            return format switch
            {
                BarcodeFormat.EAN13 => "EAN-13",
                BarcodeFormat.EAN8 => "EAN-8",
                BarcodeFormat.UPCA => "UPC-A",
                BarcodeFormat.UPCE => "UPC-E",
                BarcodeFormat.Code128 => "Code-128",
                _ => "Unknown"
            };
        }
    }
}

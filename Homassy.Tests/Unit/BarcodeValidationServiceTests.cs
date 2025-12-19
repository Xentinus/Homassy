using Homassy.API.Enums;
using Homassy.API.Services;

namespace Homassy.Tests.Unit
{
    public class BarcodeValidationServiceTests
    {
        private readonly BarcodeValidationService _service;

        public BarcodeValidationServiceTests()
        {
            _service = new BarcodeValidationService();
        }

        #region Validate - Null and Empty
        [Fact]
        public void Validate_WhenNull_ReturnsSuccess()
        {
            var result = _service.Validate(null);

            Assert.True(result.IsValid);
            Assert.Equal(BarcodeFormat.Unknown, result.Format);
        }

        [Fact]
        public void Validate_WhenEmpty_ReturnsSuccess()
        {
            var result = _service.Validate(string.Empty);

            Assert.True(result.IsValid);
            Assert.Equal(BarcodeFormat.Unknown, result.Format);
        }

        [Fact]
        public void Validate_WhenWhitespace_ReturnsSuccess()
        {
            var result = _service.Validate("   ");

            Assert.True(result.IsValid);
            Assert.Equal(BarcodeFormat.Unknown, result.Format);
        }
        #endregion

        #region Validate - EAN-13
        [Theory]
        [InlineData("4006381333931")]
        [InlineData("5901234123457")]
        [InlineData("0012345678905")]
        [InlineData("9780201379624")]
        public void Validate_WhenValidEan13_ReturnsSuccess(string barcode)
        {
            var result = _service.Validate(barcode);

            Assert.True(result.IsValid);
            Assert.Equal(BarcodeFormat.EAN13, result.Format);
            Assert.Equal(barcode, result.NormalizedBarcode);
        }

        [Theory]
        [InlineData("4006381333930")]
        [InlineData("5901234123456")]
        [InlineData("1234567890123")]
        public void Validate_WhenInvalidEan13Checksum_ReturnsFailure(string barcode)
        {
            var result = _service.Validate(barcode);

            Assert.False(result.IsValid);
            Assert.Contains("invalid checksum", result.ErrorMessage);
            Assert.Contains("EAN-13", result.ErrorMessage);
        }
        #endregion

        #region Validate - EAN-8
        [Theory]
        [InlineData("96385074")]
        [InlineData("55123457")]
        [InlineData("12345670")]
        public void Validate_WhenValidEan8_ReturnsSuccess(string barcode)
        {
            var result = _service.Validate(barcode);

            Assert.True(result.IsValid);
            Assert.Equal(BarcodeFormat.EAN8, result.Format);
            Assert.Equal(barcode, result.NormalizedBarcode);
        }

        [Theory]
        [InlineData("96385073")]
        [InlineData("55123456")]
        [InlineData("12345671")]
        public void Validate_WhenInvalidEan8Checksum_ReturnsFailure(string barcode)
        {
            var result = _service.Validate(barcode);

            Assert.False(result.IsValid);
            Assert.Contains("invalid checksum", result.ErrorMessage);
        }
        #endregion

        #region Validate - UPC-A
        [Theory]
        [InlineData("012345678905")]
        [InlineData("042100005264")]
        [InlineData("614141000036")]
        public void Validate_WhenValidUpcA_ReturnsSuccess(string barcode)
        {
            var result = _service.Validate(barcode);

            Assert.True(result.IsValid);
            Assert.Equal(BarcodeFormat.UPCA, result.Format);
            Assert.Equal(barcode, result.NormalizedBarcode);
        }

        [Theory]
        [InlineData("012345678901")]
        [InlineData("042100005260")]
        [InlineData("123456789010")]
        public void Validate_WhenInvalidUpcAChecksum_ReturnsFailure(string barcode)
        {
            var result = _service.Validate(barcode);

            Assert.False(result.IsValid);
            Assert.Contains("invalid checksum", result.ErrorMessage);
            Assert.Contains("UPC-A", result.ErrorMessage);
        }
        #endregion

        #region Validate - Invalid Characters
        [Theory]
        [InlineData("ABC123456789")]
        [InlineData("123-456-7890")]
        [InlineData("1234 5678 901")]
        [InlineData("123456789O12")]
        public void Validate_WhenContainsInvalidCharacters_ReturnsFailure(string barcode)
        {
            var result = _service.Validate(barcode);

            Assert.False(result.IsValid);
            Assert.Contains("invalid characters", result.ErrorMessage);
        }
        #endregion

        #region Validate - Invalid Length
        [Theory]
        [InlineData("12345")]
        [InlineData("123456789")]
        [InlineData("12345678901")]
        [InlineData("12345678901234")]
        [InlineData("123456789012345")]
        public void Validate_WhenInvalidLength_ReturnsFailure(string barcode)
        {
            var result = _service.Validate(barcode);

            Assert.False(result.IsValid);
            Assert.Contains("invalid length", result.ErrorMessage);
        }
        #endregion

        #region DetectFormat
        [Theory]
        [InlineData("4006381333931", BarcodeFormat.EAN13)]
        [InlineData("96385074", BarcodeFormat.EAN8)]
        [InlineData("012345678905", BarcodeFormat.UPCA)]
        [InlineData("123456", BarcodeFormat.UPCE)]
        public void DetectFormat_WhenValidBarcode_ReturnsCorrectFormat(string barcode, BarcodeFormat expectedFormat)
        {
            var result = _service.DetectFormat(barcode);

            Assert.Equal(expectedFormat, result);
        }

        [Theory]
        [InlineData("")]
        [InlineData("123")]
        [InlineData("12345")]
        public void DetectFormat_WhenInvalidBarcode_ReturnsUnknown(string barcode)
        {
            var result = _service.DetectFormat(barcode);

            Assert.Equal(BarcodeFormat.Unknown, result);
        }
        #endregion

        #region ValidateChecksum
        [Theory]
        [InlineData("4006381333931", BarcodeFormat.EAN13, true)]
        [InlineData("4006381333930", BarcodeFormat.EAN13, false)]
        [InlineData("96385074", BarcodeFormat.EAN8, true)]
        [InlineData("96385073", BarcodeFormat.EAN8, false)]
        [InlineData("012345678905", BarcodeFormat.UPCA, true)]
        [InlineData("012345678901", BarcodeFormat.UPCA, false)]
        public void ValidateChecksum_WhenCalled_ReturnsExpectedResult(string barcode, BarcodeFormat format, bool expected)
        {
            var result = _service.ValidateChecksum(barcode, format);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void ValidateChecksum_WhenCode128_ReturnsTrue()
        {
            var result = _service.ValidateChecksum("ABC123", BarcodeFormat.Code128);

            Assert.True(result);
        }

        [Fact]
        public void ValidateChecksum_WhenUnknownFormat_ReturnsFalse()
        {
            var result = _service.ValidateChecksum("1234567890", BarcodeFormat.Unknown);

            Assert.False(result);
        }
        #endregion

        #region Validate - Whitespace Trimming
        [Fact]
        public void Validate_WhenBarcodeHasLeadingWhitespace_TrimsAndValidates()
        {
            var result = _service.Validate("  4006381333931");

            Assert.True(result.IsValid);
            Assert.Equal("4006381333931", result.NormalizedBarcode);
        }

        [Fact]
        public void Validate_WhenBarcodeHasTrailingWhitespace_TrimsAndValidates()
        {
            var result = _service.Validate("4006381333931  ");

            Assert.True(result.IsValid);
            Assert.Equal("4006381333931", result.NormalizedBarcode);
        }
        #endregion

        #region Real-World Barcodes
        [Theory]
        [InlineData("5000112637922")]
        [InlineData("8711000538012")]
        [InlineData("5449000000996")]
        public void Validate_WhenRealWorldEan13_ReturnsSuccess(string barcode)
        {
            var result = _service.Validate(barcode);

            Assert.True(result.IsValid);
            Assert.Equal(BarcodeFormat.EAN13, result.Format);
        }
        #endregion
    }
}

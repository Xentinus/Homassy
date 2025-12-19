using Homassy.API.Attributes.Validation;
using Homassy.API.Services;
using System.ComponentModel.DataAnnotations;

namespace Homassy.Tests.Unit
{
    public class ValidBarcodeAttributeTests
    {
        private readonly IBarcodeValidationService _validationService;

        public ValidBarcodeAttributeTests()
        {
            _validationService = new BarcodeValidationService();
        }

        [Fact]
        public void Validate_WhenNullValue_ReturnsSuccess()
        {
            var attribute = new ValidBarcodeAttribute();
            var context = CreateValidationContext(null);

            var result = attribute.GetValidationResult(null, context);

            Assert.Equal(ValidationResult.Success, result);
        }

        [Fact]
        public void Validate_WhenEmptyString_ReturnsSuccess()
        {
            var attribute = new ValidBarcodeAttribute();
            var context = CreateValidationContext(string.Empty);

            var result = attribute.GetValidationResult(string.Empty, context);

            Assert.Equal(ValidationResult.Success, result);
        }

        [Fact]
        public void Validate_WhenValidEan13_ReturnsSuccess()
        {
            var attribute = new ValidBarcodeAttribute();
            var context = CreateValidationContext("4006381333931");

            var result = attribute.GetValidationResult("4006381333931", context);

            Assert.Equal(ValidationResult.Success, result);
        }

        [Fact]
        public void Validate_WhenValidEan8_ReturnsSuccess()
        {
            var attribute = new ValidBarcodeAttribute();
            var context = CreateValidationContext("96385074");

            var result = attribute.GetValidationResult("96385074", context);

            Assert.Equal(ValidationResult.Success, result);
        }

        [Fact]
        public void Validate_WhenValidUpcA_ReturnsSuccess()
        {
            var attribute = new ValidBarcodeAttribute();
            var context = CreateValidationContext("012345678905");

            var result = attribute.GetValidationResult("012345678905", context);

            Assert.Equal(ValidationResult.Success, result);
        }

        [Fact]
        public void Validate_WhenInvalidChecksum_ReturnsError()
        {
            var attribute = new ValidBarcodeAttribute();
            var context = CreateValidationContext("4006381333930");

            var result = attribute.GetValidationResult("4006381333930", context);

            Assert.NotEqual(ValidationResult.Success, result);
            Assert.Contains("invalid checksum", result!.ErrorMessage);
        }

        [Fact]
        public void Validate_WhenInvalidCharacters_ReturnsError()
        {
            var attribute = new ValidBarcodeAttribute();
            var context = CreateValidationContext("ABC123456789");

            var result = attribute.GetValidationResult("ABC123456789", context);

            Assert.NotEqual(ValidationResult.Success, result);
            Assert.Contains("invalid characters", result!.ErrorMessage);
        }

        [Fact]
        public void Validate_WhenInvalidLength_ReturnsError()
        {
            var attribute = new ValidBarcodeAttribute();
            var context = CreateValidationContext("12345");

            var result = attribute.GetValidationResult("12345", context);

            Assert.NotEqual(ValidationResult.Success, result);
            Assert.Contains("invalid length", result!.ErrorMessage);
        }

        [Fact]
        public void Validate_WhenNotString_ReturnsError()
        {
            var attribute = new ValidBarcodeAttribute();
            var context = CreateValidationContext(123);

            var result = attribute.GetValidationResult(123, context);

            Assert.NotEqual(ValidationResult.Success, result);
            Assert.Contains("must be a string", result!.ErrorMessage);
        }

        [Theory]
        [InlineData("4006381333931")]
        [InlineData("5901234123457")]
        [InlineData("9780201379624")]
        public void Validate_WhenMultipleValidEan13_ReturnsSuccess(string barcode)
        {
            var attribute = new ValidBarcodeAttribute();
            var context = CreateValidationContext(barcode);

            var result = attribute.GetValidationResult(barcode, context);

            Assert.Equal(ValidationResult.Success, result);
        }

        [Theory]
        [InlineData("96385074")]
        [InlineData("55123457")]
        [InlineData("12345670")]
        public void Validate_WhenMultipleValidEan8_ReturnsSuccess(string barcode)
        {
            var attribute = new ValidBarcodeAttribute();
            var context = CreateValidationContext(barcode);

            var result = attribute.GetValidationResult(barcode, context);

            Assert.Equal(ValidationResult.Success, result);
        }

        [Theory]
        [InlineData("012345678905")]
        [InlineData("042100005264")]
        [InlineData("614141000036")]
        public void Validate_WhenMultipleValidUpcA_ReturnsSuccess(string barcode)
        {
            var attribute = new ValidBarcodeAttribute();
            var context = CreateValidationContext(barcode);

            var result = attribute.GetValidationResult(barcode, context);

            Assert.Equal(ValidationResult.Success, result);
        }

        [Fact]
        public void Validate_WithInjectedService_UsesService()
        {
            var attribute = new ValidBarcodeAttribute();
            var context = CreateValidationContextWithService("4006381333931");

            var result = attribute.GetValidationResult("4006381333931", context);

            Assert.Equal(ValidationResult.Success, result);
        }

        private ValidationContext CreateValidationContext(object? value)
        {
            var testObject = new TestModel { Barcode = value?.ToString() };
            var context = new ValidationContext(testObject)
            {
                MemberName = "Barcode",
                DisplayName = "Barcode"
            };

            return context;
        }

        private ValidationContext CreateValidationContextWithService(object? value)
        {
            var testObject = new TestModel { Barcode = value?.ToString() };
            var context = new ValidationContext(testObject)
            {
                MemberName = "Barcode",
                DisplayName = "Barcode"
            };

            var serviceProvider = new TestServiceProvider(_validationService);
            context.InitializeServiceProvider(type => serviceProvider.GetService(type));

            return context;
        }

        private class TestModel
        {
            public string? Barcode { get; set; }
        }

        private class TestServiceProvider : IServiceProvider
        {
            private readonly IBarcodeValidationService _validationService;

            public TestServiceProvider(IBarcodeValidationService validationService)
            {
                _validationService = validationService;
            }

            public object? GetService(Type serviceType)
            {
                if (serviceType == typeof(IBarcodeValidationService))
                {
                    return _validationService;
                }

                return null;
            }
        }
    }
}

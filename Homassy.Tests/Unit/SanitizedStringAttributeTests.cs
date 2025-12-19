using Homassy.API.Attributes.Validation;
using Homassy.API.Services.Sanitization;
using System.ComponentModel.DataAnnotations;

namespace Homassy.Tests.Unit
{
    public class SanitizedStringAttributeTests
    {
        private readonly IInputSanitizationService _sanitizationService;

        public SanitizedStringAttributeTests()
        {
            _sanitizationService = new InputSanitizationService();
        }

        [Fact]
        public void Validate_WhenNullValue_ReturnsSuccess()
        {
            var attribute = new SanitizedStringAttribute();
            var context = CreateValidationContext(null);

            var result = attribute.GetValidationResult(null, context);

            Assert.Equal(ValidationResult.Success, result);
        }

        [Fact]
        public void Validate_WhenEmptyString_ReturnsSuccess()
        {
            var attribute = new SanitizedStringAttribute();
            var context = CreateValidationContext(string.Empty);

            var result = attribute.GetValidationResult(string.Empty, context);

            Assert.Equal(ValidationResult.Success, result);
        }

        [Fact]
        public void Validate_WhenCleanText_ReturnsSuccess()
        {
            var attribute = new SanitizedStringAttribute();
            var context = CreateValidationContext("Hello World");

            var result = attribute.GetValidationResult("Hello World", context);

            Assert.Equal(ValidationResult.Success, result);
        }

        [Fact]
        public void Validate_WhenContainsScriptTag_ReturnsError()
        {
            var attribute = new SanitizedStringAttribute();
            var context = CreateValidationContext("<script>alert('xss')</script>");

            var result = attribute.GetValidationResult("<script>alert('xss')</script>", context);

            Assert.NotEqual(ValidationResult.Success, result);
            Assert.Contains("dangerous content", result!.ErrorMessage);
        }

        [Fact]
        public void Validate_WhenContainsJavascriptUri_ReturnsError()
        {
            var attribute = new SanitizedStringAttribute();
            var context = CreateValidationContext("javascript:alert('xss')");

            var result = attribute.GetValidationResult("javascript:alert('xss')", context);

            Assert.NotEqual(ValidationResult.Success, result);
        }

        [Fact]
        public void Validate_WhenContainsOnErrorHandler_ReturnsError()
        {
            var attribute = new SanitizedStringAttribute();
            var context = CreateValidationContext("<img onerror=alert(1)>");

            var result = attribute.GetValidationResult("<img onerror=alert(1)>", context);

            Assert.NotEqual(ValidationResult.Success, result);
        }

        [Fact]
        public void Validate_WhenContainsOnLoadHandler_ReturnsError()
        {
            var attribute = new SanitizedStringAttribute();
            var context = CreateValidationContext("<body onload=alert(1)>");

            var result = attribute.GetValidationResult("<body onload=alert(1)>", context);

            Assert.NotEqual(ValidationResult.Success, result);
        }

        [Fact]
        public void Validate_WhenContainsOnClickHandler_ReturnsError()
        {
            var attribute = new SanitizedStringAttribute();
            var context = CreateValidationContext("<div onclick=alert(1)>");

            var result = attribute.GetValidationResult("<div onclick=alert(1)>", context);

            Assert.NotEqual(ValidationResult.Success, result);
        }

        [Fact]
        public void Validate_WhenContainsEval_ReturnsError()
        {
            var attribute = new SanitizedStringAttribute();
            var context = CreateValidationContext("eval(document.cookie)");

            var result = attribute.GetValidationResult("eval(document.cookie)", context);

            Assert.NotEqual(ValidationResult.Success, result);
        }

        [Fact]
        public void Validate_WhenContainsVbscript_ReturnsError()
        {
            var attribute = new SanitizedStringAttribute();
            var context = CreateValidationContext("vbscript:msgbox");

            var result = attribute.GetValidationResult("vbscript:msgbox", context);

            Assert.NotEqual(ValidationResult.Success, result);
        }

        [Fact]
        public void Validate_WhenContainsDataTextHtml_ReturnsError()
        {
            var attribute = new SanitizedStringAttribute();
            var context = CreateValidationContext("data:text/html,<script>alert(1)</script>");

            var result = attribute.GetValidationResult("data:text/html,<script>alert(1)</script>", context);

            Assert.NotEqual(ValidationResult.Success, result);
        }

        [Fact]
        public void Validate_WhenNotString_ReturnsError()
        {
            var attribute = new SanitizedStringAttribute();
            var context = CreateValidationContext(123);

            var result = attribute.GetValidationResult(123, context);

            Assert.NotEqual(ValidationResult.Success, result);
            Assert.Contains("must be a string", result!.ErrorMessage);
        }

        [Theory]
        [InlineData("<script>alert(1)</script>")]
        [InlineData("<SCRIPT>alert(1)</SCRIPT>")]
        [InlineData("<ScRiPt>alert(1)</ScRiPt>")]
        public void Validate_WhenMixedCaseScriptTag_ReturnsError(string input)
        {
            var attribute = new SanitizedStringAttribute();
            var context = CreateValidationContext(input);

            var result = attribute.GetValidationResult(input, context);

            Assert.NotEqual(ValidationResult.Success, result);
        }

        [Theory]
        [InlineData("JAVASCRIPT:alert(1)")]
        [InlineData("JavaScript:alert(1)")]
        [InlineData("jAvAsCrIpT:alert(1)")]
        public void Validate_WhenMixedCaseJavascript_ReturnsError(string input)
        {
            var attribute = new SanitizedStringAttribute();
            var context = CreateValidationContext(input);

            var result = attribute.GetValidationResult(input, context);

            Assert.NotEqual(ValidationResult.Success, result);
        }

        [Theory]
        [InlineData("Hello World")]
        [InlineData("Product Name 123")]
        [InlineData("Special chars: @#$%")]
        [InlineData("Unicode: αινσϊ")]
        [InlineData("Numbers and symbols: 100% off!")]
        public void Validate_WhenLegitimateContent_ReturnsSuccess(string input)
        {
            var attribute = new SanitizedStringAttribute();
            var context = CreateValidationContext(input);

            var result = attribute.GetValidationResult(input, context);

            Assert.Equal(ValidationResult.Success, result);
        }

        private ValidationContext CreateValidationContext(object? value)
        {
            var testObject = new TestModel { TestProperty = value?.ToString() };
            var context = new ValidationContext(testObject)
            {
                MemberName = "TestProperty",
                DisplayName = "TestProperty"
            };

            var serviceProvider = new TestServiceProvider(_sanitizationService);
            context.InitializeServiceProvider(type => serviceProvider.GetService(type));

            return context;
        }

        private class TestModel
        {
            public string? TestProperty { get; set; }
        }

        private class TestServiceProvider : IServiceProvider
        {
            private readonly IInputSanitizationService _sanitizationService;

            public TestServiceProvider(IInputSanitizationService sanitizationService)
            {
                _sanitizationService = sanitizationService;
            }

            public object? GetService(Type serviceType)
            {
                if (serviceType == typeof(IInputSanitizationService))
                {
                    return _sanitizationService;
                }

                return null;
            }
        }
    }
}

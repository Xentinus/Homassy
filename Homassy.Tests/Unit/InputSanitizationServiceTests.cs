using Homassy.API.Services.Sanitization;

namespace Homassy.Tests.Unit
{
    public class InputSanitizationServiceTests
    {
        private readonly InputSanitizationService _service;

        public InputSanitizationServiceTests()
        {
            _service = new InputSanitizationService();
        }

        [Fact]
        public void SanitizePlainText_WhenNull_ReturnsEmptyString()
        {
            var result = _service.SanitizePlainText(null);

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void SanitizePlainText_WhenWhitespace_ReturnsEmptyString()
        {
            var result = _service.SanitizePlainText("   ");

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void SanitizePlainText_WhenValidText_ReturnsSameText()
        {
            var input = "Hello World";

            var result = _service.SanitizePlainText(input);

            Assert.Equal(input, result);
        }

        [Fact]
        public void SanitizePlainText_WhenContainsScriptTag_EncodesHtml()
        {
            var input = "<script>alert('xss')</script>";

            var result = _service.SanitizePlainText(input);

            Assert.Contains("&lt;script&gt;", result);
            Assert.DoesNotContain("<script>", result);
        }

        [Fact]
        public void SanitizePlainText_WhenContainsImgOnError_EncodesHtml()
        {
            var input = "<img src=x onerror=alert('xss')>";

            var result = _service.SanitizePlainText(input);

            Assert.Contains("&lt;img", result);
            Assert.DoesNotContain("<img", result);
        }

        [Fact]
        public void SanitizePlainText_WhenContainsJavascriptUri_EncodesHtml()
        {
            var input = "<a href=\"javascript:alert('xss')\">Click</a>";

            var result = _service.SanitizePlainText(input);

            Assert.Contains("&lt;a", result);
            Assert.DoesNotContain("<a", result);
        }

        [Fact]
        public void SanitizePlainText_WhenExcessiveWhitespace_NormalizesWhitespace()
        {
            var input = "Hello    World";

            var result = _service.SanitizePlainText(input);

            Assert.Equal("Hello World", result);
        }

        [Fact]
        public void SanitizePlainText_WhenLeadingTrailingWhitespace_TrimsWhitespace()
        {
            var input = "  Hello World  ";

            var result = _service.SanitizePlainText(input);

            Assert.Equal("Hello World", result);
        }

        [Fact]
        public void SanitizePlainTextOrNull_WhenNull_ReturnsNull()
        {
            var result = _service.SanitizePlainTextOrNull(null);

            Assert.Null(result);
        }

        [Fact]
        public void SanitizePlainTextOrNull_WhenWhitespace_ReturnsNull()
        {
            var result = _service.SanitizePlainTextOrNull("   ");

            Assert.Null(result);
        }

        [Fact]
        public void SanitizePlainTextOrNull_WhenValidText_ReturnsSanitizedText()
        {
            var input = "Hello World";

            var result = _service.SanitizePlainTextOrNull(input);

            Assert.Equal(input, result);
        }

        [Theory]
        [InlineData("<script>alert('xss')</script>")]
        [InlineData("<img src=x onerror=alert(1)>")]
        [InlineData("<svg onload=alert(1)>")]
        public void SanitizePlainText_WhenContainsXssPayload_EncodesPayload(string xssPayload)
        {
            var result = _service.SanitizePlainText(xssPayload);

            Assert.DoesNotContain("<script>", result);
            Assert.DoesNotContain("<img", result);
            Assert.DoesNotContain("<svg", result);
            Assert.Contains("&lt;", result);
        }
    }
}

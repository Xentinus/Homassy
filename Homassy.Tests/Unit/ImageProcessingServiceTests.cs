using Homassy.API.Enums;
using Homassy.API.Models.ImageUpload;
using Homassy.API.Services;

namespace Homassy.Tests.Unit
{
    public class ImageProcessingServiceTests
    {
        private readonly ImageProcessingService _service;

        public ImageProcessingServiceTests()
        {
            _service = new ImageProcessingService();
        }

        #region ValidateImage - Empty/Null Input
        [Fact]
        public void ValidateImage_WhenNull_ReturnsEmptyFileError()
        {
            var result = _service.ValidateImage(null!);

            Assert.False(result.IsValid);
            Assert.Equal(ImageValidationError.EmptyFile, result.Error);
        }

        [Fact]
        public void ValidateImage_WhenEmpty_ReturnsEmptyFileError()
        {
            var result = _service.ValidateImage(string.Empty);

            Assert.False(result.IsValid);
            Assert.Equal(ImageValidationError.EmptyFile, result.Error);
        }

        [Fact]
        public void ValidateImage_WhenWhitespace_ReturnsEmptyFileError()
        {
            var result = _service.ValidateImage("   ");

            Assert.False(result.IsValid);
            Assert.Equal(ImageValidationError.EmptyFile, result.Error);
        }
        #endregion

        #region ValidateImage - Invalid Base64
        [Fact]
        public void ValidateImage_WhenInvalidBase64_ReturnsInvalidBase64Error()
        {
            var result = _service.ValidateImage("not-valid-base64!!!");

            Assert.False(result.IsValid);
            Assert.Equal(ImageValidationError.InvalidBase64, result.Error);
        }
        #endregion

        #region ValidateImage - Valid JPEG
        [Fact]
        public void ValidateImage_WhenValidJpeg_ReturnsSuccess()
        {
            var jpegBytes = CreateMinimalJpeg();
            var base64 = Convert.ToBase64String(jpegBytes);

            var options = new ImageProcessingOptions
            {
                MinWidth = 1,
                MinHeight = 1
            };

            var result = _service.ValidateImage(base64, options);

            Assert.True(result.IsValid);
            Assert.Equal(ImageFormat.Jpeg, result.DetectedFormat);
            Assert.True(result.Width > 0);
            Assert.True(result.Height > 0);
        }

        [Fact]
        public void ValidateImage_WhenJpegWithDataUrlPrefix_ReturnsSuccess()
        {
            var jpegBytes = CreateMinimalJpeg();
            var base64 = $"data:image/jpeg;base64,{Convert.ToBase64String(jpegBytes)}";

            var options = new ImageProcessingOptions
            {
                MinWidth = 1,
                MinHeight = 1
            };

            var result = _service.ValidateImage(base64, options);

            Assert.True(result.IsValid);
            Assert.Equal(ImageFormat.Jpeg, result.DetectedFormat);
        }
        #endregion

        #region ValidateImage - Valid PNG
        [Fact]
        public void ValidateImage_WhenValidPng_ReturnsSuccess()
        {
            var pngBytes = CreateMinimalPng();
            var base64 = Convert.ToBase64String(pngBytes);

            var options = new ImageProcessingOptions
            {
                MinWidth = 1,
                MinHeight = 1
            };

            var result = _service.ValidateImage(base64, options);

            Assert.True(result.IsValid);
            Assert.Equal(ImageFormat.Png, result.DetectedFormat);
            Assert.Equal(1, result.Width);
            Assert.Equal(1, result.Height);
        }
        #endregion

        #region ValidateImage - Unsupported GIF
        [Fact]
        public void ValidateImage_WhenGif_ReturnsInvalidFormatError()
        {
            var gifBytes = CreateMinimalGif();
            var base64 = Convert.ToBase64String(gifBytes);

            var result = _service.ValidateImage(base64);

            Assert.False(result.IsValid);
            Assert.Equal(ImageValidationError.InvalidFormat, result.Error);
        }
        #endregion

        #region ValidateImage - File Too Large
        [Fact]
        public void ValidateImage_WhenFileTooLarge_ReturnsFileTooLargeError()
        {
            var jpegBytes = CreateMinimalJpeg();
            var base64 = Convert.ToBase64String(jpegBytes);

            var options = new ImageProcessingOptions
            {
                MaxFileSizeBytes = 10
            };

            var result = _service.ValidateImage(base64, options);

            Assert.False(result.IsValid);
            Assert.Equal(ImageValidationError.FileTooLarge, result.Error);
        }
        #endregion

        #region ValidateImage - Unsupported Format
        [Fact]
        public void ValidateImage_WhenFormatNotAllowed_ReturnsUnsupportedFormatError()
        {
            var pngBytes = CreateMinimalPng();
            var base64 = Convert.ToBase64String(pngBytes);

            var options = new ImageProcessingOptions
            {
                MinWidth = 1,
                MinHeight = 1,
                AllowedFormats = [ImageFormat.Jpeg]
            };

            var result = _service.ValidateImage(base64, options);

            Assert.False(result.IsValid);
            Assert.Equal(ImageValidationError.UnsupportedFormat, result.Error);
        }
        #endregion

        #region ValidateImage - Invalid Format
        [Fact]
        public void ValidateImage_WhenNotAnImage_ReturnsInvalidFormatError()
        {
            var textBytes = System.Text.Encoding.UTF8.GetBytes("This is not an image file content");
            var base64 = Convert.ToBase64String(textBytes);

            var result = _service.ValidateImage(base64);

            Assert.False(result.IsValid);
            Assert.Equal(ImageValidationError.InvalidFormat, result.Error);
        }
        #endregion

        #region ValidateImage - Dimensions Too Small
        [Fact]
        public void ValidateImage_WhenDimensionsTooSmall_ReturnsDimensionsTooSmallError()
        {
            var pngBytes = CreateMinimalPng();
            var base64 = Convert.ToBase64String(pngBytes);

            var options = new ImageProcessingOptions
            {
                MinWidth = 100,
                MinHeight = 100
            };

            var result = _service.ValidateImage(base64, options);

            Assert.False(result.IsValid);
            Assert.Equal(ImageValidationError.DimensionsTooSmall, result.Error);
        }
        #endregion

        #region ProcessImage - Returns Null When Invalid
        [Fact]
        public void ProcessImage_WhenInvalidImage_ReturnsNull()
        {
            var result = _service.ProcessImage("invalid-base64!!!");

            Assert.Null(result);
        }

        [Fact]
        public void ProcessImage_WhenEmpty_ReturnsNull()
        {
            var result = _service.ProcessImage(string.Empty);

            Assert.Null(result);
        }
        #endregion

        #region ProcessImage - Valid Image
        [Fact]
        public void ProcessImage_WhenValidJpeg_ReturnsProcessedImage()
        {
            var jpegBytes = CreateMinimalJpeg();
            var base64 = Convert.ToBase64String(jpegBytes);

            var options = new ImageProcessingOptions
            {
                MinWidth = 1,
                MinHeight = 1
            };

            var result = _service.ProcessImage(base64, options);

            Assert.NotNull(result);
            Assert.Equal(ImageFormat.Jpeg, result.Format);
            Assert.NotEmpty(result.Base64);
            Assert.True(result.Data.Length > 0);
            Assert.Equal("image/jpeg", result.ContentType);
        }

        [Fact]
        public void ProcessImage_WhenValidPng_ReturnsProcessedImage()
        {
            var pngBytes = CreateMinimalPng();
            var base64 = Convert.ToBase64String(pngBytes);

            var options = new ImageProcessingOptions
            {
                MinWidth = 1,
                MinHeight = 1
            };

            var result = _service.ProcessImage(base64, options);

            Assert.NotNull(result);
            Assert.Equal(ImageFormat.Png, result.Format);
            Assert.Equal("image/png", result.ContentType);
        }
        #endregion

        #region ProcessImageAsync
        [Fact]
        public async Task ProcessImageAsync_WhenValidJpeg_ReturnsProcessedImage()
        {
            var jpegBytes = CreateMinimalJpeg();
            var base64 = Convert.ToBase64String(jpegBytes);

            var options = new ImageProcessingOptions
            {
                MinWidth = 1,
                MinHeight = 1
            };

            var result = await _service.ProcessImageAsync(base64, options);

            Assert.NotNull(result);
            Assert.Equal(ImageFormat.Jpeg, result.Format);
        }

        [Fact]
        public async Task ProcessImageAsync_WhenCancelled_ThrowsTaskCanceledException()
        {
            var jpegBytes = CreateMinimalJpeg();
            var base64 = Convert.ToBase64String(jpegBytes);
            var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<TaskCanceledException>(
                () => _service.ProcessImageAsync(base64, null, cts.Token));
        }
        #endregion

        #region ImageProcessingOptions Defaults
        [Fact]
        public void ImageProcessingOptions_HasCorrectDefaults()
        {
            var options = new ImageProcessingOptions();

            Assert.Equal(800, options.MaxWidth);
            Assert.Equal(800, options.MaxHeight);
            Assert.Equal(50, options.MinWidth);
            Assert.Equal(50, options.MinHeight);
            Assert.Equal(5 * 1024 * 1024, options.MaxFileSizeBytes);
            Assert.Equal(80, options.JpegQuality);
            Assert.Equal(ImageFormat.Jpeg, options.OutputFormat);
            Assert.Contains(ImageFormat.Jpeg, options.AllowedFormats);
            Assert.Contains(ImageFormat.Png, options.AllowedFormats);
            Assert.Contains(ImageFormat.WebP, options.AllowedFormats);
            Assert.Equal(3, options.AllowedFormats.Count);
        }
        #endregion

        #region ImageValidationResult Factory Methods
        [Fact]
        public void ImageValidationResult_Success_CreatesValidResult()
        {
            var result = ImageValidationResult.Success(ImageFormat.Jpeg, 100, 200, 5000);

            Assert.True(result.IsValid);
            Assert.Equal(ImageValidationError.None, result.Error);
            Assert.Equal(ImageFormat.Jpeg, result.DetectedFormat);
            Assert.Equal(100, result.Width);
            Assert.Equal(200, result.Height);
            Assert.Equal(5000, result.FileSizeBytes);
            Assert.Empty(result.ErrorMessage);
        }

        [Fact]
        public void ImageValidationResult_Failure_CreatesInvalidResult()
        {
            var result = ImageValidationResult.Failure(ImageValidationError.FileTooLarge, "File is too large");

            Assert.False(result.IsValid);
            Assert.Equal(ImageValidationError.FileTooLarge, result.Error);
            Assert.Equal("File is too large", result.ErrorMessage);
            Assert.Equal(ImageFormat.Unknown, result.DetectedFormat);
        }
        #endregion

        #region Helper Methods
        private static byte[] CreateMinimalJpeg()
        {
            return
            [
                0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01,
                0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
                0xFF, 0xDB, 0x00, 0x43, 0x00, 0x08, 0x06, 0x06, 0x07, 0x06, 0x05, 0x08,
                0x07, 0x07, 0x07, 0x09, 0x09, 0x08, 0x0A, 0x0C, 0x14, 0x0D, 0x0C, 0x0B,
                0x0B, 0x0C, 0x19, 0x12, 0x13, 0x0F, 0x14, 0x1D, 0x1A, 0x1F, 0x1E, 0x1D,
                0x1A, 0x1C, 0x1C, 0x20, 0x24, 0x2E, 0x27, 0x20, 0x22, 0x2C, 0x23, 0x1C,
                0x1C, 0x28, 0x37, 0x29, 0x2C, 0x30, 0x31, 0x34, 0x34, 0x34, 0x1F, 0x27,
                0x39, 0x3D, 0x38, 0x32, 0x3C, 0x2E, 0x33, 0x34, 0x32,
                0xFF, 0xC0, 0x00, 0x0B, 0x08, 0x00, 0x64, 0x00, 0x64, 0x01, 0x01, 0x11, 0x00,
                0xFF, 0xC4, 0x00, 0x1F, 0x00, 0x00, 0x01, 0x05, 0x01, 0x01, 0x01, 0x01,
                0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x02,
                0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B,
                0xFF, 0xC4, 0x00, 0xB5, 0x10, 0x00, 0x02, 0x01, 0x03, 0x03, 0x02, 0x04,
                0x03, 0x05, 0x05, 0x04, 0x04, 0x00, 0x00, 0x01, 0x7D, 0x01, 0x02, 0x03,
                0x00, 0x04, 0x11, 0x05, 0x12, 0x21, 0x31, 0x41, 0x06, 0x13, 0x51, 0x61,
                0x07, 0x22, 0x71, 0x14, 0x32, 0x81, 0x91, 0xA1, 0x08, 0x23, 0x42, 0xB1,
                0xC1, 0x15, 0x52, 0xD1, 0xF0, 0x24, 0x33, 0x62, 0x72, 0x82, 0x09, 0x0A,
                0x16, 0x17, 0x18, 0x19, 0x1A, 0x25, 0x26, 0x27, 0x28, 0x29, 0x2A, 0x34,
                0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48,
                0x49, 0x4A, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5A, 0x63, 0x64,
                0x65, 0x66, 0x67, 0x68, 0x69, 0x6A, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78,
                0x79, 0x7A, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89, 0x8A, 0x92, 0x93,
                0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9A, 0xA2, 0xA3, 0xA4, 0xA5, 0xA6,
                0xA7, 0xA8, 0xA9, 0xAA, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6, 0xB7, 0xB8, 0xB9,
                0xBA, 0xC2, 0xC3, 0xC4, 0xC5, 0xC6, 0xC7, 0xC8, 0xC9, 0xCA, 0xD2, 0xD3,
                0xD4, 0xD5, 0xD6, 0xD7, 0xD8, 0xD9, 0xDA, 0xE1, 0xE2, 0xE3, 0xE4, 0xE5,
                0xE6, 0xE7, 0xE8, 0xE9, 0xEA, 0xF1, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6, 0xF7,
                0xF8, 0xF9, 0xFA,
                0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00,
                0xFB, 0xD5, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0xFF, 0xD9
            ];
        }

        private static byte[] CreateMinimalPng()
        {
            return
            [
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
                0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
                0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
                0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
                0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41,
                0x54, 0x08, 0xD7, 0x63, 0xF8, 0x0F, 0x00, 0x00,
                0x01, 0x01, 0x01, 0x00, 0x05, 0xFE, 0xDC, 0xCC,
                0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44,
                0xAE, 0x42, 0x60, 0x82
            ];
        }

        private static byte[] CreateMinimalGif()
        {
            return
            [
                0x47, 0x49, 0x46, 0x38, 0x39, 0x61,
                0x01, 0x00, 0x01, 0x00,
                0x80, 0x00, 0x00,
                0xFF, 0xFF, 0xFF,
                0x00, 0x00, 0x00,
                0x21, 0xF9, 0x04, 0x01, 0x00, 0x00, 0x00, 0x00,
                0x2C, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
                0x02, 0x02, 0x44, 0x01, 0x00,
                0x3B
            ];
        }
        #endregion
    }
}

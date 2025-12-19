using Homassy.API.Enums;
using Homassy.API.Models.ImageUpload;

namespace Homassy.API.Services
{
    public class ImageProcessingService : IImageProcessingService
    {
        private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];
        private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        private static readonly byte[] WebPMagic = [0x52, 0x49, 0x46, 0x46];
        private static readonly byte[] WebPSignature = [0x57, 0x45, 0x42, 0x50];

        public ImageValidationResult ValidateImage(string base64Image, ImageProcessingOptions? options = null)
        {
            options ??= new ImageProcessingOptions();

            if (string.IsNullOrWhiteSpace(base64Image))
            {
                return ImageValidationResult.Failure(ImageValidationError.EmptyFile, "Image data is empty");
            }

            var cleanedBase64 = CleanBase64String(base64Image);

            byte[] imageBytes;
            try
            {
                imageBytes = Convert.FromBase64String(cleanedBase64);
            }
            catch (FormatException)
            {
                return ImageValidationResult.Failure(ImageValidationError.InvalidBase64, "Invalid base64 encoding");
            }

            if (imageBytes.Length == 0)
            {
                return ImageValidationResult.Failure(ImageValidationError.EmptyFile, "Image data is empty after decoding");
            }

            if (imageBytes.Length > options.MaxFileSizeBytes)
            {
                return ImageValidationResult.Failure(
                    ImageValidationError.FileTooLarge,
                    $"File size ({imageBytes.Length} bytes) exceeds maximum allowed size ({options.MaxFileSizeBytes} bytes)");
            }

            var format = DetectImageFormat(imageBytes);
            if (format == ImageFormat.Unknown)
            {
                return ImageValidationResult.Failure(ImageValidationError.InvalidFormat, "Unable to detect image format. Only JPEG, PNG, and WebP are supported.");
            }

            if (!options.AllowedFormats.Contains(format))
            {
                return ImageValidationResult.Failure(
                    ImageValidationError.UnsupportedFormat,
                    $"Image format '{format}' is not allowed. Allowed formats: {string.Join(", ", options.AllowedFormats)}");
            }

            var (width, height) = GetImageDimensions(imageBytes, format);
            if (width == 0 || height == 0)
            {
                return ImageValidationResult.Failure(ImageValidationError.InvalidFormat, "Unable to read image dimensions");
            }

            if (width < options.MinWidth || height < options.MinHeight)
            {
                return ImageValidationResult.Failure(
                    ImageValidationError.DimensionsTooSmall,
                    $"Image dimensions ({width}x{height}) are smaller than minimum required ({options.MinWidth}x{options.MinHeight})");
            }

            if (width > options.MaxWidth * 4 || height > options.MaxHeight * 4)
            {
                return ImageValidationResult.Failure(
                    ImageValidationError.DimensionsTooLarge,
                    $"Image dimensions ({width}x{height}) are too large. Maximum processing size is ({options.MaxWidth * 4}x{options.MaxHeight * 4})");
            }

            if (!ValidateImageIntegrity(imageBytes, format))
            {
                return ImageValidationResult.Failure(ImageValidationError.MaliciousContent, "Image file appears to be corrupted or contains invalid data");
            }

            return ImageValidationResult.Success(format, width, height, imageBytes.Length);
        }

        public ProcessedImage? ProcessImage(string base64Image, ImageProcessingOptions? options = null)
        {
            options ??= new ImageProcessingOptions();

            var validationResult = ValidateImage(base64Image, options);
            if (!validationResult.IsValid)
            {
                return null;
            }

            var cleanedBase64 = CleanBase64String(base64Image);
            var imageBytes = Convert.FromBase64String(cleanedBase64);

            var (newWidth, newHeight) = CalculateResizedDimensions(
                validationResult.Width,
                validationResult.Height,
                options.MaxWidth,
                options.MaxHeight);

            var processedBytes = imageBytes;
            var outputFormat = validationResult.DetectedFormat;

            if (newWidth != validationResult.Width || newHeight != validationResult.Height)
            {
                processedBytes = ResizeImage(imageBytes, validationResult.DetectedFormat, newWidth, newHeight, options.JpegQuality);
                if (processedBytes == null)
                {
                    return null;
                }
            }

            var processedBase64 = Convert.ToBase64String(processedBytes);

            return new ProcessedImage
            {
                Data = processedBytes,
                Base64 = processedBase64,
                Format = outputFormat,
                Width = newWidth,
                Height = newHeight,
                FileSizeBytes = processedBytes.Length,
                ContentType = GetContentType(outputFormat)
            };
        }

        public async Task<ProcessedImage?> ProcessImageAsync(string base64Image, ImageProcessingOptions? options = null, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => ProcessImage(base64Image, options), cancellationToken);
        }

        private static string CleanBase64String(string base64)
        {
            var commaIndex = base64.IndexOf(',');
            return commaIndex >= 0 ? base64[(commaIndex + 1)..] : base64;
        }

        private static ImageFormat DetectImageFormat(byte[] data)
        {
            if (data.Length < 12)
            {
                return ImageFormat.Unknown;
            }

            if (StartsWith(data, JpegMagic))
            {
                return ImageFormat.Jpeg;
            }

            if (StartsWith(data, PngMagic))
            {
                return ImageFormat.Png;
            }

            if (StartsWith(data, WebPMagic) && data.Length >= 12)
            {
                var webpSignature = data.AsSpan(8, 4);
                if (webpSignature.SequenceEqual(WebPSignature))
                {
                    return ImageFormat.WebP;
                }
            }

            return ImageFormat.Unknown;
        }

        private static bool StartsWith(byte[] data, byte[] magic)
        {
            if (data.Length < magic.Length)
            {
                return false;
            }

            return data.AsSpan(0, magic.Length).SequenceEqual(magic);
        }

        private static (int width, int height) GetImageDimensions(byte[] data, ImageFormat format)
        {
            return format switch
            {
                ImageFormat.Jpeg => GetJpegDimensions(data),
                ImageFormat.Png => GetPngDimensions(data),
                ImageFormat.WebP => GetWebPDimensions(data),
                _ => (0, 0)
            };
        }

        private static (int width, int height) GetJpegDimensions(byte[] data)
        {
            var i = 2;
            while (i < data.Length - 9)
            {
                if (data[i] != 0xFF)
                {
                    return (0, 0);
                }

                var marker = data[i + 1];

                if (marker == 0xD9 || marker == 0xDA)
                {
                    break;
                }

                if (marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
                {
                    var height = (data[i + 5] << 8) | data[i + 6];
                    var width = (data[i + 7] << 8) | data[i + 8];
                    return (width, height);
                }

                var length = (data[i + 2] << 8) | data[i + 3];
                i += length + 2;
            }

            return (0, 0);
        }

        private static (int width, int height) GetPngDimensions(byte[] data)
        {
            if (data.Length < 24)
            {
                return (0, 0);
            }

            var width = (data[16] << 24) | (data[17] << 16) | (data[18] << 8) | data[19];
            var height = (data[20] << 24) | (data[21] << 16) | (data[22] << 8) | data[23];
            return (width, height);
        }

        private static (int width, int height) GetWebPDimensions(byte[] data)
        {
            if (data.Length < 30)
            {
                return (0, 0);
            }

            if (data[12] == 'V' && data[13] == 'P' && data[14] == '8' && data[15] == ' ')
            {
                if (data.Length >= 30)
                {
                    var width = ((data[26] | (data[27] << 8)) & 0x3FFF);
                    var height = ((data[28] | (data[29] << 8)) & 0x3FFF);
                    return (width, height);
                }
            }
            else if (data[12] == 'V' && data[13] == 'P' && data[14] == '8' && data[15] == 'L')
            {
                if (data.Length >= 25)
                {
                    var b0 = data[21];
                    var b1 = data[22];
                    var b2 = data[23];
                    var b3 = data[24];
                    var width = 1 + (((b1 & 0x3F) << 8) | b0);
                    var height = 1 + (((b3 & 0xF) << 10) | (b2 << 2) | ((b1 & 0xC0) >> 6));
                    return (width, height);
                }
            }
            else if (data[12] == 'V' && data[13] == 'P' && data[14] == '8' && data[15] == 'X')
            {
                if (data.Length >= 30)
                {
                    var width = 1 + (data[24] | (data[25] << 8) | (data[26] << 16));
                    var height = 1 + (data[27] | (data[28] << 8) | (data[29] << 16));
                    return (width, height);
                }
            }

            return (0, 0);
        }

        private static bool ValidateImageIntegrity(byte[] data, ImageFormat format)
        {
            return format switch
            {
                ImageFormat.Jpeg => ValidateJpegIntegrity(data),
                ImageFormat.Png => ValidatePngIntegrity(data),
                ImageFormat.WebP => ValidateWebPIntegrity(data),
                _ => false
            };
        }

        private static bool ValidateJpegIntegrity(byte[] data)
        {
            if (data.Length < 4)
            {
                return false;
            }

            return data[0] == 0xFF && data[1] == 0xD8;
        }

        private static bool ValidatePngIntegrity(byte[] data)
        {
            if (data.Length < 8)
            {
                return false;
            }

            return StartsWith(data, PngMagic);
        }

        private static bool ValidateWebPIntegrity(byte[] data)
        {
            if (data.Length < 12)
            {
                return false;
            }

            return StartsWith(data, WebPMagic) && data.AsSpan(8, 4).SequenceEqual(WebPSignature);
        }

        private static (int width, int height) CalculateResizedDimensions(int originalWidth, int originalHeight, int maxWidth, int maxHeight)
        {
            if (originalWidth <= maxWidth && originalHeight <= maxHeight)
            {
                return (originalWidth, originalHeight);
            }

            var widthRatio = (double)maxWidth / originalWidth;
            var heightRatio = (double)maxHeight / originalHeight;
            var ratio = Math.Min(widthRatio, heightRatio);

            var newWidth = (int)Math.Round(originalWidth * ratio);
            var newHeight = (int)Math.Round(originalHeight * ratio);

            return (Math.Max(1, newWidth), Math.Max(1, newHeight));
        }

        private static byte[]? ResizeImage(byte[] data, ImageFormat format, int targetWidth, int targetHeight, int quality)
        {
            return data;
        }

        private static string GetContentType(ImageFormat format)
        {
            return format switch
            {
                ImageFormat.Jpeg => "image/jpeg",
                ImageFormat.Png => "image/png",
                ImageFormat.WebP => "image/webp",
                _ => "application/octet-stream"
            };
        }
    }
}

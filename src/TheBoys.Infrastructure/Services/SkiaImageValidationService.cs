using System.Text;
using SkiaSharp;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;

namespace TheBoys.Infrastructure.Services;

public sealed class SkiaImageValidationService : IImageValidationService
{
    private const long MaxFileSize = 15 * 1024 * 1024; // 15 MiB
    private const int MinFileSize = 12; // 12 bytes
    private const long MaxPixelCount = 40_000_000; // 40 Megapixels per SRS

    public Result<ValidatedImageInfo> ValidateImage(byte[] imageBytes, string claimedMimeType)
    {
        if (imageBytes == null || imageBytes.Length < MinFileSize || imageBytes.Length > MaxFileSize)
            return Result<ValidatedImageInfo>.Failure(Error.Validation("ImageSizeOutOfRange", "Dung lượng ảnh phải từ 12 bytes đến 15 MB."));

        var detectedMime = DetectMimeBySignature(imageBytes);
        if (detectedMime == null)
            return Result<ValidatedImageInfo>.Failure(Error.Validation("UnsupportedFormat", "Chỉ hỗ trợ ảnh định dạng PNG, JPEG hoặc WEBP hợp lệ."));

        if (!string.IsNullOrWhiteSpace(claimedMimeType) &&
            !string.Equals(claimedMimeType.Trim(), detectedMime, StringComparison.OrdinalIgnoreCase))
        {
            return Result<ValidatedImageInfo>.Failure(Error.Validation("MimeTypeMismatch", $"Định dạng ảnh thực tế ({detectedMime}) không khớp với MIME type được khai báo ({claimedMimeType})."));
        }

        try
        {
            using var stream = new MemoryStream(imageBytes, writable: false);
            using var codec = SKCodec.Create(stream);
            if (codec == null)
                return Result<ValidatedImageInfo>.Failure(Error.Validation("CorruptImage", "Tệp ảnh không hợp lệ hoặc bị lỗi header định dạng."));

            var width = codec.Info.Width;
            var height = codec.Info.Height;

            if (width <= 0 || height <= 0)
                return Result<ValidatedImageInfo>.Failure(Error.Validation("InvalidDimensions", "Kích thước ảnh không hợp lệ."));

            long pixelCount = (long)width * height;
            if (pixelCount > MaxPixelCount)
                return Result<ValidatedImageInfo>.Failure(Error.Validation("ImageTooLarge", $"Độ phân giải ảnh ({pixelCount:N0} pixels) vượt quá giới hạn tối đa 40 megapixel ({MaxPixelCount:N0} pixels)."));

            // Actual pixel decode test to catch truncated/corrupted payload behind valid headers
            using var data = SKData.CreateCopy(imageBytes);
            using var image = SKImage.FromEncodedData(data);
            if (image == null || image.Width <= 0 || image.Height <= 0)
                return Result<ValidatedImageInfo>.Failure(Error.Validation("CorruptImageData", "Dữ liệu pixel ảnh bị hỏng không thể giải mã."));

            using var bitmap = SKBitmap.FromImage(image);
            if (bitmap == null || bitmap.Width <= 0 || bitmap.Height <= 0)
                return Result<ValidatedImageInfo>.Failure(Error.Validation("CorruptImageData", "Dữ liệu pixel ảnh bị hỏng không thể giải mã."));

            return Result<ValidatedImageInfo>.Success(new ValidatedImageInfo(
                detectedMime,
                width,
                height,
                pixelCount,
                imageBytes.Length
            ));
        }
        catch (Exception)
        {
            return Result<ValidatedImageInfo>.Failure(Error.Validation("DecodeFailure", "Không thể giải mã dữ liệu ảnh. Tệp có thể đã bị hỏng."));
        }
    }

    private static string? DetectMimeBySignature(byte[] bytes)
    {
        if (bytes.Length < 12) return null;

        // PNG signature: 89 50 4E 47 0D 0A 1A 0A
        if (bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            return "image/png";

        // JPEG SOI signature: FF D8 FF
        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";

        // WEBP signature: RIFF....WEBP
        if (bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F' &&
            bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P')
            return "image/webp";

        return null;
    }
}


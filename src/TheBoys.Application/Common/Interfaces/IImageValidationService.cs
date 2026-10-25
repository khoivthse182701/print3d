using TheBoys.Application.Common.Models;

namespace TheBoys.Application.Common.Interfaces;

public sealed record ValidatedImageInfo(
    string DetectedMimeType,
    int Width,
    int Height,
    long PixelCount,
    long FileSizeBytes
);

public interface IImageValidationService
{
    Result<ValidatedImageInfo> ValidateImage(byte[] imageBytes, string claimedMimeType);
}


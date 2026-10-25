namespace TheBoys.Application.Common.Interfaces;

public interface IContentModerationService
{
    bool IsTextSafe(string? text);
    bool IsImageSafe(byte[]? imageBytes);
    bool IsContentSafe(string? text, byte[]? imageBytes);
}

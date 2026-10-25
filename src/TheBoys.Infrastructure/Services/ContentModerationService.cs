using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;
using TheBoys.Application.Common.Interfaces;

namespace TheBoys.Infrastructure.Services;

public sealed class ContentModerationService : IContentModerationService
{
    private static readonly string[] MultiWordBlacklist =
    [
        "khoa than", "khỏa thân", "tinh duc", "tình dục", "khieu dam", "khiêu dâm",
        "lam tinh", "làm tình", "dam dang", "dâm đãng", "loan luan", "loạn luân",
        "duong vat", "dương vật", "am dao", "âm đạo", "am ho", "âm hộ",
        "doi truy", "đồi trụy", "bao luc", "bạo lực", "chem giet", "chém giết",
        "mau me", "máu me", "dau roi mau chay", "đầu rơi máu chảy", "tu tu", "tự tử",
        "vu khi", "vũ khí", "thuoc no", "thuốc nổ"
    ];

    private static readonly string[] SingleWordBlacklist =
    [
        "nude", "nudity", "naked", "porn", "porno", "pornography", "xxx", "sex", "sexy", "erotic", "hentai",
        "boob", "boobs", "breast", "breasts", "vagina", "pussy", "penis", "dick", "dildo", "masturbat", "orgasm",
        "gore", "blood", "decapitat", "murder", "kill", "torture", "suicide", "weapon", "weapons", "rifle", "bomb",
        "vú", "ngực", "lồn", "buồi", "cặc", "súng", "bom"
    ];

    private static readonly Regex[] SingleWordRegexes = SingleWordBlacklist
        .Select(w => new Regex($@"\b{Regex.Escape(w)}", RegexOptions.IgnoreCase | RegexOptions.Compiled))
        .ToArray();

    public bool IsTextSafe(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;

        var lower = text.Trim().ToLowerInvariant();
        var unaccented = RemoveDiacritics(lower);

        // Check multi-word blacklist
        foreach (var phrase in MultiWordBlacklist)
        {
            if (lower.Contains(phrase, StringComparison.OrdinalIgnoreCase) ||
                unaccented.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Check single-word regex with word boundaries
        foreach (var regex in SingleWordRegexes)
        {
            if (regex.IsMatch(lower) || regex.IsMatch(unaccented))
            {
                return false;
            }
        }

        return true;
    }

    public bool IsImageSafe(byte[]? imageBytes)
    {
        if (imageBytes == null || imageBytes.Length < 12) return true;

        try
        {
            using var stream = new MemoryStream(imageBytes, writable: false);
            using var codec = SKCodec.Create(stream);
            if (codec == null) return true; // If codec cannot decode, let validation service handle it

            using var originalBitmap = SKBitmap.Decode(codec);
            if (originalBitmap == null || originalBitmap.Width <= 0 || originalBitmap.Height <= 0) return true;

            // Fast downsampling for skin tone cluster analysis
            var targetW = Math.Min(originalBitmap.Width, 128);
            var targetH = Math.Min(originalBitmap.Height, 128);

            SKBitmap analysisBitmap;
            bool isResized = false;
            if (originalBitmap.Width > targetW || originalBitmap.Height > targetH)
            {
                analysisBitmap = originalBitmap.Resize(new SKImageInfo(targetW, targetH), SKSamplingOptions.Default) ?? originalBitmap;
                isResized = analysisBitmap != originalBitmap;
            }
            else
            {
                analysisBitmap = originalBitmap;
            }

            try
            {
                int skinPixels = 0;
                int totalPixels = analysisBitmap.Width * analysisBitmap.Height;

                for (int y = 0; y < analysisBitmap.Height; y++)
                {
                    for (int x = 0; x < analysisBitmap.Width; x++)
                    {
                        var color = analysisBitmap.GetPixel(x, y);
                        if (color.Alpha < 50) continue;

                        byte r = color.Red;
                        byte g = color.Green;
                        byte b = color.Blue;

                        // ITU-R BT.601 YCbCr
                        double yVal = 0.299 * r + 0.587 * g + 0.114 * b;
                        double cb = -0.168736 * r - 0.331264 * g + 0.5 * b + 128;
                        double cr = 0.5 * r - 0.418688 * r - 0.081312 * b + 128;

                        // Skin tone cluster: Y > 50, 77 <= Cb <= 135, 133 <= Cr <= 177, R > G > B, (R - G) >= 15
                        if (yVal > 50 && cb >= 77 && cb <= 135 && cr >= 133 && cr <= 177 && r > g && g > b && (r - g) >= 15)
                        {
                            skinPixels++;
                        }
                    }
                }

                double skinRatio = (double)skinPixels / Math.Max(1, totalPixels);
                return skinRatio <= 0.45; // Reject if skin ratio > 45%
            }
            finally
            {
                if (isResized)
                {
                    analysisBitmap.Dispose();
                }
            }
        }
        catch
        {
            return true; // Graceful fallback on unexpected decode errors
        }
    }

    public bool IsContentSafe(string? text, byte[]? imageBytes)
    {
        return IsTextSafe(text) && IsImageSafe(imageBytes);
    }

    private static string RemoveDiacritics(string text)
    {
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder(capacity: normalizedString.Length);

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace("đ", "d")
            .Replace("Đ", "D");
    }
}

using System.Text.RegularExpressions;

namespace TheBoys.Infrastructure.Security;

public static class PiiAndSecretRedactor
{
    private static readonly Regex ApiKeyRegex = new(@"(msy_[A-Za-z0-9_\-]{8,}|tsk_[A-Za-z0-9_\-]{8,})", RegexOptions.Compiled);
    private static readonly Regex BearerRegex = new(@"Bearer\s+([A-Za-z0-9\-\._~\+\/]+=*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PasswordJsonRegex = new(@"(""(?:password|passwordHash|secret|token|apiKey|connectionString)""\s*:\s*"")([^""]+)("")", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ConnStrPasswordRegex = new(@"(Password|pwd)\s*=\s*([^;]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string RedactText(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        var result = ApiKeyRegex.Replace(input, "[REDACTED_API_KEY]");
        result = BearerRegex.Replace(result, "Bearer [REDACTED_TOKEN]");
        result = PasswordJsonRegex.Replace(result, "$1[REDACTED]$3");
        result = ConnStrPasswordRegex.Replace(result, "$1=[REDACTED]");
        return result;
    }

    public static string MaskApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return string.Empty;
        if (apiKey.Length <= 8) return "[REDACTED]";
        return string.Concat(apiKey.AsSpan(0, 4), "...", apiKey.AsSpan(apiKey.Length - 4, 4));
    }
}


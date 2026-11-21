using ProxyConfig.Core.Models;

namespace ProxyConfig.Core.Validation;

public static class HostLoggingValidator
{
    public static IReadOnlyList<string> Validate(HostLoggingOptions options)
    {
        var errors = new List<string>();
        if (options.AccessLogEnabled && !SafeFile(options.AccessLogPath))
            errors.Add("Access log must be a safe target-server file path.");
        if (!SafeFile(options.ErrorLogPath))
            errors.Add("Error log must be a safe target-server file path.");
        if (!Enum.IsDefined(options.ErrorLogLevel))
            errors.Add("Unsupported error log severity.");
        return errors;
    }

    private static bool SafeFile(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        !path.Any(ch => char.IsControl(ch) || ch is '"' or '\'' or ';' or '#' or '{' or '}' or '<' or '>' or '|') &&
        !path.Contains("..", StringComparison.Ordinal);
}

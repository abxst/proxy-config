namespace ProxyConfig.Core.Models;

public enum ErrorLogLevel
{
    Error,
    Warn,
    Info,
    Debug
}

/// <summary>Host-level access/error log paths on the target server.</summary>
public sealed record HostLoggingOptions
{
    public bool AccessLogEnabled { get; init; } = true;
    public string AccessLogPath { get; init; } = "logs/access.log";
    public string ErrorLogPath { get; init; } = "logs/error.log";
    public ErrorLogLevel ErrorLogLevel { get; init; } = ErrorLogLevel.Warn;
    public TargetPlatform TargetPlatform { get; init; } = TargetPlatform.Linux;
}

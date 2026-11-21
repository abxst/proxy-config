using ProxyConfig.Core.Models;

namespace ProxyConfig.Core.Validation;

public static class StreamProxyValidator
{
    public static IReadOnlyList<string> Validate(StreamProxyOptions options)
    {
        var errors = new List<string>();
        if (options.ListenPort is < 1 or > 65535)
            errors.Add("Stream listen port must be between 1 and 65535.");
        if (!SafeTcpEndpoint.TryNormalize(options.TargetEndpoint, out _))
            errors.Add("Stream target must be an explicit hostname:port or [IPv6]:port.");
        if (options.ProxyTimeoutSeconds is < 1 or > 3600)
            errors.Add("Proxy timeout must be between 1 and 3600 seconds.");
        return errors;
    }
}

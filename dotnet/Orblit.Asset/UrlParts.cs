namespace Orblit.Asset;

/// <summary>The pieces of a <see cref="WebUrl"/>, each already in its normal form.</summary>
internal readonly record struct UrlParts(
    string Scheme,
    bool HasAuthority,
    string UserInfo,
    string Host,
    int? Port,
    string Path,
    string? Query,
    string? Fragment)
{
    /// <summary>The port a scheme leaves out of its text. Dart counts zero as the default for every other scheme.</summary>
    internal static int DefaultPort(string scheme) => scheme switch
    {
        "http" => 80,
        "https" => 443,
        _ => 0,
    };

    /// <summary>These parts with the port dropped when it is the scheme's own.</summary>
    internal UrlParts Normalised() => this with { Port = Port == DefaultPort(Scheme) ? null : Port };

    public override string ToString()
    {
        var text = new System.Text.StringBuilder();
        if (Scheme.Length > 0)
        {
            text.Append(Scheme).Append(':');
        }

        if (HasAuthority)
        {
            text.Append("//");
            if (UserInfo.Length > 0)
            {
                text.Append(UserInfo).Append('@');
            }

            text.Append(Host.Contains(':') ? $"[{Host}]" : Host);
            if (Port is { } port)
            {
                text.Append(':').Append(port);
            }
        }

        text.Append(Path);
        if (Query is not null)
        {
            text.Append('?').Append(Query);
        }

        if (Fragment is not null)
        {
            text.Append('#').Append(Fragment);
        }

        return text.ToString();
    }
}

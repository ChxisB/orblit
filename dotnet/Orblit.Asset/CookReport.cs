namespace Orblit.Asset;

/// <summary>What happened to all of them.</summary>
public sealed class CookReport(IEnumerable<CookResult> results)
{
    public IReadOnlyList<CookResult> Results { get; } = [.. results];

    public IEnumerable<CookResult> WithStatus(CookStatus status) => Results.Where(result => result.Status == status);

    public int Count(CookStatus status) => WithStatus(status).Count();

    /// <summary>
    /// Nothing failed. A report with nothing in it is fine by this measure, so a caller that expected assets
    /// should check for them itself.
    /// </summary>
    public bool Ok => Results.All(result => result.Ok);

    /// <summary>
    /// Every asset that went into every asset cooked, which is what a build hook watches so that editing a
    /// texture rebuilds the model using it.
    /// </summary>
    public IReadOnlySet<AssetId> Dependencies => Results.SelectMany(result => result.Dependencies).ToHashSet();

    /// <summary>One line, for the end of a cook.</summary>
    public string Summary
    {
        get
        {
            var parts = Enum.GetValues<CookStatus>()
                .Where(status => Count(status) > 0)
                .Select(status => $"{Count(status)} {status.ToString().ToLowerInvariant()}")
                .ToList();
            return parts.Count == 0 ? "nothing to cook" : string.Join(", ", parts);
        }
    }

    public override string ToString() => Summary;
}

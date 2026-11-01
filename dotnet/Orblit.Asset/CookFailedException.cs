namespace Orblit.Asset;

/// <summary>
/// Thrown when a cook run as part of a build could not cook everything.
/// A build hook that returned quietly after failing to cook a texture would produce an app that builds, installs,
/// launches and then cannot draw one of its own assets. Failing the build is the cheaper of the two.
/// </summary>
public sealed class CookFailedException(CookReport report) : Exception
{
    public CookReport Report { get; } = report;

    /// <summary>
    /// Only the results that failed, which is what somebody reading a build log needs. The rest of the report is
    /// there for anyone who wants it.
    /// </summary>
    public IEnumerable<CookResult> Failures => Report.WithStatus(CookStatus.Failed);

    public override string Message
    {
        get
        {
            var lines = Failures.Select(failure => $"  {failure.Id}: {failure.Error?.Message}").ToList();
            return $"Cooking assets failed for {lines.Count} of {Report.Results.Count}:\n{string.Join('\n', lines)}";
        }
    }
}

namespace Orblit.Asset;

/// <summary>
/// One file a cook produced. A cook is rarely one file out: a texture becomes a KTX2 for each
/// family the targets need, and they travel together because they were made together. Half of a
/// cook is not a hit.
/// </summary>
public sealed record CookOutput
{
    public CookOutput(string name, ContentHash hash, long bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        Name = name;
        Hash = hash;
        Bytes = bytes;
    }

    /// <summary>
    /// What this output is called among the cook's outputs: <c>wall.astc.ktx2</c>. A name within the
    /// cook, not an asset id, because where the file ends up is the caller's business.
    /// </summary>
    public string Name { get; }

    /// <summary>What the output is, byte for byte, and how to get it back out of the store.</summary>
    public ContentHash Hash { get; }

    /// <summary>
    /// How many bytes that is, so a cache can add up what it is holding without reading any of it.
    /// </summary>
    public long Bytes { get; }

    public override string ToString() => $"CookOutput({Name}, {Hash}, {Bytes} bytes)";
}

/// <summary>What one cook produced, all of it.</summary>
public sealed class CookedAsset
{
    public CookedAsset(IEnumerable<CookOutput> outputs) =>
        Outputs = [.. outputs.OrderBy(output => output.Name, StringComparer.Ordinal)];

    /// <summary>
    /// Every file the cook produced, in name order, so that two cooks of the same thing compare
    /// equal and a manifest built from them does not reorder itself between runs.
    /// </summary>
    public IReadOnlyList<CookOutput> Outputs { get; }

    /// <summary>The output called <paramref name="name"/>, or null when the cook did not produce one.</summary>
    public CookOutput? this[string name] => Outputs.FirstOrDefault(output => output.Name == name);

    /// <summary>How many bytes the whole cook came to.</summary>
    public long Bytes => Outputs.Sum(output => output.Bytes);

    public override string ToString() => $"CookedAsset({string.Join(", ", Outputs)})";
}

/// <summary>
/// Why cooking a key did not work, remembered so that something can say so without cooking again.
/// A cache of failures is only ever for reporting. A cook never reads one to decide whether to try:
/// importers fail for reasons that are not in the key, such as a tool that was not installed or a
/// disk that was full.
/// </summary>
public sealed record CookFailure(string Reason, DateTimeOffset At)
{
    public override string ToString() => $"CookFailure({Reason}, at {At.UtcDateTime:O})";
}

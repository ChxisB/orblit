namespace Orblit.Filament;

/// <summary>
/// A bundled asset as a place in a file: where to open, where it starts and how long it is.
/// </summary>
public sealed record AssetFile
{
    public AssetFile(string path, long offset, long length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        Path = path;
        Offset = offset;
        Length = length;
    }

    /// <summary>A path the process can open and seek in.</summary>
    public string Path { get; }

    /// <summary>Where the asset begins in the file at <see cref="Path"/>.</summary>
    public long Offset { get; }

    /// <summary>How many bytes the asset is.</summary>
    public long Length { get; }
}

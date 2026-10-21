namespace Orblit.Asset;

/// <summary>
/// Assets read from a directory on disk, an id's slashes being its folders. What a project uses
/// while it is being worked on, so that saving a texture in an image editor is all it takes for the
/// next read to see it.
/// </summary>
/// <remarks>
/// Nothing outside the directory can be read through it. An <see cref="AssetId"/> cannot spell
/// <c>..</c> in the first place, but a link inside the directory can still lead out of it, so every
/// file is followed to where it really is and refused unless that is still inside. A link from one
/// place in the directory to another is fine. The check happens just before the read, so a link
/// swapped in between the two could still slip past. It stops a project's files reaching out by
/// accident, or a downloaded project doing it on purpose, and does not sandbox a hostile process on
/// the same machine.
/// </remarks>
public sealed class DirectoryAssetSource(string rootPath) : IAssetSource
{
    /// <summary>The directory ids are relative to, as it was given.</summary>
    public string RootPath { get; } = rootPath;

    public async Task<byte[]> ReadAsync(AssetId id)
    {
        var path = Locate(id);
        try
        {
            return await File.ReadAllBytesAsync(path).ConfigureAwait(false);
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new AssetNotFoundException(id);
        }
    }

    public Task<bool> ExistsAsync(AssetId id)
    {
        try
        {
            Locate(id);
            return Task.FromResult(true);
        }
        catch (AssetNotFoundException)
        {
            return Task.FromResult(false);
        }
    }

    // Where the id really is on disk, or an AssetNotFoundException saying why it is not readable from
    // here. The root has its own links resolved as well as the file, and fresh on every call.
    private string Locate(AssetId id)
    {
        string root;
        try
        {
            root = RealPath.Of(RootPath);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new AssetNotFoundException(id, $"the directory \"{RootPath}\" does not exist");
        }

        var separator = Path.DirectorySeparatorChar;
        var inside = root.EndsWith(separator) ? root : $"{root}{separator}";
        var path = inside + id.ToString().Replace('/', separator);

        // A folder is not an asset, and a link to nothing is not one either.
        if (!File.Exists(path))
        {
            throw new AssetNotFoundException(id);
        }

        var real = RealPath.Of(path);
        return real.StartsWith(inside, StringComparison.Ordinal)
            ? real
            : throw new AssetNotFoundException(id, $"it is reached through a link that leads outside \"{RootPath}\"");
    }
}

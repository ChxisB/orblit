using Orblit.Filament;

namespace Orblit.Examples;

/// <summary>
/// Where an example finds files it does not carry, and how it hands them to
/// the renderer. Read as bytes, the way a browser, a phone's package or a
/// download would give them, so the example works in all of them.
/// </summary>
public interface ISampleFiles
{
    /// <summary>The bytes at <paramref name="path"/>, or null when there is no file there.</summary>
    Task<byte[]?> ReadAsync(string path);

    /// <summary>Gives the renderer <paramref name="bytes"/> under <paramref name="name"/>, for a scene to name.</summary>
    void Provide(string name, byte[] bytes);
}

/// <summary>Files from the local disk, handed to the renderer's resource table.</summary>
public sealed class DiskSamples : ISampleFiles
{
    public async Task<byte[]?> ReadAsync(string path) =>
        File.Exists(path) ? await File.ReadAllBytesAsync(path) : null;

    public void Provide(string name, byte[] bytes) => RenderResources.Provide(name, bytes);
}

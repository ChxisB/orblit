using Orblit.Sprite;

namespace Orblit.Examples;

/// <summary>
/// Where an example keeps the pictures it draws, so its scene can name them by
/// path. They are drawn rather than shipped, so the examples carry no assets
/// and say exactly what their textures are.
/// </summary>
public interface IPictureFolder
{
    /// <summary>
    /// Keeps a square RGBA8 picture for <paramref name="example"/> and gives the
    /// path a texture names it by. Null when it could not be kept, and the
    /// example draws without it.
    /// </summary>
    string? Keep(string example, string name, int size, byte[] rgba);
}

/// <summary>Pictures kept as PNG files, in a folder for each example under the temporary folder.</summary>
public sealed class TempPictureFolder : IPictureFolder
{
    public string? Keep(string example, string name, int size, byte[] rgba)
    {
        var folder = Path.Combine(Path.GetTempPath(), $"orblit_gallery_{example}");
        var path = Path.Combine(folder, name);
        try
        {
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(path, PngCodec.Encode(size, size, rgba));
            return path;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

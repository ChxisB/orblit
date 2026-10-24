namespace Orblit.Filament;

/// <summary>
/// An image on disk, used as one of a material's maps. A path rather than bytes, because the same
/// file is usually on many materials and the renderer loads each one once however many ask for it.
/// Two textures are equal when they name the same file in the same colour space, which is how the
/// message decides to send a file once.
/// </summary>
/// <param name="Path">An absolute path to a PNG, JPEG or KTX2 file.</param>
/// <param name="Srgb">
/// Whether the file holds colour. Normals, masks and roughness are numbers, and reading them as
/// colour bends every value.
/// </param>
public sealed record Texture(string Path, bool Srgb = true);

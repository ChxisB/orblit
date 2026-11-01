using Orblit.Mesh;

namespace Orblit.Scene;

/// <summary>
/// A scene as a glTF document and the bytes it refers to.
/// </summary>
/// <remarks>
/// The two are kept apart because the same document is written two ways: a <c>.gltf</c> puts the
/// bytes in a file beside it, a <c>.glb</c> puts them in a chunk after it. Deciding that here would
/// mean writing the scene twice.
/// </remarks>
/// <param name="Json">The document.</param>
/// <param name="Binary">The bytes its views point into.</param>
/// <param name="Problems">
/// What could not be written exactly, in the order it was met. Reported rather than thrown: a scene
/// that lost one texture is still a scene worth having, and the caller is the one who knows whether
/// to show this to somebody or fail the build over it.
/// </param>
public sealed record GltfScene(
    OrderedDictionary<string, object?> Json,
    byte[] Binary,
    IReadOnlyList<string> Problems);

/// <summary>
/// The document being written and everything the parts of the writer share: the buffer the views
/// land in, the problems met, and the extensions used.
/// </summary>
internal sealed class GltfOutput(IReadOnlyDictionary<string, byte[]> files)
{
    /// <summary>The bytes of anything the scene points at, by the path the scene names it with.</summary>
    public IReadOnlyDictionary<string, byte[]> Files { get; } = files;

    public GltfBuffer Buffer { get; } = new();

    public List<string> Problems { get; } = [];

    /// <summary>Sorted at the end, so the order the file lists them in does not depend on the scene's.</summary>
    public SortedSet<string> Used { get; } = new(StringComparer.Ordinal);

    public OrderedDictionary<string, object?> Json { get; } = new()
    {
        ["asset"] = new OrderedDictionary<string, object?> { ["version"] = "2.0", ["generator"] = "Orblit" },
        ["nodes"] = new List<object?>(),
        ["meshes"] = new List<object?>(),
        ["materials"] = new List<object?>(),
        ["cameras"] = new List<object?>(),
        ["textures"] = new List<object?>(),
        ["images"] = new List<object?>(),
        ["samplers"] = new List<object?>(),
    };

    /// <summary>One of the document's arrays. Each is made when the document is, so there is always one.</summary>
    public List<object?> Section(string name) => (List<object?>)Json[name]!; // Json made them all above

    /// <summary>Adds an entry to one of the document's arrays, returning where it landed.</summary>
    public long Add(string name, OrderedDictionary<string, object?> entry)
    {
        var section = Section(name);
        section.Add(entry);
        return section.Count - 1;
    }

    /// <summary>The document's own block for one extension, made when it is first asked for.</summary>
    public OrderedDictionary<string, object?> Document(string extension)
    {
        if (!Json.TryGetValue("extensions", out var held))
        {
            held = Json["extensions"] = new OrderedDictionary<string, object?>();
        }

        var extensions = (OrderedDictionary<string, object?>)held!; // made just above or by a graft, as a map
        if (!extensions.TryGetValue(extension, out var block))
        {
            block = extensions[extension] = new OrderedDictionary<string, object?>();
        }

        return (OrderedDictionary<string, object?>)block!; // made just above or by a graft, as a map
    }
}

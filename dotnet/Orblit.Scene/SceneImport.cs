using Orblit.Light;
using Orblit.Mesh;

namespace Orblit.Scene;

/// <summary>
/// A scene read out of a glTF or OBJ document.
/// </summary>
/// <remarks>
/// <see cref="Problems"/> is returned rather than thrown for the reason every other reader in this
/// package returns it: a document with one node nobody could make sense of is still a scene worth
/// opening, and the caller is the one who knows whether to show the list to somebody or fail a build
/// over it.
/// </remarks>
public sealed class SceneImported(SceneDocument document, IReadOnlyList<string>? problems = null, bool wasWrittenHere = false)
{
    public SceneDocument Document { get; } = document;

    /// <summary>What could not be carried across, in the order it was met.</summary>
    public IReadOnlyList<string> Problems { get; } = problems ?? [];

    /// <summary>
    /// Whether the document carried Orblit's own record of the scene.
    /// <para>
    /// True means this is the scene that was exported, with its ids, its components and its settings,
    /// not an interpretation of one. False means the document came from somewhere else and what is
    /// here was worked out from the parts of glTF that have an equivalent, which is worth saying out
    /// loud before somebody saves it over the original.
    /// </para>
    /// </summary>
    public bool WasWrittenHere { get; } = wasWrittenHere;

    public bool HasProblems => Problems.Count > 0;
}

/// <summary>
/// Reading a scene back from the bytes of a model file.
/// </summary>
public static class SceneImport
{
    private static ReadOnlySpan<byte> ByteOrderMark => [0xEF, 0xBB, 0xBF];

    /// <summary>
    /// Reads a scene out of the bytes of a <c>.glb</c>, a <c>.gltf</c> or an <c>.obj</c>.
    /// <para>
    /// An OBJ is read as one entity for each of its groups, by <see cref="ObjToScene"/>. It is told
    /// from glTF by its first byte, because no OBJ starts with a brace and no glTF starts with
    /// anything else.
    /// </para>
    /// <para>
    /// Which of the two it is comes from the bytes rather than from a name: a <c>.gltf</c> that is
    /// really a GLB is an ordinary thing to be handed, and a reader that trusts the extension fails
    /// on it with a JSON error nobody can act on.
    /// </para>
    /// <para>
    /// <paramref name="files"/> supplies whatever the document points at by name, keyed by the URI
    /// the document names it by. The <c>.bin</c> beside a <c>.gltf</c> is the one that matters. It
    /// takes bytes rather than a way to read them for the reason the export side does: this package
    /// has no filesystem in it, because the same code has to run in the editor, in a cook step and in
    /// a browser.
    /// </para>
    /// <para>
    /// Throws <see cref="SceneFormatException"/> only when there is nothing to read at all: bytes
    /// that are neither a GLB nor JSON, or JSON that is not a document. Everything short of that comes
    /// back in <see cref="SceneImported.Problems"/>, because a scene with one unreadable node is
    /// still a scene worth opening.
    /// </para>
    /// </summary>
    public static SceneImported ReadSceneFrom(
        byte[] bytes,
        string name = "Scene",
        IReadOnlyDictionary<string, byte[]>? files = null)
    {
        if (!IsGltf(bytes))
        {
            return ObjToScene(bytes, name, files);
        }

        (JsonMap Json, byte[]? Binary) parts;
        try
        {
            parts = GltfFiles.GltfParts(bytes, files);
        }
        catch (FormatException error)
        {
            throw new SceneFormatException(error.Message);
        }

        return GltfToScene(parts.Json, parts.Binary, name);
    }

    /// <summary>
    /// Reads <paramref name="json"/> back into a scene.
    /// <para>
    /// <paramref name="binary"/> is the buffer the document refers to: a GLB's <c>BIN</c> chunk, or
    /// the <c>.bin</c> beside a <c>.gltf</c>. It is only read for a document written elsewhere, since
    /// one written here states its geometry in the scene's own terms and does not need the accessors
    /// decoded to get it back.
    /// </para>
    /// <para>
    /// The exporter writes every entity's id, visibility and components onto the node's
    /// <c>extras.orblit</c>, so a document that came from <c>WriteAs</c> comes back as the scene it
    /// was rather than as a flattened photograph of one. A document from anywhere else is read from
    /// the parts of glTF that have an equivalent here: the tree, the names, the transforms, the
    /// lights, the cameras, and triangles as editable geometry.
    /// </para>
    /// </summary>
    public static SceneImported GltfToScene(JsonMap json, byte[]? binary = null, string name = "Scene") =>
        new GltfReader(json, binary, name).Read();

    /// <summary>
    /// Reads a scene out of the bytes of a Wavefront <c>.obj</c>.
    /// <para>
    /// Each <c>o</c> or <c>g</c> group becomes one entity holding its faces as editable geometry,
    /// which is how a glTF node with a mesh is read. An OBJ has no tree, no lights and no cameras, so
    /// the entities sit at the top level and have no transform.
    /// </para>
    /// <para>
    /// <paramref name="files"/> holds the <c>.mtl</c> the file names. What the reader could not read
    /// comes back in <see cref="SceneImported.Problems"/>.
    /// </para>
    /// </summary>
    public static SceneImported ObjToScene(
        byte[] bytes,
        string name = "Scene",
        IReadOnlyDictionary<string, byte[]>? files = null)
    {
        ObjModel model;
        try
        {
            model = ObjFiles.ReadObj(bytes, files);
        }
        catch (FormatException error)
        {
            throw new SceneFormatException(error.Message);
        }

        var problems = new List<string>(model.Problems);
        var entities = new List<SceneEntity>();
        foreach (var group in model.Groups)
        {
            var part = model.MeshOf(group);
            var wears = part.Faces.Select(face => face.Material).Distinct().Count();
            if (wears > 1)
            {
                problems.Add($"Group {group.Name} wears {wears} materials. The colour of the first was kept for all of it.");
            }

            var components = new Dictionary<string, SceneComponent> { [SceneComponents.Mesh] = ObjMesh(model, part) };
            entities.Add(new SceneEntity($"group-{entities.Count}", group.Name, components: components));
        }

        return new SceneImported(new SceneDocument(name, entities: entities), problems);
    }

    private static MeshComponent ObjMesh(ObjModel model, MeshGeometry part)
    {
        var at = part.Faces[0].Material;

        // Faces are drawn in one colour, so the material indices stop here.
        foreach (var face in part.Faces)
        {
            face.Material = 0;
        }

        if (at >= model.Materials.Count)
        {
            return new MeshComponent(geometry: part, authored: true);
        }

        var colour = model.Materials[at].Colour;
        return new MeshComponent(
            geometry: part,
            colour: Tint.FromLinear(new Vector3(colour[0], colour[1], colour[2])),
            authored: true);
    }

    /// <summary>
    /// Whether <paramref name="bytes"/> begin as a GLB does, or as the JSON object of a
    /// <c>.gltf</c>. A <c>[</c> counts, so a JSON array is refused as a bad glTF and not read as an
    /// OBJ that has no faces.
    /// </summary>
    private static bool IsGltf(byte[] bytes)
    {
        if (bytes.AsSpan().StartsWith("glTF"u8))
        {
            return true;
        }

        // A .gltf saved with a byte order mark still starts as JSON.
        var first = Array.FindIndex(bytes, bytes.AsSpan().StartsWith(ByteOrderMark) ? ByteOrderMark.Length : 0, static one => one > 32);
        return first >= 0 && bytes[first] is 0x7B or 0x5B;
    }
}

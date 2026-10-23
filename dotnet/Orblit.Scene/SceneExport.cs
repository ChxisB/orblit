using System.Text;
using Orblit.Mesh;

namespace Orblit.Scene;

/// <summary>
/// What a whole scene can be written as.
/// </summary>
/// <remarks>
/// FBX is deliberately not here. Reading one is worth the trouble because people have them; writing
/// one is a proprietary format with no public specification, and everything that opens an FBX opens
/// a glTF.
/// </remarks>
public enum SceneFormat
{
    /// <summary>
    /// JSON with the bytes in a file beside it. The one to read in a text editor, and the one to put
    /// in version control.
    /// </summary>
    Gltf,

    /// <summary>The same document with the bytes packed in after it. One file, and the one to ship.</summary>
    Glb,

    /// <summary>Corners and faces in world space, with a material library. Everything reads it and it keeps almost nothing.</summary>
    Obj,
}

public static class SceneFormats
{
    extension(SceneFormat format)
    {
        /// <summary>As it is written in a menu.</summary>
        public string Label => format switch
        {
            SceneFormat.Gltf => "glTF",
            SceneFormat.Glb => "GLB",
            SceneFormat.Obj => "OBJ",
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };

        /// <summary>The file extension, dot included.</summary>
        public string Extension => format switch
        {
            SceneFormat.Gltf => ".gltf",
            SceneFormat.Glb => ".glb",
            SceneFormat.Obj => ".obj",
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };

        /// <summary>
        /// Whether the format keeps the tree, the lights, the cameras and the animation, rather than a
        /// flattened copy of the shapes.
        /// </summary>
        public bool KeepsScene => format != SceneFormat.Obj;
    }
}

/// <summary>The files an export produced, and what it could not carry.</summary>
/// <param name="Files">
/// In the order they should be saved. The first is the one to name the export after; the rest are
/// the sidecars it points at.
/// </param>
/// <param name="Problems">
/// What the format could not hold, in the order it was met. Empty is the ordinary case and means
/// nothing was lost.
/// </param>
public sealed record SceneWritten(IReadOnlyList<Written> Files, IReadOnlyList<string> Problems)
{
    public Written First => Files[0];
}

/// <summary>Writing a whole scene out.</summary>
public static class SceneExport
{
    extension(SceneDocument scene)
    {
        /// <summary>
        /// This scene as <paramref name="format"/>.
        /// </summary>
        /// <param name="format">What to write.</param>
        /// <param name="name">The stem the files are named by.</param>
        /// <param name="materials">
        /// Resolves the <c>.omat</c> paths the scene names, without which a material is the colour its
        /// mesh was drawn in.
        /// </param>
        /// <param name="files">
        /// The bytes of anything the scene points at, an imported model or a texture, keyed by the
        /// project path the scene names it by.
        /// </param>
        /// <remarks>
        /// Bytes rather than a way to read them, deliberately. This package has no filesystem in it
        /// and is not going to acquire one: the same export has to run in the editor, in a cook step
        /// and in a browser, and only the caller knows which of those it is in.
        /// </remarks>
        public SceneWritten WriteAs(
            SceneFormat format,
            string name = "scene",
            MaterialLibrary? materials = null,
            IReadOnlyDictionary<string, byte[]>? files = null) => format switch
            {
                SceneFormat.Obj => ObjFiles(scene.ToObj(materials, $"{name}.mtl"), name),
                SceneFormat.Glb => GlbFiles(scene.ToGltf(materials, files), name),
                SceneFormat.Gltf => GltfFilesOf(scene.ToGltf(materials, files), name),
                _ => throw new ArgumentOutOfRangeException(nameof(format)),
            };

        /// <summary>This scene as a glTF document and the bytes it points at.</summary>
        public GltfScene ToGltf(MaterialLibrary? materials = null, IReadOnlyDictionary<string, byte[]>? files = null) =>
            new GltfExporter(scene, materials, files ?? new Dictionary<string, byte[]>()).Write();

        /// <summary>This scene flattened into an OBJ and the material library it names.</summary>
        public ObjScene ToObj(MaterialLibrary? materials = null, string library = "scene.mtl") =>
            new ObjWriter(scene, materials, library).Write();
    }

    private static SceneWritten ObjFiles(ObjScene written, string name) => new(
        [
            new Written($"{name}.obj", Encoding.UTF8.GetBytes(written.Obj)),
            new Written($"{name}.mtl", Encoding.UTF8.GetBytes(written.Mtl)),
        ],
        written.Problems);

    private static SceneWritten GlbFiles(GltfScene written, string name) => new(
        [new Written($"{name}.glb", GltfFiles.GlbBytes(written.Json, written.Binary))],
        written.Problems);

    private static SceneWritten GltfFilesOf(GltfScene written, string name)
    {
        var empty = written.Binary.Length == 0;
        var text = GltfFiles.GltfText(written.Json, empty ? null : $"{name}.bin", empty ? null : written.Binary.Length);
        List<Written> files = [new Written($"{name}.gltf", Encoding.UTF8.GetBytes(text))];
        if (!empty)
        {
            files.Add(new Written($"{name}.bin", written.Binary));
        }

        return new SceneWritten(files, written.Problems);
    }
}

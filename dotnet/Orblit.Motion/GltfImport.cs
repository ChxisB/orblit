using Orblit.Mesh;

namespace Orblit.Motion;

/// <summary>Reading the animations and skeletons out of a glTF file somebody else wrote.</summary>
public static class GltfImport
{
    /// <summary>
    /// Every animation in a <c>.glb</c> or a <c>.gltf</c>, as a clip.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A joint of a skin becomes a bone of the model playing the clip, named the way the model's skeleton names
    /// it, so the clip plays on the model it came out of and on any other with the same bones. Any other node
    /// becomes an entity, under the id a scene imported from the same file gives it.
    /// </para>
    /// <para>
    /// Nothing is resampled: a key in the file is a key in the clip, at the time the file has it, and the file's
    /// easing comes across as it is. A linear key stays linear, a step stays a step, and a cubic spline keeps its
    /// tangents. Numbers are written as short as they can be while still reading back as the float the file had,
    /// so a clip is not three times the size of the data it came from.
    /// </para>
    /// <para>
    /// What the file gives no word for is guessed the way an artist would expect: the frame rate is the common
    /// one every key sits on, and an animation whose name starts or ends with "loop" or "cycle" loops.
    /// </para>
    /// </remarks>
    /// <param name="bytes">The file.</param>
    /// <param name="files">Whatever the document names by URI, the <c>.bin</c> beside a <c>.gltf</c>.</param>
    /// <exception cref="ClipFormatException">When the bytes are not glTF at all.</exception>
    public static ClipsImported Clips(byte[] bytes, IReadOnlyDictionary<string, byte[]>? files = null)
    {
        var (nodes, accessors) = Open(bytes, files);
        return new GltfClipReader(nodes, accessors).Read();
    }

    /// <summary>
    /// Every skin in a <c>.glb</c> or a <c>.gltf</c>, as a skeleton standing at rest.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The bones are named as <see cref="Clips"/> names them, so a clip imported from one file is measured
    /// against the skeleton read from that file, and a model's skeleton names the same bones the clips made for
    /// it do. They come parents first, whatever order the file lists its joints in.
    /// </para>
    /// <para>
    /// A joint is taken to hang directly from the nearest joint above it. A skeleton with an ordinary node
    /// between two joints has that node's transform left out, which is how a rig exported for a game engine is
    /// written anyway.
    /// </para>
    /// </remarks>
    /// <param name="bytes">The file.</param>
    /// <param name="files">Whatever the document names by URI.</param>
    /// <exception cref="ClipFormatException">When the bytes are not glTF at all.</exception>
    public static IReadOnlyList<RestSkeleton> Skeletons(byte[] bytes, IReadOnlyDictionary<string, byte[]>? files = null) =>
        GltfSkeletons.Of(Open(bytes, files).Nodes);

    private static (GltfNodes Nodes, GltfAccessors Accessors) Open(byte[] bytes, IReadOnlyDictionary<string, byte[]>? files)
    {
        (IReadOnlyDictionary<string, object?> Json, byte[]? Binary) parts;
        try
        {
            parts = GltfFiles.GltfParts(bytes, files);
        }
        catch (FormatException error)
        {
            throw new ClipFormatException(error.Message);
        }

        return (new GltfNodes(parts.Json), new GltfAccessors(parts.Json, parts.Binary, []));
    }
}

using Orblit.VectorMath;

namespace Orblit.Filament;

/// <summary>
/// One thing drawn. Keys are the host's to invent and keep stable: any number will do, as long as
/// one object carries the same key for as long as it exists and no new object reuses a key still
/// in the scene.
/// </summary>
public sealed record RenderObject
{
    public required long Key { get; init; }

    public required Matrix4 Transform { get; init; }

    /// <summary>Linear RGB, not sRGB: the lighting maths happens in linear space, and a silent conversion goes unnoticed.</summary>
    public required Vector3 Colour { get; init; }

    /// <summary>An absolute path to a glTF or glb file, or null for the built-in cube. A file that cannot be read is drawn as the cube.</summary>
    public string? Mesh { get; init; }

    public bool CastShadows { get; init; } = true;

    public bool ReceiveShadows { get; init; } = true;

    public bool Visible { get; init; } = true;

    /// <summary>The key of the <see cref="RenderMaterial"/> this object wears, or null for the default. A key the scene lacks is drawn as the default.</summary>
    public long? Material { get; init; }

    /// <summary>A group of seven, nought to <see cref="RenderScene.MaxLayer"/>. Out of range is clamped.</summary>
    public int Layer { get; init; }

    /// <summary>How far each of the mesh's morph shapes is dialled in, nought to one. Null leaves what the file set.</summary>
    public IReadOnlyList<double>? MorphWeights { get; init; }

    /// <summary>Which of the file's clips plays. Null plays nothing and returns the object to the pose its file rests in.</summary>
    public ModelAnimation? Animation { get; init; }

    /// <summary>Which of the file's material variants the object wears, by its position in <see cref="AssetInfo.Variants"/>.</summary>
    public int? Variant { get; init; }

    /// <summary>Joints of the mesh's skins set by hand, after any clip.</summary>
    public IReadOnlyList<JointPose>? Joints { get; init; }

    /// <summary>
    /// The flag bits this object sends. The layer rides in the high bits because a parallel array
    /// of it would be a fourth buffer packed and crossed every frame to carry a byte.
    /// </summary>
    internal int Flags =>
        (CastShadows ? 1 : 0)
        | (ReceiveShadows ? 2 : 0)
        | (Visible ? 4 : 0)
        | (Math.Clamp(Layer, 0, RenderScene.MaxLayer) << 8);
}

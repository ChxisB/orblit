using Orblit.VectorMath;

namespace Orblit.Filament;

/// <summary>What happens to a population's members at the far end of its range.</summary>
public enum Fade
{
    /// <summary>They sink into the ground.</summary>
    Sink,

    /// <summary>They shrink to nothing where they stand.</summary>
    Shrink,

    /// <summary>They stay full size until they are cut off.</summary>
    None,
}

/// <summary>
/// Many copies of one mesh, drawn as a single thing. Grass, rubble and trees are populations: the
/// renderer takes a transform and a colour for each member, and a population that has not changed
/// since it was last sent costs nothing to send again.
/// </summary>
public sealed class Population
{
    /// <summary>Floats of bounds for each population: the minimum corner, then the maximum.</summary>
    public const int BoundsStride = 6;

    public Population(int key, float[] transforms, float[] colours, Bounds bounds)
    {
        if (transforms.Length != colours.Length / 3 * 16)
        {
            throw new ArgumentException(
                "One transform of sixteen and one colour of three for each.", nameof(transforms));
        }

        Key = key;
        Transforms = transforms;
        Colours = colours;
        Bounds = bounds;
    }

    /// <summary>This population's identity, and the key its revision is held under.</summary>
    public int Key { get; }

    /// <summary>Sixteen floats for each member, column major, in world space.</summary>
    public float[] Transforms { get; }

    /// <summary>Three floats for each member: its colour, linear.</summary>
    public float[] Colours { get; }

    /// <summary>A box round every member, which the renderer culls the whole population against.</summary>
    public Bounds Bounds { get; }

    /// <summary>How far from the camera members are drawn, in metres. Nought is no limit.</summary>
    public double Range { get; init; }

    /// <summary>The mesh each member is. Null is a unit cube.</summary>
    public string? Mesh { get; init; }

    /// <summary>
    /// Bumped whenever the members change. The renderer is sent the buffers only when the revision
    /// it holds differs, so a population that is edited without a bump never shows the edit.
    /// </summary>
    public int Revision { get; init; }

    public Fade Fade { get; init; } = Fade.Sink;

    public bool CastShadows { get; init; }

    public bool ReceiveShadows { get; init; } = true;

    public int Layer { get; init; }

    public int Count => Transforms.Length / 16;

    public bool IsVisible => Count > 0;

    /// <summary>
    /// Shadow bits at 0 and 1, the fade in two bits at 2 and 3 in the order the shader switches on,
    /// and the layer from bit 8 up.
    /// </summary>
    public int Flags =>
        (CastShadows ? 1 : 0)
        | (ReceiveShadows ? 2 : 0)
        | ((int)Fade << 2)
        | (Math.Clamp(Layer, 0, RenderScene.MaxLayer) << 8);
}

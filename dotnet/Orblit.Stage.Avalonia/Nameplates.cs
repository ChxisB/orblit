using Avalonia;
using Orblit.Filament;
using Orblit.Ui.Avalonia;
using Orblit.VectorMath;

namespace Orblit.Stage.Avalonia;

/// <summary>One label hanging in the scene: a name over a head, a number over a hit.</summary>
/// <param name="Id">Tells this label from the others. It is added to <see cref="NameplatesOptions.KeyBase"/> to make the label's object and material keys.</param>
/// <param name="Text">What it says.</param>
/// <param name="Position">The middle of the label.</param>
public sealed record Nameplate(int Id, string Text, Vector3 Position);

/// <summary>What <see cref="Nameplates"/> builds for one frame.</summary>
public sealed record NameplateBatch(IReadOnlyList<RenderObject> Objects, IReadOnlyList<RenderMaterial> Materials);

/// <summary>How far labels show, how many, how big, and where their picture goes.</summary>
public sealed record NameplatesOptions
{
    /// <summary>Where a label starts to fade, measured from the camera's position.</summary>
    public required double FadeStart { get; init; }

    /// <summary>Where a label is gone. It must be past <see cref="FadeStart"/>.</summary>
    public required double FadeEnd { get; init; }

    /// <summary>Past this only the nearest labels are kept.</summary>
    public int MaxLabels { get; init; } = 256;

    /// <summary>How big a label's text is in the world, so a 32 pixel line is <c>32 * MetresPerPixel</c> metres high.</summary>
    public double MetresPerPixel { get; init; } = 0.01;

    /// <summary>The first key this uses. Pick a range no other object or material is in.</summary>
    public long KeyBase { get; init; } = 0x4e500000;

    /// <summary>Names this picture, so two sets of labels never share one.</summary>
    public string Id { get; init; } = "nameplates";

    /// <summary>Where a picture's bytes go. The renderer's own store unless a test supplies one.</summary>
    public Action<string, byte[]> Provide { get; init; } = static (name, bytes) => RenderResources.Provide(name, bytes);

    public Func<string, bool> Release { get; init; } = RenderResources.Release;
}

/// <summary>
/// Many short labels drawn as quads that square up to the view.
/// </summary>
/// <remarks>
/// A scene with two hundred names above two hundred heads cannot afford two hundred widget trees. Every
/// distinct string is drawn once into the shared <see cref="Atlas"/>, and each label is a quad that shows
/// its string's rectangle of that one picture. Two labels with the same text share the rectangle.
/// <para>
/// <see cref="Sync"/> and <see cref="Build"/> are the two halves. Sync adds strings to the picture and
/// hands the renderer the new one. Build turns the labels of this frame into scene objects and materials,
/// nearest first, so it is a query that can be called every frame.
/// </para>
/// <para>
/// A label fades out between <see cref="NameplatesOptions.FadeStart"/> and
/// <see cref="NameplatesOptions.FadeEnd"/>, measured from the camera's position, and is dropped past
/// <see cref="NameplatesOptions.FadeEnd"/>. A label whose text Sync has not drawn yet is left out. There
/// is no test against walls, so a label behind one shows through it.
/// </para>
/// </remarks>
public sealed class Nameplates : IDisposable
{
    // A quad is the cube squashed to this depth.
    private const double Depth = 0.001;

    private (string Name, Size Size)? _shown;
    private int _pictures;

    public Nameplates(LabelAtlas atlas, NameplatesOptions options)
    {
        if (options.FadeStart >= options.FadeEnd)
        {
            throw new ArgumentException("The fade must start before it ends.", nameof(options));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(options.MaxLabels);
        Atlas = atlas;
        Options = options;
    }

    /// <summary>The picture every label reads. The caller sets its style and width, and this adds strings to it.</summary>
    public LabelAtlas Atlas { get; }

    public NameplatesOptions Options { get; }

    /// <summary>
    /// Draws any string the atlas lacks and hands the renderer the new picture. Put the scene together
    /// again afterwards, because <see cref="Build"/> names it. The old picture's bytes are released and its
    /// texture is not. The renderer keeps what it already made until it is disposed, so each new picture
    /// adds to memory. Add strings in batches and not one at a time.
    /// </summary>
    public void Sync(IEnumerable<string> texts)
    {
        foreach (var text in texts)
        {
            Atlas.Add(text);
        }

        if (!Atlas.IsDirty)
        {
            return;
        }

        var size = Atlas.Size;
        var next = RenderResources.NameFor($"panel/{Options.Id}/{++_pictures}.png");
        Options.Provide(next, Atlas.Paint());
        var old = _shown?.Name;
        _shown = (next, size);
        if (old is not null)
        {
            Options.Release(old);
        }
    }

    /// <summary>Lets go of the picture the renderer holds. The atlas stays the caller's.</summary>
    public void Dispose()
    {
        var name = _shown?.Name;
        _shown = null;
        if (name is not null)
        {
            Options.Release(name);
        }
    }

    /// <summary>The objects and materials to put in the scene for <paramref name="labels"/> seen from <paramref name="camera"/>. Empty until the first <see cref="Sync"/>.</summary>
    public NameplateBatch Build(RenderCamera camera, IEnumerable<Nameplate> labels)
    {
        if (_shown is not var (name, size))
        {
            return new NameplateBatch([], []);
        }

        var objects = new List<RenderObject>();
        var materials = new List<RenderMaterial>();
        var turn = SquareTo(camera);
        foreach (var (distance, label, rect) in Nearest(camera, labels))
        {
            var key = Options.KeyBase + label.Id;
            var scale = new Vector3(rect.Width * Options.MetresPerPixel / 2, rect.Height * Options.MetresPerPixel / 2, Depth);
            var alpha = Math.Clamp((Options.FadeEnd - distance) / (Options.FadeEnd - Options.FadeStart), 0, 1);
            objects.Add(new RenderObject
            {
                Key = key,
                Material = key,
                Transform = Matrix4.Compose(label.Position, turn, scale),
                Colour = new Vector3(1, 1, 1),
                CastShadows = false,
                ReceiveShadows = false,
            });
            materials.Add(new RenderMaterial
            {
                Key = key,
                Shading = MaterialShading.Unlit,
                Blend = MaterialBlend.Fade,
                BaseColour = new Vector4(1, 1, 1, alpha),

                // The rectangle's rows run top down and the cube's v runs bottom up.
                Tiling = new Vector2(rect.Width / size.Width, -rect.Height / size.Height),
                Offset = new Vector2(rect.Left / size.Width, rect.Bottom / size.Height),
                Wrap = MaterialWrap.Clamp,
                BaseColourMap = new Texture(name),
            });
        }

        return new NameplateBatch(objects, materials);
    }

    // The labels in range whose text is in the picture, nearest first, up to the cap. A stable sort, so
    // labels at the same distance keep the order they were given in.
    private IEnumerable<(double Distance, Nameplate Label, Rect Rect)> Nearest(RenderCamera camera, IEnumerable<Nameplate> labels) => labels
        .Select(label => (Distance: label.Position.DistanceTo(camera.Position), Label: label, Rect: Atlas.RectOf(label.Text)))
        .Where(near => near.Distance < Options.FadeEnd && near.Rect is not null)
        .OrderBy(near => near.Distance)
        .Take(Options.MaxLabels)
        .Select(near => (near.Distance, near.Label, near.Rect!.Value));

    // A turn that puts a quad's front square to the view. It is the camera's own turn and not a turn toward
    // the eye, so text stays upright and does not skew towards the edge of the screen.
    private static Quaternion SquareTo(RenderCamera camera)
    {
        var back = (camera.Position - camera.Target).Normalized();

        // shortcut: up is world Y because the camera has no roll. Looking straight up or down has no
        // right-hand side, so labels there face along x. Upgrade when the camera gains an up vector.
        var right = new Vector3(0, 1, 0).Cross(back);
        if (right.Length2 < 1e-12)
        {
            right.SetValues(1, 0, 0);
        }
        else
        {
            right.Normalize();
        }

        var up = back.Cross(right);
        return Quaternion.FromRotation(new Matrix3
        {
            [0] = right.X,
            [1] = right.Y,
            [2] = right.Z,
            [3] = up.X,
            [4] = up.Y,
            [5] = up.Z,
            [6] = back.X,
            [7] = back.Y,
            [8] = back.Z,
        });
    }
}

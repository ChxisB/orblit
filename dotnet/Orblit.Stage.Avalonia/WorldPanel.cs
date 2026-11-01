using Avalonia;
using Orblit.Filament;
using Orblit.VectorMath;

namespace Orblit.Stage.Avalonia;

/// <summary>What a <see cref="WorldPanel"/> is called and where its picture goes.</summary>
public sealed record WorldPanelOptions
{
    /// <summary>Names this panel's pictures, so two panels never share one.</summary>
    public required string Id { get; init; }

    public required long ObjectKey { get; init; }

    public required long MaterialKey { get; init; }

    /// <summary>Where a picture's bytes go. The renderer's own store unless a test supplies one.</summary>
    public Action<string, byte[]> Provide { get; init; } = static (name, bytes) => RenderResources.Provide(name, bytes);

    public Func<string, bool> Release { get; init; } = RenderResources.Release;
}

/// <summary>
/// An <see cref="IPanel"/> standing in the scene on a flat face, with a click and a drag that reach it.
/// </summary>
/// <remarks>
/// The face is the front of the built-in cube, squashed thin: a quad is a cube with a small z scale in
/// the transform, so a screen on a wall is the cube's +z side. The cube spans -1 to 1 on each axis, so a
/// transform of <c>scale(2, 1, 0.05)</c> is a face four metres wide and two high. Give the panel the same
/// shape in pixels or the picture is stretched.
/// <para>
/// Two halves. <see cref="Quad"/> and <see cref="Material"/> are what to put in the scene, and
/// <see cref="Sync"/> draws the panel when it changed and gives the renderer the new picture. Down,
/// Move and Up take a <see cref="PointerRay"/> and turn it into a pointer on the panel.
/// </para>
/// <para>
/// The renderer loads a name once, so each picture goes under a name of its own. Releasing the one before
/// frees its bytes and not the texture the renderer already made from it, which stays until the renderer
/// is disposed. That is a whole picture of memory per change. It is fine for a screen that changes when
/// somebody presses something and not for one that animates.
/// </para>
/// A panel takes one pointer. Pass the events of a single pointer to the pointer methods, and not those
/// of a second finger.
/// </remarks>
public sealed class WorldPanel : IDisposable
{
    private const int PointerId = 1;

    // How far from level with the face a ray may run and still count as level, in face space.
    private const double Level = 1e-9;

    private readonly IPanel _panel;
    private readonly WorldPanelOptions _options;
    private Matrix4 _transform = Matrix4.Identity();
    private Matrix4 _inverse = Matrix4.Identity();
    private string? _name;
    private int _pictures;

    // Where the pointer last was on the face's plane, so a drag that ends with the ray off the plane still
    // finishes where it left.
    private Point? _last;

    public WorldPanel(IPanel panel, Matrix4 transform, WorldPanelOptions options)
    {
        _panel = panel;
        _options = options;
        Place(transform);
    }

    /// <summary>What to put in the scene's objects.</summary>
    public RenderObject Quad => new()
    {
        Key = _options.ObjectKey,
        Material = _options.MaterialKey,
        Transform = _transform.Clone(),
        Colour = new Vector3(1, 1, 1),
        CastShadows = false,
        ReceiveShadows = false,
    };

    /// <summary>What to put in the scene's materials. Invisible until the first <see cref="Sync"/>.</summary>
    public RenderMaterial Material => new()
    {
        Key = _options.MaterialKey,
        Shading = MaterialShading.Unlit,
        Blend = MaterialBlend.Fade,
        BaseColour = new Vector4(1, 1, 1, _name is null ? 0 : 1),

        // A picture's rows run top down and the cube's v runs bottom up.
        Tiling = new Vector2(1, -1),
        Offset = new Vector2(0, 1),
        Wrap = MaterialWrap.Clamp,
        BaseColourMap = _name is null ? null : new Texture(_name),
    };

    /// <summary>Where the face is. Takes effect for the next ray.</summary>
    public void Place(Matrix4 transform)
    {
        _transform = transform.Clone();
        _inverse = Matrix4.Inverted(transform);
    }

    /// <summary>
    /// Draws the panel if it changed and hands the renderer the new picture. Put the scene together again
    /// afterwards, because <see cref="Material"/> names it.
    /// </summary>
    public void Sync()
    {
        if (!_panel.IsDirty)
        {
            return;
        }

        var next = RenderResources.NameFor($"panel/{_options.Id}/{++_pictures}.png");
        _options.Provide(next, _panel.Paint());
        var old = _name;
        _name = next;
        if (old is not null)
        {
            _options.Release(old);
        }
    }

    /// <summary>Lets go of the picture's bytes. The panel stays the caller's.</summary>
    public void Dispose()
    {
        var name = _name;
        _name = null;
        if (name is not null)
        {
            _options.Release(name);
        }
    }

    /// <summary>Where <paramref name="ray"/> lands on the panel, in its pixels, or null if it misses the face. A ray from behind the face misses.</summary>
    public Point? PixelAt(PointerRay ray) => OnPlane(ray) is { } pixel && IsOnFace(pixel) ? pixel : null;

    public bool Hits(PointerRay ray) => PixelAt(ray) is not null;

    /// <summary>Presses on the panel where <paramref name="ray"/> lands. Does nothing if it misses.</summary>
    public void Down(PointerRay ray)
    {
        if (PixelAt(ray) is not { } pixel)
        {
            return;
        }

        _last = pixel;
        _panel.Send(new PanelPointer(PointerPhase.Down, PointerId, pixel));
    }

    /// <summary>Moves the press. A drag that leaves the face keeps going, as a drag on a screen does when the pointer strays off it.</summary>
    public void Move(PointerRay ray) => Follow(ray, PointerPhase.Move);

    /// <summary>Lets go.</summary>
    public void Up(PointerRay ray)
    {
        Follow(ray, PointerPhase.Up);
        _last = null;
    }

    /// <summary>Drops the press without a release, as the system does for a touch it takes back. A button under it is not tapped.</summary>
    public void Cancel()
    {
        if (_last is not { } pixel)
        {
            return;
        }

        _last = null;
        _panel.Send(new PanelPointer(PointerPhase.Cancel, PointerId, pixel));
    }

    private void Follow(PointerRay ray, PointerPhase phase)
    {
        if ((OnPlane(ray) ?? _last) is not { } pixel)
        {
            return;
        }

        _last = pixel;
        _panel.Send(new PanelPointer(phase, PointerId, pixel));
    }

    // Half open, so the far edges belong to whatever is next to the panel. Avalonia's Rect.Contains
    // includes them.
    private bool IsOnFace(Point pixel) =>
        pixel.X >= 0 && pixel.X < _panel.Size.Width && pixel.Y >= 0 && pixel.Y < _panel.Size.Height;

    // Where the ray crosses the face's plane in panel pixels, however far from the face. Worked in the
    // face's own space, where it is the plane z = 1 and the cube's scale and turn are already undone.
    private Point? OnPlane(PointerRay ray)
    {
        var from = _inverse.Transformed3(ray.Origin);
        var along = _inverse.Rotated3(ray.Direction);

        // A ray level with the face meets its plane only in rounding noise, and which side of zero that
        // noise lands on differs between platforms.
        if (along.Z >= -Level || from.Z < 1)
        {
            return null;
        }

        var reach = (1 - from.Z) / along.Z;
        var x = from.X + (along.X * reach);
        var y = from.Y + (along.Y * reach);
        return new Point((x + 1) / 2 * _panel.Size.Width, (1 - y) / 2 * _panel.Size.Height);
    }
}

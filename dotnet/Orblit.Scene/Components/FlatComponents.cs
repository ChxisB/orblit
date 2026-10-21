using Orblit.Light;

namespace Orblit.Scene;

/// <summary>
/// A flat picture, drawn from a texture or a region of an atlas.
/// </summary>
/// <remarks>
/// Placed by the entity's transform like everything else, so a sprite can be parented to a 3D object
/// and a 3D object to a sprite. <see cref="Depth"/> is the only thing that is 2D about it: flat layers
/// have no meaningful distance from the camera, so the order they draw in has to be stated rather
/// than measured.
/// </remarks>
// F2: twelve fields of one sprite, called by name.
public sealed class SpriteComponent(
    string? texture = null,
    string? atlas = null,
    string? region = null,
    string? animation = null,
    double width = 1,
    double height = 1,
    double pivotX = 0.5,
    double pivotY = 0.5,
    double depth = 0,
    Tint? colour = null,
    double opacity = 1,
    bool additive = false) : SceneComponent
{
    /// <summary>A picture of its own, as a project path.</summary>
    public string? Texture { get; } = texture;

    /// <summary>
    /// Or an atlas and the region in it, which is what a sprite sheet is. Both are kept rather than
    /// one being derived: an atlas can be rebuilt and the region names survive it, whereas
    /// coordinates would not.
    /// </summary>
    public string? Atlas { get; } = atlas;

    public string? Region { get; } = region;

    /// <summary>A named flipbook in <see cref="Atlas"/> to play instead of holding one region.</summary>
    public string? Animation { get; } = animation;

    /// <summary>
    /// How big it is drawn, in the scene's own units rather than in pixels. Pixels would make a
    /// sprite change size when somebody changes the resolution, which is the one thing a flat picture
    /// must not do.
    /// </summary>
    public double Width { get; } = width;

    public double Height { get; } = height;

    /// <summary>
    /// Where on the picture the entity's position actually is, from 0 to 1. The middle by default,
    /// and the bottom middle is what a standing character wants.
    /// </summary>
    public double PivotX { get; } = pivotX;

    public double PivotY { get; } = pivotY;

    /// <summary>What draws in front of what. Higher is nearer the camera.</summary>
    public double Depth { get; } = depth;

    public Tint Colour { get; } = colour ?? Tint.Hex(0xFFFFFF);

    public double Opacity { get; } = opacity;

    /// <summary>Added to what is behind rather than covering it, for fire and glow.</summary>
    public bool Additive { get; } = additive;

    public override string Type => SceneComponents.Sprite;

    public static SpriteComponent FromJson(JsonMap json) => new(
        Values.Text(json, "texture"),
        Values.Text(json, "atlas"),
        Values.Text(json, "region"),
        Values.Text(json, "animation"),
        Values.Number(json, "width", 1),
        Values.Number(json, "height", 1),
        Values.Number(json, "pivotX", 0.5),
        Values.Number(json, "pivotY", 0.5),
        Values.Number(json, "depth", 0),
        Values.ReadTint(Values.At(json, "colour"), Tint.Hex(0xFFFFFF)),
        Values.Number(json, "opacity", 1),
        Values.Flag(json, "additive", false));

    public override JsonMap ToJson() => Values.Pruned(new OrderedDictionary<string, object?>
    {
        ["texture"] = Texture,
        ["atlas"] = Atlas,
        ["region"] = Region,
        ["animation"] = Animation,
        ["width"] = Width,
        ["height"] = Height,
        ["pivotX"] = PivotX,
        ["pivotY"] = PivotY,
        ["depth"] = Depth,
        ["colour"] = Values.TintToJson(Colour),
        ["opacity"] = Opacity,
        ["additive"] = Additive ? true : null,
    });
}

/// <summary>
/// A grid of tiles, as a map file somebody authored elsewhere.
/// </summary>
/// <remarks>
/// The map itself is not inlined. A Tiled map is tens of thousands of indices and it is edited in
/// Tiled: copying it into the scene file would make every scene enormous, make merges impossible,
/// and mean the copy goes stale the first time anybody opens the real one.
/// </remarks>
public sealed class TilemapComponent(string? asset = null, double depth = 0, double opacity = 1) : SceneComponent
{
    /// <summary>The map file, relative to the project.</summary>
    public string? Asset { get; } = asset;

    public double Depth { get; } = depth;

    public double Opacity { get; } = opacity;

    public override string Type => SceneComponents.Tilemap;

    public static TilemapComponent FromJson(JsonMap json) => new(
        Values.Text(json, "asset"),
        Values.Number(json, "depth", 0),
        Values.Number(json, "opacity", 1));

    public override JsonMap ToJson() => Values.Pruned(new OrderedDictionary<string, object?>
    {
        ["asset"] = Asset,
        ["depth"] = Depth,
        ["opacity"] = Opacity,
    });
}

/// <summary>One layer of a parallax backdrop.</summary>
public sealed class ParallaxLayer(string image, double depth = 1, double size = 1, double offset = 0, double drift = 0)
{
    public string Image { get; } = image;

    /// <summary>
    /// How far away it reads as. One is at the camera's own distance and moves with it exactly;
    /// larger is further off and moves less.
    /// </summary>
    public double Depth { get; } = depth;

    public double Size { get; } = size;

    public double Offset { get; } = offset;

    /// <summary>How fast it slides on its own, for cloud that moves when nothing else does.</summary>
    public double Drift { get; } = drift;

    public static ParallaxLayer FromJson(JsonMap json) => new(
        Values.Text(json, "image") ?? "",
        Values.Number(json, "depth", 1),
        Values.Number(json, "size", 1),
        Values.Number(json, "offset", 0),
        Values.Number(json, "drift", 0));

    public OrderedDictionary<string, object?> ToJson() => new()
    {
        ["image"] = Image,
        ["depth"] = Depth,
        ["size"] = Size,
        ["offset"] = Offset,
        ["drift"] = Drift,
    };
}

/// <summary>
/// A backdrop of layers that move at different rates.
/// </summary>
/// <remarks>
/// Stated inline rather than as a file, unlike a tilemap, because a parallax is five or six lines
/// and belongs to the scene it is behind. There is no second tool that authors one.
/// </remarks>
public sealed class ParallaxComponent(IReadOnlyList<ParallaxLayer>? layers = null) : SceneComponent
{
    /// <summary>Furthest first, which is the order they draw in.</summary>
    public IReadOnlyList<ParallaxLayer> Layers { get; } = layers ?? [];

    public override string Type => SceneComponents.Parallax;

    public static ParallaxComponent FromJson(JsonMap json) => new(
        Values.At(json, "layers") is JsonList raw
            ? [.. raw.OfType<JsonMap>().Select(ParallaxLayer.FromJson)]
            : []);

    public override JsonMap ToJson() => new OrderedDictionary<string, object?>
    {
        ["layers"] = Layers.Select(static layer => (object?)layer.ToJson()).ToList(),
    };
}

/// <summary>An interface drawn over the scene.</summary>
public sealed class CanvasComponent(string? asset = null, double scale = 1) : SceneComponent
{
    /// <summary>The interface file, relative to the project.</summary>
    public string? Asset { get; } = asset;

    public double Scale { get; } = scale;

    public override string Type => SceneComponents.Canvas;

    public static CanvasComponent FromJson(JsonMap json) =>
        new(Values.Text(json, "asset"), Values.Number(json, "scale", 1));

    public override JsonMap ToJson() => Values.Pruned(new OrderedDictionary<string, object?>
    {
        ["asset"] = Asset,
        ["scale"] = Scale,
    });
}

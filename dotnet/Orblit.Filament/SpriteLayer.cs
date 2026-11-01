using Orblit.VectorMath;

namespace Orblit.Filament;

/// <summary>How a sprite layer's pixels combine with what is behind them.</summary>
public enum SpriteBlend
{
    /// <summary>
    /// Over what is behind, by the picture's own alpha. Premultiplied, so a soft edge fades to
    /// what is behind it and not to a dark fringe.
    /// </summary>
    Alpha,

    /// <summary>Added to what is behind, so it only ever brightens: sparks, glows, a muzzle flash, a laser.</summary>
    Add,
}

/// <summary>
/// A layer of sprites: one image, and any number of rectangles cut from it. A layer is one draw
/// however many sprites it holds, and draws them in the order <see cref="Sprites"/> gives them.
/// Layers draw by <see cref="Order"/>, lowest first. A 2D scene wants an orthographic camera and,
/// because a sprite's colours are the artist's, linear tone mapping, so the picture is not graded
/// as though it were photographed.
/// </summary>
/// <remarks>
/// The sprites cross to the renderer only when <see cref="Revision"/> is not the one it last took,
/// the bargain a population makes. Everything else about a layer, its <see cref="Transform"/> and
/// its <see cref="Tint"/>, is sent every frame and costs nothing to change, which is how a
/// backdrop scrolls or a whole layer fades.
/// </remarks>
public sealed class SpriteLayer
{
    /// <summary>
    /// Floats a layer's settings take: the transform, then the tint. Must match
    /// kSpriteLayerParams.
    /// </summary>
    public const int Stride = 20;

    private readonly bool? _snap;

    /// <param name="key">What this layer is, across frames.</param>
    /// <param name="sprites">The sprites, <see cref="LayerSprite.Stride"/> floats each. See <see cref="Pack"/>.</param>
    public SpriteLayer(int key, float[] sprites)
    {
        if (sprites.Length % LayerSprite.Stride != 0)
        {
            throw new ArgumentException($"Sprites are whole records of {LayerSprite.Stride} floats.", nameof(sprites));
        }

        Key = key;
        Sprites = sprites;
    }

    public int Key { get; }

    public float[] Sprites { get; }

    /// <summary>
    /// The picture the sprites are cut from. Null draws each as a rectangle of its own colour. A
    /// resource name works as a path does.
    /// </summary>
    public Texture? Image { get; init; }

    /// <summary>Where the layer is: every sprite in it, moved at once.</summary>
    public Matrix4 Transform { get; init; } = Matrix4.Identity();

    /// <summary>A colour over every sprite in the layer, in linear light.</summary>
    public Vector4 Tint { get; init; } = new(1, 1, 1, 1);

    /// <summary>Which layers draw first: lowest first, and signed, so a backdrop at -10 is behind a default layer at nought.</summary>
    public int Order { get; init; }

    /// <summary>
    /// Sharp for pixel art, which is the default because a 2D layer is more often pixel art than
    /// not; smooth for anything painted.
    /// </summary>
    public MaterialFilter Filter { get; init; } = MaterialFilter.Sharp;

    /// <summary>
    /// Whether corners land on whole pixels. On by default for sharp layers, where a sprite moving
    /// by a fraction of a pixel would otherwise shimmer.
    /// </summary>
    public bool Snap
    {
        get => _snap ?? Filter == MaterialFilter.Sharp;
        init => _snap = value;
    }

    public SpriteBlend Blend { get; init; } = SpriteBlend.Alpha;

    /// <summary>Bumped by whoever changes <see cref="Sprites"/>.</summary>
    public int Revision { get; init; }

    public int Count => Sprites.Length / LayerSprite.Stride;

    /// <summary>The bits the renderer reads, in the order OrblitSprites.h names them.</summary>
    public int Flags =>
        (Filter == MaterialFilter.Sharp ? 1 : 0)
        | (Snap ? 2 : 0)
        | (Image is { Srgb: false } ? 4 : 0)
        | (Blend == SpriteBlend.Add ? 8 : 0);

    /// <summary>Writes this layer's <see cref="Stride"/> floats at <paramref name="at"/>.</summary>
    internal void PackParams(float[] into, int at)
    {
        for (var i = 0; i < 16; i++)
        {
            into[at + i] = (float)Transform[i];
        }

        into[at + 16] = (float)Tint.X;
        into[at + 17] = (float)Tint.Y;
        into[at + 18] = (float)Tint.Z;
        into[at + 19] = (float)Tint.W;
    }

    /// <summary><paramref name="list"/> as the floats <see cref="Sprites"/> takes.</summary>
    public static float[] Pack(IReadOnlyList<LayerSprite> list)
    {
        var packed = new float[list.Count * LayerSprite.Stride];
        for (var i = 0; i < list.Count; i++)
        {
            list[i].WriteInto(packed, i * LayerSprite.Stride);
        }

        return packed;
    }
}

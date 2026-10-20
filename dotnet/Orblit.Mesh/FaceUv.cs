using Orblit.VectorMath;

namespace Orblit.Mesh;

/// <summary>How a face's texture coordinates fill the space they are given.</summary>
public enum UvFit
{
    /// <summary>
    /// A constant texture density however big the face is, so a wall twice as wide shows twice
    /// as much brick. Two walls made at different sizes match where they meet.
    /// </summary>
    Tile,

    /// <summary>The whole texture once across the face, squashed to the face's shape.</summary>
    Stretch,

    /// <summary>The whole texture once, keeping its proportions, with the spare room left over.</summary>
    Fit,
}

/// <summary>
/// How a face gets its texture coordinates. Automatic is a rule: project the face flat, then
/// move, turn and scale that. It survives the face being extruded, moved, resized or cut,
/// because it is worked out again from whatever the face is now. Manual is coordinates, one a
/// corner, and it survives nothing except being the exact thing somebody drew.
/// </summary>
public sealed class FaceUv
{
    private readonly Vector2? _offset;
    private readonly Vector2? _scale;

    // F2: eight optional settings, each called by name.
    public FaceUv(
        UvFit fit = UvFit.Tile,
        Vector2? offset = null,
        Vector2? scale = null,
        double rotation = 0,
        bool flipU = false,
        bool flipV = false,
        bool swap = false,
        IReadOnlyList<Vector2>? manual = null)
    {
        Fit = fit;
        _offset = offset;
        _scale = scale;
        Rotation = rotation;
        FlipU = flipU;
        FlipV = flipV;
        Swap = swap;
        Manual = manual;
    }

    /// <summary>
    /// Explicit coordinates, one a corner, in the face's own order. Non-null means the face is
    /// no longer automatic. The other settings are then ignored but kept, so going back to
    /// automatic returns to the settings somebody had.
    /// </summary>
    public IReadOnlyList<Vector2>? Manual { get; }

    public bool IsManual => Manual is not null;

    public UvFit Fit { get; }

    /// <summary>Where the texture starts. Animating it scrolls the surface.</summary>
    public Vector2 Offset => _offset ?? Vector2.Zero();

    /// <summary>How many times it repeats. Under <see cref="UvFit.Tile"/> two means twice as many bricks in the same wall.</summary>
    public Vector2 Scale => _scale ?? new Vector2(1, 1);

    /// <summary>How far the texture is turned on the face, in degrees.</summary>
    public double Rotation { get; }

    public bool FlipU { get; }

    public bool FlipV { get; }

    /// <summary>Whether the two axes are exchanged, which turns a texture on its side without turning it.</summary>
    public bool Swap { get; }

    // F2: nine optional settings, each called by name.
    public FaceUv CopyWith(
        UvFit? fit = null,
        Vector2? offset = null,
        Vector2? scale = null,
        double? rotation = null,
        bool? flipU = null,
        bool? flipV = null,
        bool? swap = null,
        IReadOnlyList<Vector2>? manual = null,
        bool clearManual = false) => new(
            fit ?? Fit,
            offset ?? Offset,
            scale ?? Scale,
            rotation ?? Rotation,
            flipU ?? FlipU,
            flipV ?? FlipV,
            swap ?? Swap,
            clearManual ? null : manual ?? Manual);

    /// <summary>
    /// The coordinates for a face's corners. <paramref name="points"/> are the corners in the
    /// world and <paramref name="normal"/> is the direction the face points. Manual coordinates
    /// are handed straight back when there are the right number of them. A face that has been cut
    /// or extruded since they were drawn has the wrong number, and falling back to the rule beats
    /// handing out coordinates that belong to corners which are no longer there.
    /// </summary>
    public List<Vector2> ForFace(IReadOnlyList<Vector3> points, Vector3 normal)
    {
        if (Manual is { } drawn && drawn.Count == points.Count)
        {
            return [.. drawn.Select(at => at.Clone())];
        }

        var axes = AxesFor(normal);
        var flat = points.Select(at => new Vector2(at.Dot(axes.U), at.Dot(axes.V))).ToList();
        if (Fit != UvFit.Tile)
        {
            Normalise(flat, keepShape: Fit == UvFit.Fit);
        }

        return [.. flat.Select(Place)];
    }

    /// <summary>Moves and scales coordinates so the face's own bounds fill nought to one.</summary>
    private static void Normalise(List<Vector2> flat, bool keepShape)
    {
        if (flat.Count == 0)
        {
            return;
        }

        var minU = flat[0].X;
        var maxU = flat[0].X;
        var minV = flat[0].Y;
        var maxV = flat[0].Y;
        foreach (var at in flat)
        {
            minU = Math.Min(minU, at.X);
            maxU = Math.Max(maxU, at.X);
            minV = Math.Min(minV, at.Y);
            maxV = Math.Max(maxV, at.Y);
        }

        // A face with no width in one direction is edge-on or degenerate. Dividing by that is an
        // infinity that spreads through every vertex.
        var spanU = maxU - minU;
        var spanV = maxV - minV;
        if (spanU < 1e-9)
        {
            spanU = 1;
        }

        if (spanV < 1e-9)
        {
            spanV = 1;
        }

        if (keepShape)
        {
            var most = Math.Max(spanU, spanV);
            spanU = most;
            spanV = most;
        }

        foreach (var at in flat)
        {
            at.X = (at.X - minU) / spanU;
            at.Y = (at.Y - minV) / spanV;
        }
    }

    /// <summary>
    /// Applies the swap, the turn, the scale, the flips and the offset, in that order. Turning
    /// then scaling means the scale is along the texture's axes rather than the face's.
    /// </summary>
    private Vector2 Place(Vector2 at)
    {
        var u = at.X;
        var v = at.Y;

        if (Swap)
        {
            (u, v) = (v, u);
        }

        if (Rotation != 0)
        {
            var radians = Rotation * Math.PI / 180;
            var cos = Math.Cos(radians);
            var sin = Math.Sin(radians);
            var turnedU = (u * cos) - (v * sin);
            v = (u * sin) + (v * cos);
            u = turnedU;
        }

        u *= Scale.X;
        v *= Scale.Y;
        if (FlipU)
        {
            u = -u;
        }

        if (FlipV)
        {
            v = -v;
        }

        return new Vector2(u + Offset.X, v + Offset.Y);
    }

    /// <summary>
    /// Two axes across a face, for a flat projection. Whichever world axis the face is least
    /// aligned with decides them, so the pair is never parallel to the normal, and two faces
    /// pointing the same way always get the same pair.
    /// </summary>
    public static (Vector3 U, Vector3 V) AxesFor(Vector3 normal)
    {
        var away = Math.Abs(normal.X) < 0.9 ? new Vector3(1, 0, 0) : new Vector3(0, 1, 0);
        var u = normal.Cross(away).Normalized();
        return (u, normal.Cross(u).Normalized());
    }

    public OrderedDictionary<string, object?> ToJson()
    {
        var json = new OrderedDictionary<string, object?>();
        JsonValues.Put(json, Fit != UvFit.Tile, "fit", EnumNames.Of(Fit));
        JsonValues.Put(json, Offset.X != 0 || Offset.Y != 0, "offset", new List<object?> { Offset.X, Offset.Y });
        JsonValues.Put(json, Scale.X != 1 || Scale.Y != 1, "scale", new List<object?> { Scale.X, Scale.Y });
        JsonValues.Put(json, Rotation != 0, "turn", Rotation);
        JsonValues.Put(json, FlipU, "flipU", true);
        JsonValues.Put(json, FlipV, "flipV", true);
        JsonValues.Put(json, Swap, "swap", true);
        JsonValues.Put(json, Manual is not null, "uvs", Manual?.SelectMany(at => new object?[] { at.X, at.Y }).ToList());
        return json;
    }

    public static FaceUv? FromJson(object? value)
    {
        if (JsonValues.Map(value) is not { } map)
        {
            return null;
        }

        map.TryGetValue("fit", out var fit);
        map.TryGetValue("turn", out var turn);
        map.TryGetValue("flipU", out var flipU);
        map.TryGetValue("flipV", out var flipV);
        map.TryGetValue("swap", out var swap);
        return new FaceUv(
            EnumNames.Parse(fit, UvFit.Tile),
            PairOf(map, "offset"),
            PairOf(map, "scale"),
            JsonValues.Num(turn) ?? 0,
            JsonValues.IsTrue(flipU),
            JsonValues.IsTrue(flipV),
            JsonValues.IsTrue(swap),
            DrawnFrom(map));
    }

    private static Vector2? PairOf(IReadOnlyDictionary<string, object?> map, string key)
    {
        if (!map.TryGetValue(key, out var raw) || JsonValues.List(raw) is not { Count: >= 2 } list)
        {
            return null;
        }

        return JsonValues.Num(list[0]) is { } x && JsonValues.Num(list[1]) is { } y ? new Vector2(x, y) : null;
    }

    /// <summary>The drawn coordinates, or null when the file has none. A pair that is not two numbers is skipped.</summary>
    private static List<Vector2>? DrawnFrom(IReadOnlyDictionary<string, object?> map)
    {
        if (!map.TryGetValue("uvs", out var raw) || JsonValues.List(raw) is not { Count: var count } flat || count % 2 != 0)
        {
            return null;
        }

        var drawn = new List<Vector2>();
        for (var i = 0; i + 1 < count; i += 2)
        {
            if (JsonValues.Num(flat[i]) is { } x && JsonValues.Num(flat[i + 1]) is { } y)
            {
                drawn.Add(new Vector2(x, y));
            }
        }

        return drawn;
    }
}

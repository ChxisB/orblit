using System.Text;
using Orblit.Json;
using Orblit.VectorMath;

namespace Orblit.Terrain;

/// <summary>
/// Ground: a grid of heights, the sets that cover it, and the regions it is stored in.
/// Texel (<c>i</c>, <c>j</c>) of the whole terrain sits at (<c>i × spacing</c>, <c>j × spacing</c>)
/// in the world, and is texel (<c>i − x × regionSize</c>, <c>j − z × regionSize</c>) of region
/// (<c>x</c>, <c>z</c>). Regions exist only where the ground does; asking about anywhere else
/// answers null.
/// The settings are one small file and each region is another beside it, named by
/// <see cref="RegionKey.FileName"/>, so an edit to one corner of a large world rewrites one region
/// and not the world. Reading and writing the files is left to the caller: this package knows
/// their bytes and not where they live, so it runs where there is no file system.
/// </summary>
public sealed class Terrain
{
    /// <summary>What a terrain's settings file name ends in.</summary>
    public const string Extension = ".oterrain";

    public const string Marker = "orblit.terrain";
    public const int FormatVersion = 1;

    private readonly OrderedDictionary<RegionKey, TerrainRegion> _regions = [];

    public Terrain(
        int regionSize = 256,
        double spacing = 1,
        IEnumerable<TerrainSet>? sets = null,
        AutoCover? autoCover = null,
        double blendSharpness = 0.87,
        IEnumerable<ScatterLayer>? scatter = null)
    {
        RegionSize = regionSize;
        Spacing = spacing;
        Sets = [.. sets ?? []];
        AutoCover = autoCover ?? new AutoCover();
        BlendSharpness = blendSharpness;
        Scatter = [.. scatter ?? []];
        if (!TerrainRegion.ValidSize(regionSize))
        {
            throw new ArgumentOutOfRangeException(
                nameof(regionSize),
                regionSize,
                $"A region is a power of two texels across, from {TerrainRegion.MinSize} to {TerrainRegion.MaxSize}.");
        }

        if (!(spacing > 0) || !double.IsFinite(spacing))
        {
            throw new ArgumentOutOfRangeException(nameof(spacing), spacing, "Must be above zero.");
        }

        if (Sets.Count > Cover.SetCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sets),
                Sets.Count,
                $"A terrain has at most {Cover.SetCount} sets.");
        }
    }

    /// <summary>
    /// The steps that bring an older terrain file up to <see cref="FormatVersion"/>, oldest first.
    /// Empty: this is the first format.
    /// </summary>
    public static IReadOnlyList<TerrainMigration> Migrations { get; } = [];

    /// <summary>Texels a region is across.</summary>
    public int RegionSize { get; }

    /// <summary>Metres between texels.</summary>
    public double Spacing { get; }

    /// <summary>
    /// The kinds of ground a <see cref="Cover"/> can name, by index. At most
    /// <see cref="Cover.SetCount"/>. The terrain's own copy of the list it was made with, so it can
    /// be changed whatever that list was.
    /// </summary>
    public List<TerrainSet> Sets { get; }

    /// <summary>How ground marked <see cref="Cover.Automatic"/> chooses its sets.</summary>
    public AutoCover AutoCover { get; set; }

    /// <summary>
    /// What is scattered over the ground: grass, stones, trees, a layer each. The terrain's own
    /// copy of the list it was made with. Where each one stands is a <c>ScatterPlacer</c>'s to work
    /// out, from these and the ground.
    /// </summary>
    public List<ScatterLayer> Scatter { get; }

    /// <summary>
    /// How sharply one set gives way to another where they blend: 0 is a smooth fade, 1 a hard
    /// edge along the taller of the two.
    /// </summary>
    public double BlendSharpness { get; set; }

    public IEnumerable<TerrainRegion> Regions => _regions.Values;

    public TerrainRegion? RegionAt(RegionKey key) => _regions.GetValueOrDefault(key);

    /// <summary>The region at <paramref name="key"/>, made flat and empty if there was none.</summary>
    public TerrainRegion AddRegion(RegionKey key)
    {
        if (!_regions.TryGetValue(key, out var region))
        {
            region = new TerrainRegion(key, RegionSize);
            _regions[key] = region;
        }

        return region;
    }

    /// <summary>
    /// Puts <paramref name="region"/> in, replacing any at its key. Throws if it is not
    /// <see cref="RegionSize"/> across.
    /// </summary>
    public void PutRegion(TerrainRegion region)
    {
        if (region.Size != RegionSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(region),
                region.Size,
                $"This terrain's regions are {RegionSize} across.");
        }

        _regions[region.Key] = region;
    }

    public TerrainRegion? RemoveRegion(RegionKey key) => _regions.Remove(key, out var region) ? region : null;

    /// <summary>The region the world point (<c>x</c>, <c>z</c>) lies in, whether or not it exists.</summary>
    public RegionKey KeyAt(double x, double z) =>
        RegionKey.Containing(Doubles.Floor(x / Spacing), Doubles.Floor(z / Spacing), RegionSize);

    /// <summary>
    /// Sets every height of the region at <paramref name="key"/> from <paramref name="height"/>,
    /// asked at each texel's world position. Makes the region if it was not there.
    /// </summary>
    public TerrainRegion FillHeights(RegionKey key, Func<double, double, double> height)
    {
        var region = AddRegion(key);
        var i0 = key.X * RegionSize;
        var j0 = key.Z * RegionSize;
        for (var j = 0; j < RegionSize; j++)
        {
            for (var i = 0; i < RegionSize; i++)
            {
                region.Heights[(j * RegionSize) + i] = (float)height((i0 + i) * Spacing, (j0 + j) * Spacing);
            }
        }

        region.Touch();
        return region;
    }

    /// <summary>
    /// The height of texel (<paramref name="i"/>, <paramref name="j"/>) of the whole terrain, holes
    /// included, or null where there is no region.
    /// </summary>
    public double? TexelHeight(long i, long j)
    {
        var region = RegionAt(RegionKey.Containing(i, j, RegionSize));
        return region?.Heights[IndexIn(region, i, j)];
    }

    /// <summary>The cover of texel (<paramref name="i"/>, <paramref name="j"/>), or null where there is no region.</summary>
    public Cover? TexelCover(long i, long j)
    {
        var region = RegionAt(RegionKey.Containing(i, j, RegionSize));
        return region is null ? null : new Cover(region.Cover[IndexIn(region, i, j)]);
    }

    /// <summary>The colour of texel (<paramref name="i"/>, <paramref name="j"/>), or null where there is no region.</summary>
    public GroundColour? TexelColour(long i, long j)
    {
        var region = RegionAt(RegionKey.Containing(i, j, RegionSize));
        if (region is null)
        {
            return null;
        }

        var at = IndexIn(region, i, j) * 4;
        var bytes = region.Colour;
        return GroundColour.Bytes(bytes[at], bytes[at + 1], bytes[at + 2], bytes[at + 3]);
    }

    /// <summary>
    /// The height of the ground at world (<paramref name="x"/>, <paramref name="z"/>), exactly as
    /// it is drawn nearby, or null where nothing is drawn.
    /// Each square of four texels is two triangles split along the diagonal from its low corner to
    /// its high one, the same two the mesh draws, so a foot put here meets the surface the eye sees
    /// rather than a smoothed guess at it. A triangle with a hole or a missing region at any corner
    /// is not drawn, and has no height.
    /// </summary>
    public double? HeightAt(double x, double z)
    {
        if (!double.IsFinite(x) || !double.IsFinite(z))
        {
            return null;
        }

        var u = x / Spacing;
        var v = z / Spacing;
        var i = Doubles.Floor(u);
        var j = Doubles.Floor(v);
        var fu = u - i;
        var fv = v - j;
        if (SolidHeight(i, j) is not { } h00 || SolidHeight(i + 1, j + 1) is not { } h11)
        {
            return null;
        }

        if (fu >= fv)
        {
            return SolidHeight(i + 1, j) is { } h10 ? h00 + (fu * (h10 - h00)) + (fv * (h11 - h10)) : null;
        }

        return SolidHeight(i, j + 1) is { } h01 ? h00 + (fv * (h01 - h00)) + (fu * (h11 - h01)) : null;
    }

    /// <summary>
    /// Which way the ground faces at world (<paramref name="x"/>, <paramref name="z"/>), the way it
    /// is lit: a slope worked out at each of the four nearest texels and blended between them, so
    /// it turns smoothly across a triangle instead of jumping at its edges. Null only where none of
    /// the four texels exists.
    /// Holes make no difference here. A texel with no neighbour on one side takes its own height
    /// for the missing one.
    /// </summary>
    public Vector3? NormalAt(double x, double z)
    {
        if (!double.IsFinite(x) || !double.IsFinite(z))
        {
            return null;
        }

        var u = x / Spacing;
        var v = z / Spacing;
        var i = Doubles.Floor(u);
        var j = Doubles.Floor(v);
        var fu = u - i;
        var fv = v - j;
        var gx = 0.0;
        var gz = 0.0;
        var total = 0.0;

        void Corner(long ci, long cj, double weight)
        {
            if (weight == 0 || TexelHeight(ci, cj) is not { } centre)
            {
                return;
            }

            gx += weight * ((TexelHeight(ci + 1, cj) ?? centre) - (TexelHeight(ci - 1, cj) ?? centre));
            gz += weight * ((TexelHeight(ci, cj + 1) ?? centre) - (TexelHeight(ci, cj - 1) ?? centre));
            total += weight;
        }

        Corner(i, j, (1 - fu) * (1 - fv));
        Corner(i + 1, j, fu * (1 - fv));
        Corner(i, j + 1, (1 - fu) * fv);
        Corner(i + 1, j + 1, fu * fv);
        if (total == 0)
        {
            return null;
        }

        var scale = 1 / (total * 2 * Spacing);
        var normal = new Vector3(-gx * scale, 1, -gz * scale);
        normal.Normalize();
        return normal;
    }

    /// <summary>
    /// The cover of the texel nearest world (<paramref name="x"/>, <paramref name="z"/>), as it is
    /// stored: <see cref="Cover.Automatic"/> ground says so rather than naming its sets. Null where
    /// there is no region.
    /// </summary>
    public Cover? CoverAt(double x, double z)
    {
        if (!double.IsFinite(x) || !double.IsFinite(z))
        {
            return null;
        }

        return TexelCover(Doubles.Round(x / Spacing), Doubles.Round(z / Spacing));
    }

    /// <summary>
    /// Where a ray from <paramref name="origin"/> along <paramref name="direction"/> first meets
    /// the ground, or null if it meets none within <paramref name="maxDistance"/>: through a hole,
    /// off the edge, into the sky.
    /// Meets the surface <see cref="HeightAt"/> describes, which is the one drawn, so a brush lands
    /// where the pointer is. Stepped half a texel at a time through the box the regions fill and
    /// then narrowed by halves, so a ridge thinner than half a texel can be missed at a glancing
    /// angle.
    /// </summary>
    public Vector3? Raycast(Vector3 origin, Vector3 direction, double maxDistance = double.PositiveInfinity)
    {
        if (_regions.Count == 0 || direction.Length2 == 0)
        {
            return null;
        }

        var along = direction.Normalized();
        var (low, high) = Bounds();
        var (near, far) = Slabs(origin, along, low, high);
        var start = Math.Max(near, 0.0);
        var end = Math.Min(far, maxDistance);
        if (start > end)
        {
            return null;
        }

        var step = Spacing * 0.5;
        double? above = null;
        for (var t = start; ; t = Math.Min(t + step, end))
        {
            var gap = GapAt(origin, along, t);
            if (gap <= 0)
            {
                return Narrow(origin, along, above, t);
            }

            above = gap is null ? null : t;
            if (t >= end)
            {
                return null;
            }
        }
    }

    public OrderedDictionary<string, object?> ToJson()
    {
        var keys = _regions.Keys.OrderBy(key => key.Z).ThenBy(key => key.X);
        var json = new OrderedDictionary<string, object?>
        {
            ["kind"] = Marker,
            ["formatVersion"] = (long)FormatVersion,
            ["regionSize"] = (long)RegionSize,
            ["spacing"] = Spacing,
            ["blendSharpness"] = BlendSharpness,
            ["autoCover"] = AutoCover.ToJson(),
            ["sets"] = Sets.Select(set => (object?)set.ToJson()).ToList(),
        };
        if (Scatter.Count > 0)
        {
            json["scatter"] = Scatter.Select(layer => (object?)layer.ToJson()).ToList();
        }

        json["regions"] = keys.Select(key => (object?)new List<object?> { key.X, key.Z }).ToList();
        return json;
    }

    /// <summary>
    /// The settings file's text, with a key to a line and a set or a scatter layer to a line, so
    /// the diff of two versions is the settings that changed. The regions are listed and not
    /// included: each is its own file.
    /// </summary>
    public string Encode()
    {
        var lines = new List<string>();
        foreach (var (key, value) in ToJson())
        {
            var text = key is "sets" or "scatter" && value is List<object?> { Count: > 0 } items
                ? "[\n" + string.Join(",\n", items.Select(item => "    " + DartJson.Encode(item))) + "\n  ]"
                : DartJson.Encode(value);
            lines.Add($"  {DartJson.Encode(key)}: {text}");
        }

        return new StringBuilder("{\n").AppendJoin(",\n", lines).Append("\n}\n").ToString();
    }

    /// <summary>
    /// A terrain's settings out of a file's text, with the regions it lists and whatever could not
    /// be read. The regions come back as keys and not as regions: each is a file of its own for the
    /// caller to read and <see cref="PutRegion"/>.
    /// Lenient about the parts, strict about the whole: a set or a setting that cannot be read is
    /// dropped with a note, and a file that is not a terrain, is one from a newer Orblit, or does
    /// not say how big its regions are throws <see cref="TerrainFormatException"/>.
    /// </summary>
    public static TerrainLoad Decode(string text)
    {
        var (parsed, version) = ReadHeader(text);
        var problems = new List<string>();
        var json = parsed;
        foreach (var step in Migrations)
        {
            if (version is { } from && step.From >= from)
            {
                json = step.Apply(json, problems);
            }
        }

        if (JsonReads.Integer(json, "regionSize") is not { } regionSize || !TerrainRegion.ValidSize(regionSize))
        {
            throw new TerrainFormatException("This terrain does not say how big its regions are.");
        }

        var spacing = JsonReads.Number(json, "spacing") ?? 1;
        if (spacing <= 0)
        {
            problems.Add($"The texels were {Doubles.ToDartString(spacing)} m apart; they are 1 m now.");
            spacing = 1;
        }

        var sets = ReadSets(json, problems);
        var scatter = ReadScatter(json, problems);
        var keys = ReadKeys(json, problems);
        var terrain = new Terrain(
            (int)regionSize,
            spacing,
            sets,
            AutoCover.FromJson(json.GetValueOrDefault("autoCover")),
            Doubles.Clamp(JsonReads.Number(json, "blendSharpness") ?? 0.87, 0.0, 1.0),
            scatter);
        return new TerrainLoad(terrain, keys, problems);
    }

    private static (OrderedDictionary<string, object?> Json, long? Version) ReadHeader(string text)
    {
        object? parsed;
        try
        {
            parsed = DartJson.Decode(text);
        }
        catch (FormatException error)
        {
            throw new TerrainFormatException($"This is not a terrain file: {error.Message}");
        }

        if (parsed is not OrderedDictionary<string, object?> json || json.GetValueOrDefault("kind") as string != Marker)
        {
            throw new TerrainFormatException("This is not a terrain file.");
        }

        var version = json.GetValueOrDefault("formatVersion") as long?;
        if (version > FormatVersion)
        {
            throw new TerrainFormatException("This terrain was written by a newer Orblit.");
        }

        return (json, version);
    }

    private static List<TerrainSet> ReadSets(IReadOnlyDictionary<string, object?> json, List<string> problems)
    {
        var sets = new List<TerrainSet>();
        foreach (var raw in json.GetValueOrDefault("sets") as IReadOnlyList<object?> ?? [])
        {
            if (TerrainSet.FromJson(raw, problems) is not { } set)
            {
                continue;
            }

            if (sets.Count == Cover.SetCount)
            {
                problems.Add($"Sets past the {Cover.SetCount}th were left out, starting with \"{set.Name}\".");
                break;
            }

            sets.Add(set);
        }

        return sets;
    }

    private static List<ScatterLayer> ReadScatter(IReadOnlyDictionary<string, object?> json, List<string> problems)
    {
        var scatter = new List<ScatterLayer>();
        foreach (var raw in json.GetValueOrDefault("scatter") as IReadOnlyList<object?> ?? [])
        {
            if (ScatterLayer.FromJson(raw, problems) is { } layer)
            {
                scatter.Add(layer);
            }
        }

        return scatter;
    }

    private static List<RegionKey> ReadKeys(IReadOnlyDictionary<string, object?> json, List<string> problems)
    {
        var keys = new OrderedDictionary<RegionKey, bool>();
        foreach (var raw in json.GetValueOrDefault("regions") as IReadOnlyList<object?> ?? [])
        {
            if (raw is IReadOnlyList<object?> { Count: 2 } pair && pair[0] is long x && pair[1] is long z)
            {
                if (!keys.TryAdd(new RegionKey(x, z), true))
                {
                    problems.Add($"Region {x}, {z} was listed twice.");
                }
            }
            else
            {
                problems.Add("A region that does not say where it is was left out.");
            }
        }

        return [.. keys.Keys];
    }

    private int IndexIn(TerrainRegion region, long i, long j) =>
        (int)(((j - (region.Key.Z * RegionSize)) * RegionSize) + (i - (region.Key.X * RegionSize)));

    /// <summary>
    /// The height of the texel, or null where there is no ground to stand on: no region, or a hole.
    /// </summary>
    private double? SolidHeight(long i, long j)
    {
        var region = RegionAt(RegionKey.Containing(i, j, RegionSize));
        if (region is null)
        {
            return null;
        }

        var at = IndexIn(region, i, j);
        return new Cover(region.Cover[at]).Hole ? null : region.Heights[at];
    }

    /// <summary>
    /// The box the regions fill, padded so flat ground is a box with some depth to step through
    /// rather than a plane the first step lands exactly on.
    /// </summary>
    private (Vector3 Low, Vector3 High) Bounds()
    {
        var low = new Vector3(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
        var high = new Vector3(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity);
        var across = RegionSize * Spacing;
        foreach (var region in _regions.Values)
        {
            low.X = Math.Min(low.X, region.Key.X * across);
            low.Z = Math.Min(low.Z, region.Key.Z * across);
            high.X = Math.Max(high.X, (region.Key.X + 1) * across);
            high.Z = Math.Max(high.Z, (region.Key.Z + 1) * across);
            low.Y = Math.Min(low.Y, region.MinHeight);
            high.Y = Math.Max(high.Y, region.MaxHeight);
        }

        low.Y -= Spacing;
        high.Y += Spacing;
        return (low, high);
    }

    private double? GapAt(Vector3 origin, Vector3 along, double t)
    {
        var point = origin + (along * t);
        return point.Y - HeightAt(point.X, point.Z);
    }

    private Vector3 Narrow(Vector3 origin, Vector3 along, double? above, double t)
    {
        var low = above ?? t;
        var high = t;
        for (var halving = 0; halving < 24 && above is not null; halving++)
        {
            var middle = (low + high) / 2;
            if (GapAt(origin, along, middle) <= 0)
            {
                high = middle;
            }
            else
            {
                low = middle;
            }
        }

        var hit = origin + (along * high);
        hit.Y = HeightAt(hit.X, hit.Z) ?? hit.Y;
        return hit;
    }

    /// <summary>How far along a ray it enters and leaves the box from <c>low</c> to <c>high</c>.</summary>
    private static (double, double) Slabs(Vector3 origin, Vector3 along, Vector3 low, Vector3 high)
    {
        var near = double.NegativeInfinity;
        var far = double.PositiveInfinity;
        for (var axis = 0; axis < 3; axis++)
        {
            var from = origin[axis];
            var pace = along[axis];
            if (Math.Abs(pace) < 1e-12)
            {
                if (from < low[axis] || from > high[axis])
                {
                    return (1, 0);
                }

                continue;
            }

            var enter = (low[axis] - from) / pace;
            var leave = (high[axis] - from) / pace;
            if (enter > leave)
            {
                (enter, leave) = (leave, enter);
            }

            near = Math.Max(near, enter);
            far = Math.Min(far, leave);
        }

        return (near, far);
    }
}

/// <summary>
/// What reading a terrain file gave: the terrain with no regions in it yet, the regions its file
/// lists, and what had to be left out.
/// </summary>
public sealed record TerrainLoad(Terrain Terrain, IReadOnlyList<RegionKey> Regions, IReadOnlyList<string> Problems);

/// <summary>A terrain file that cannot be read at all.</summary>
public sealed class TerrainFormatException(string message) : Exception(message);

/// <summary>One step between terrain formats: decoded JSON at <see cref="From"/> in, at <see cref="To"/> out.</summary>
public abstract class TerrainMigration
{
    public abstract int From { get; }

    public int To => From + 1;

    public abstract OrderedDictionary<string, object?> Apply(OrderedDictionary<string, object?> json, List<string> notes);
}

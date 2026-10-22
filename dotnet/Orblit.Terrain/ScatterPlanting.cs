using Orblit.VectorMath;
using static Orblit.Terrain.ScatterHash;

namespace Orblit.Terrain;

/// <summary>A spot a layer chose, once the ground there let something stand on it.</summary>
internal readonly record struct Site(uint Spot, double X, double Z, double Height, Vector3 Normal);

/// <summary>One layer being laid over one region.</summary>
internal sealed class Planting
{
    private readonly Ground _ground;
    private readonly ScatterLayer _layer;
    private readonly TerrainRegion _region;
    private readonly double _spacing;
    private readonly double _cell;
    private readonly uint _seed;
    private readonly bool _block;
    private readonly (double Red, double Green, double Blue) _base;
    private readonly Floats _transforms = new();
    private readonly Floats _colours = new();
    private readonly Vector3 _minimum = Vector3.All(double.PositiveInfinity);
    private readonly Vector3 _maximum = Vector3.All(double.NegativeInfinity);

    public Planting(Terrain terrain, ScatterLayer layer, TerrainRegion region)
    {
        _ground = new Ground(terrain, region);
        _layer = layer;
        _region = region;
        _spacing = terrain.Spacing;
        _cell = 1 / Math.Sqrt(layer.Density);
        _seed = Mix(Low(layer.Seed));
        _block = layer.Mesh is null;
        _base = Linear(layer.Colour);
    }

    /// <summary>
    /// The pattern is one square per unit of density over the whole world. Each square holds one
    /// spot, and the ground there decides whether anything stands on it.
    /// </summary>
    public ScatterGroup Place(int index, int id)
    {
        var across = _region.Size * _spacing;
        var x0 = _region.Key.X * across;
        var z0 = _region.Key.Z * across;
        var firstX = Doubles.Floor(x0 / _cell);
        var lastX = Doubles.Floor((x0 + across) / _cell);
        var firstZ = Doubles.Floor(z0 / _cell);
        var lastZ = Doubles.Floor((z0 + across) / _cell);
        for (var cz = firstZ; cz <= lastZ; cz++)
        {
            for (var cx = firstX; cx <= lastX; cx++)
            {
                Plant(cx, cz);
            }
        }

        if (_colours.Length == 0)
        {
            _minimum.SetValues(0, 0, 0);
            _maximum.SetValues(0, 0, 0);
        }

        return new ScatterGroup(
            key: _region.Key,
            layer: index,
            id: id,
            transforms: _transforms.Done(),
            colours: _colours.Done(),
            minimum: _minimum,
            maximum: _maximum);
    }

    private static bool Outside(double value, double low, double high) => value < low || value > high;

    // Which way is up for something leaning "lean" of the way from straight up to the ground's normal.
    private static Vector3 Up(Vector3 normal, double lean)
    {
        var x = normal.X * lean;
        var y = 1 + ((normal.Y - 1) * lean);
        var z = normal.Z * lean;
        var length = Math.Sqrt((x * x) + (y * y) + (z * z));
        return new Vector3(x / length, y / length, z / length);
    }

    private void Plant(long cx, long cz)
    {
        if (Choose(cx, cz) is { } site)
        {
            Grow(site);
        }
    }

    // Each skip below tests "outside" rather than "inside", so a NaN gets through.
    private Site? Choose(long cx, long cz)
    {
        var spot = Mix(Mix(_seed ^ Low(cx)) ^ Low(cz));
        var x = (cx + Draw(spot, 0)) * _cell;
        var z = (cz + Draw(spot, 1)) * _cell;
        if (!_ground.Holds(x, z))
        {
            return null;
        }

        if (_ground.HeightAt(x, z) is not { } height || Outside(height, _layer.MinHeight, _layer.MaxHeight))
        {
            return null;
        }

        var normal = _ground.NormalAt(x, z);
        var slope = Math.Acos(Doubles.Clamp(normal.Y, -1.0, 1.0)) * 180 / Math.PI;
        if (Outside(slope, _layer.MinSlope, _layer.MaxSlope))
        {
            return null;
        }

        var site = new Site(spot, x, z, height, normal);
        var share = _ground.Share(_layer, site);
        return share <= 0 || Draw(spot, 2) >= share ? null : site;
    }

    private void Grow(Site site)
    {
        var scale = _layer.MinScale + (Draw(site.Spot, 3) * (_layer.MaxScale - _layer.MinScale));
        var turn = _layer.Turn ? Draw(site.Spot, 4) * 2 * Math.PI : 0.0;
        var up = Up(site.Normal, _layer.Lean);
        var lift = _layer.Lift * scale;
        var (sizeX, sizeY, sizeZ) = _layer.Size;
        var sx = sizeX * scale;
        var sy = sizeY * scale;
        var sz = sizeZ * scale;

        // A block is the cube from -1 to 1 halved and stood on its bottom face. A model already
        // stands on its origin.
        var half = _block ? 0.5 : 1.0;
        var along = lift + (_block ? sy / 2 : 0.0);
        Stand(
            up,
            turn,
            new Vector3(sx * half, sy * half, sz * half),
            new Vector3(site.X + (up.X * along), site.Height + (up.Y * along), site.Z + (up.Z * along)));
        Extend(site, Math.Sqrt((sx * sx) + (sy * sy) + (sz * sz)) + Math.Abs(lift));
        Paint(site);
    }

    // Writes one transform: turned "turn" radians about its own up, tilted so that up is "up",
    // scaled by "size" along its own axes, and moved to "at".
    private void Stand(Vector3 up, double turn, Vector3 size, Vector3 at)
    {
        // The shortest rotation from +y to up: about the axis square to both, by the angle between
        // them. Its middle column is up itself.
        var sine2 = (up.X * up.X) + (up.Z * up.Z);
        var cosine = up.Y;
        var (ax, az) = sine2 < 1e-18 ? (0.0, 0.0) : (up.Z / Math.Sqrt(sine2), -up.X / Math.Sqrt(sine2));
        var rest = 1 - cosine;
        var x0 = cosine + (rest * ax * ax);
        var y0 = -up.X;
        var z0 = rest * ax * az;
        var x2 = rest * ax * az;
        var y2 = -up.Z;
        var z2 = cosine + (rest * az * az);

        // Then the turn about +y, which comes first: it mixes the tilt's first and last columns
        // and leaves up alone.
        var c = Math.Cos(turn);
        var s = Math.Sin(turn);
        _transforms.Add(((c * x0) - (s * x2)) * size.X, ((c * y0) - (s * y2)) * size.X, ((c * z0) - (s * z2)) * size.X, 0);
        _transforms.Add(up.X * size.Y, up.Y * size.Y, up.Z * size.Y, 0);
        _transforms.Add(((s * x0) + (c * x2)) * size.Z, ((s * y0) + (c * y2)) * size.Z, ((s * z0) + (c * z2)) * size.Z, 0);
        _transforms.Add(at.X, at.Y, at.Z, 1);
    }

    private void Extend(Site site, double reach)
    {
        _minimum.SetValues(
            Math.Min(_minimum.X, site.X - reach),
            Math.Min(_minimum.Y, site.Height - reach),
            Math.Min(_minimum.Z, site.Z - reach));
        _maximum.SetValues(
            Math.Max(_maximum.X, site.X + reach),
            Math.Max(_maximum.Y, site.Height + reach),
            Math.Max(_maximum.Z, site.Z + reach));
    }

    private void Paint(Site site)
    {
        var shade = 1 + (_layer.ColourVariation * ((Draw(site.Spot, 5) * 2) - 1));
        var red = _base.Red * shade;
        var green = _base.Green * shade;
        var blue = _base.Blue * shade;
        var tint = _layer.GroundTint;
        if (tint > 0)
        {
            var (r, g, b) = _ground.ColourAt(site.X, site.Z);
            red *= 1 + ((Channel(r) - 1) * tint);
            green *= 1 + ((Channel(g) - 1) * tint);
            blue *= 1 + ((Channel(b) - 1) * tint);
        }

        _colours.Add(red, green, blue);
    }
}

/// <summary>A growing run of floats, kept unboxed: a region of grass is millions.</summary>
internal sealed class Floats
{
    private float[] _data = new float[256];

    public int Length { get; private set; }

    public void Add(params ReadOnlySpan<double> values)
    {
        foreach (var value in values)
        {
            if (Length == _data.Length)
            {
                Array.Resize(ref _data, _data.Length * 2);
            }

            _data[Length++] = (float)value;
        }
    }

    /// <summary>The floats added, in an array of their own length.</summary>
    public float[] Done() => _data.AsSpan(0, Length).ToArray();
}

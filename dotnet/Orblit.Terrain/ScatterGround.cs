using Orblit.VectorMath;

namespace Orblit.Terrain;

/// <summary>
/// The ground one region is placed on. Read straight from the region's maps wherever everything a
/// question needs lies inside it, which is nearly everywhere, and through the terrain near its
/// edges. The answers are the terrain's own to the last bit, the same sums in the same order, so
/// where a spot falls against the edge of a region never changes what stands on it.
/// </summary>
internal sealed class Ground(Terrain terrain, TerrainRegion region)
{
    private readonly int _size = region.Size;
    private readonly long _firstI = region.Key.X * region.Size;
    private readonly long _firstJ = region.Key.Z * region.Size;
    private readonly float[] _heights = region.Heights;
    private readonly uint[] _cover = region.Cover;
    private readonly byte[] _colour = region.Colour;
    private readonly double _spacing = terrain.Spacing;

    /// <summary>Whether world (<paramref name="x"/>, <paramref name="z"/>) is in this region, as <see cref="Terrain.KeyAt"/> decides.</summary>
    public bool Holds(double x, double z)
    {
        var i = Doubles.Floor(x / _spacing) - _firstI;
        var j = Doubles.Floor(z / _spacing) - _firstJ;
        return i >= 0 && i < _size && j >= 0 && j < _size;
    }

    /// <summary>The same as <see cref="Terrain.HeightAt"/>.</summary>
    public double? HeightAt(double x, double z)
    {
        var u = x / _spacing;
        var v = z / _spacing;
        var i = Doubles.Floor(u);
        var j = Doubles.Floor(v);
        var at = Inner(i, j);
        if (at < 0)
        {
            return terrain.HeightAt(x, z);
        }

        var fu = u - i;
        var fv = v - j;
        if (Solid(at) is not { } h00 || Solid(at + _size + 1) is not { } h11)
        {
            return null;
        }

        if (fu >= fv)
        {
            return Solid(at + 1) is { } h10 ? h00 + (fu * (h10 - h00)) + (fv * (h11 - h10)) : null;
        }

        return Solid(at + _size) is { } h01 ? h00 + (fv * (h01 - h00)) + (fu * (h11 - h01)) : null;
    }

    /// <summary>The same as <see cref="Terrain.NormalAt"/>, where there is ground.</summary>
    public Vector3 NormalAt(double x, double z)
    {
        var u = x / _spacing;
        var v = z / _spacing;
        var i = Doubles.Floor(u);
        var j = Doubles.Floor(v);
        var at = Inner(i, j);
        if (at < 0)
        {
            return terrain.NormalAt(x, z) ?? throw new InvalidOperationException("There is no ground at a spot that was placed.");
        }

        var fu = u - i;
        var fv = v - j;
        var gx = 0.0;
        var gz = 0.0;
        var total = 0.0;

        // The cast makes the subtraction a double one, as it is in Dart.
        void Corner(int centre, double weight)
        {
            if (weight == 0)
            {
                return;
            }

            gx += weight * ((double)_heights[centre + 1] - _heights[centre - 1]);
            gz += weight * ((double)_heights[centre + _size] - _heights[centre - _size]);
            total += weight;
        }

        Corner(at, (1 - fu) * (1 - fv));
        Corner(at + 1, fu * (1 - fv));
        Corner(at + _size, (1 - fu) * fv);
        Corner(at + _size + 1, fu * fv);
        var scale = 1 / (total * 2 * _spacing);
        var normal = new Vector3(-gx * scale, 1, -gz * scale);
        normal.Normalize();
        return normal;
    }

    /// <summary>
    /// How much of the ground at the site is sets <paramref name="layer"/> grows on, 0 to 1: the
    /// share at each of the four nearest texels, blended the way the ground's own sets blend
    /// between them.
    /// </summary>
    public double Share(ScatterLayer layer, Site site)
    {
        if (layer.Sets.Count == 0)
        {
            return 1;
        }

        var u = site.X / _spacing;
        var v = site.Z / _spacing;
        var i = Doubles.Floor(u);
        var j = Doubles.Floor(v);
        var fu = u - i;
        var fv = v - j;
        var auto = terrain.AutoCover;
        var flat = auto.Flatness(site.Normal.Y, site.Height);

        double At(long ci, long cj)
        {
            if (CoverAt(ci, cj) is not { } cover || cover.Hole)
            {
                return 0;
            }

            var (low, high, blend) = cover.Automatic
                ? (auto.Steep, auto.Flat, flat)
                : (cover.Base, cover.Overlay, cover.Blend);
            if (low == high)
            {
                return layer.GrowsOn((int)low) ? 1 : 0;
            }

            return (layer.GrowsOn((int)low) ? 1 - blend : 0) + (layer.GrowsOn((int)high) ? blend : 0);
        }

        return (At(i, j) * (1 - fu) * (1 - fv)) +
            (At(i + 1, j) * fu * (1 - fv)) +
            (At(i, j + 1) * (1 - fu) * fv) +
            (At(i + 1, j + 1) * fu * fv);
    }

    /// <summary>The ground's colour at the texel nearest world (<paramref name="x"/>, <paramref name="z"/>), as sRGB bytes. White where there is none.</summary>
    public (int Red, int Green, int Blue) ColourAt(double x, double z)
    {
        var i = Doubles.Round(x / _spacing);
        var j = Doubles.Round(z / _spacing);
        var at = Inner(i, j);
        if (at >= 0)
        {
            return (_colour[at * 4], _colour[(at * 4) + 1], _colour[(at * 4) + 2]);
        }

        return terrain.TexelColour(i, j) is { } found ? (found.Red, found.Green, found.Blue) : (255, 255, 255);
    }

    // Where texel (i, j) is in the region's maps, or -1 unless every texel from one before it to
    // two after it, both ways, is in the region.
    private int Inner(long i, long j)
    {
        var li = i - _firstI;
        var lj = j - _firstJ;
        return li >= 1 && li <= _size - 3 && lj >= 1 && lj <= _size - 3 ? (int)((lj * _size) + li) : -1;
    }

    private double? Solid(int at) => new Cover(_cover[at]).Hole ? null : _heights[at];

    private Cover? CoverAt(long i, long j)
    {
        var at = Inner(i, j);
        return at < 0 ? terrain.TexelCover(i, j) : new Cover(_cover[at]);
    }
}

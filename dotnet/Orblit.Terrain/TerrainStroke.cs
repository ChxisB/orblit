using Orblit.VectorMath;

namespace Orblit.Terrain;

/// <summary>
/// One press of a brush on a terrain, from the pointer going down to it coming up.
/// Each <see cref="MoveTo"/> carries the brush from where it was to somewhere new, laying a dab every
/// <see cref="Brush.Spacing"/> of the way, and hands back what that move changed as a
/// <see cref="TerrainPatch"/>. Fold the patches together with <see cref="TerrainPatch.FollowedBy"/>
/// and the stroke is one step to undo, whatever the number of moves it took.
/// Writes straight into the regions' maps and touches them, so a renderer that watches
/// <see cref="TerrainRegion.Revision"/> sends only the regions the brush is in. Ground with no region
/// under it is left alone: a brush shapes the ground that exists, and making more is a separate
/// decision.
/// </summary>
public sealed class TerrainStroke
{
    private readonly Random _random;
    private (double X, double Z)? _last;
    private (double X, double Z)? _far;
    private double? _startHeight;
    private double? _farHeight;
    private double _farDistance = -1;
    private double _carry;

    // F2: nine parameters, all but the first two optional and called by name.
    public TerrainStroke(
        Terrain terrain,
        BrushTool tool,
        Brush? brush = null,
        bool invert = false,
        int set = 0,
        GroundColour? colour = null,
        double roughness = 0,
        double? height = null,
        int seed = 0,
        int tileSize = TerrainPatch.DefaultTileSize)
    {
        Terrain = terrain;
        Tool = tool;
        Brush = brush ?? new Brush();
        Invert = invert;
        Set = set;
        Colour = colour ?? GroundColour.None;
        Roughness = roughness;
        Height = height;
        TileSize = tileSize;
        // Decision 77: a seed repeats a jitter, though not the one Dart's Random gave.
        _random = new Random(seed);
    }

    public Terrain Terrain { get; }

    public BrushTool Tool { get; }

    public Brush Brush { get; }

    /// <summary>
    /// Whether the tool works backwards: lowers for <see cref="BrushTool.Raise"/>, fills for
    /// <see cref="BrushTool.Hole"/>, hands painted ground back for the paint tools.
    /// </summary>
    public bool Invert { get; }

    /// <summary>The set <see cref="BrushTool.Cover"/> lays, by its index in <see cref="Terrain.Sets"/>.</summary>
    public int Set { get; }

    /// <summary>The colour <see cref="BrushTool.Colour"/> tints towards. Its roughness is not painted with it.</summary>
    public GroundColour Colour { get; }

    /// <summary>The nudge <see cref="BrushTool.Roughness"/> paints, −1 (a mirror) to +1 (chalk).</summary>
    public double Roughness { get; }

    /// <summary>
    /// The height <see cref="BrushTool.Flatten"/> draws the ground towards, or null for the height
    /// where the stroke began.
    /// </summary>
    public double? Height { get; }

    /// <summary>Texels a tile of the undo record is across. See <see cref="TerrainPatch"/>.</summary>
    public int TileSize { get; }

    /// <summary>How many dabs the stroke has laid.</summary>
    public int Dabs { get; private set; }

    /// <summary>Where the stroke began, or null before its first <see cref="MoveTo"/>.</summary>
    public (double X, double Z)? Start { get; private set; }

    /// <summary>
    /// Carries the brush to world (<paramref name="x"/>, <paramref name="z"/>) and says what that
    /// changed. The first move lays one dab where it lands; each after lays one every
    /// <see cref="Brush.Spacing"/> along the way from the last, and none if the brush has not gone far
    /// enough since.
    /// </summary>
    public TerrainPatch MoveTo(double x, double z)
    {
        var recorder = new TerrainRecorder(Terrain, [Tool.Layer()], TileSize);
        if (_last is not { } last)
        {
            Start = (x, z);
            _startHeight = GroundAt(x, z);
            Lay(x, z, recorder);
        }
        else
        {
            var dx = x - last.X;
            var dz = z - last.Z;
            var distance = Math.Sqrt((dx * dx) + (dz * dz));
            var step = Step;
            var along = step - _carry;
            while (along <= distance)
            {
                var t = along / distance;
                Lay(last.X + (dx * t), last.Z + (dz * t), recorder);
                along += step;
            }

            _carry = distance - (along - step);
        }

        _last = (x, z);
        return recorder.Finish();
    }

    /// <summary>
    /// Metres between dabs: the brush's spacing, but never so close that a dab lands on the same
    /// quarter-texel as the last.
    /// </summary>
    private double Step => Math.Max(
        Math.Max(Brush.Spacing, Brush.MinSpacing) * Brush.Size,
        Terrain.Spacing * 0.25);

    /// <summary>
    /// How much of a reference dab each dab is, so <see cref="Brush.Strength"/> means the same per
    /// pass at any spacing.
    /// </summary>
    private double PerDab => Step / (Brush.Size * Brush.ReferenceSpacing);

    private static int Towards(int was, int target, double share) =>
        (int)Doubles.Round(was + ((target - was) * share));

    /// <summary>How far one dab blends towards its target, where <paramref name="weight"/> of it lands.</summary>
    private double Share(double weight) =>
        1 - Math.Pow(1 - Doubles.Clamp(Brush.Strength * weight, 0.0, 1.0), PerDab);

    /// <summary>
    /// The ground's height at world (<paramref name="x"/>, <paramref name="z"/>), or the nearest
    /// texel's where a hole leaves it none. Null off the terrain.
    /// </summary>
    private double? GroundAt(double x, double z) =>
        Terrain.HeightAt(x, z) ??
        Terrain.TexelHeight(Doubles.Round(x / Terrain.Spacing), Doubles.Round(z / Terrain.Spacing));

    private void Lay(double atX, double atZ, TerrainRecorder recorder)
    {
        if (!(Brush.Size > 0))
        {
            return;
        }

        Dabs++;
        var dab = Scatter(atX, atZ);
        if (dab.I0 > dab.I1 || dab.J0 > dab.J1)
        {
            return;
        }

        recorder.Keep(dab.I0, dab.J0, dab.I1, dab.J1);
        Paint(dab);
    }

    /// <summary>The dab at (<paramref name="atX"/>, <paramref name="atZ"/>) after its jitter, and the texels it reaches.</summary>
    private Dab Scatter(double atX, double atZ)
    {
        var x = atX;
        var z = atZ;
        var radius = Brush.Radius;
        if (Brush.Jitter > 0)
        {
            var angle = _random.NextDouble() * 2 * Math.PI;
            var reach = Math.Sqrt(_random.NextDouble()) * Doubles.Clamp(Brush.Jitter, 0.0, 1.0) * radius;
            x += Math.Cos(angle) * reach;
            z += Math.Sin(angle) * reach;
        }

        var spacing = Terrain.Spacing;
        return new Dab(
            x,
            z,
            Doubles.Ceil((x - radius) / spacing),
            Doubles.Ceil((z - radius) / spacing),
            Doubles.Floor((x + radius) / spacing),
            Doubles.Floor((z + radius) / spacing));
    }

    private void Paint(Dab dab)
    {
        switch (Tool)
        {
            case BrushTool.Raise or BrushTool.Lower:
                Raise(dab);
                break;
            case BrushTool.Smooth:
                Smooth(dab);
                break;
            case BrushTool.Flatten:
                Flatten(dab);
                break;
            case BrushTool.Slope:
                Slope(dab);
                break;
            case BrushTool.Cover:
                PaintCover(dab);
                break;
            case BrushTool.Hole:
                Punch(dab);
                break;
            case BrushTool.Colour:
                Tint(dab);
                break;
            case BrushTool.Roughness:
                Roughen(dab);
                break;
            default:
                throw new InvalidOperationException($"{Tool} is not a brush tool.");
        }
    }

    private void Raise(Dab dab)
    {
        var up = (Tool == BrushTool.Raise) != Invert;
        var rise = (up ? 1 : -1) * Brush.Strength * Step / 4;
        Each(dab, texel =>
        {
            var heights = texel.Region.Heights;
            heights[texel.At] = (float)(heights[texel.At] + (rise * texel.Weight));
        });
    }

    private void Flatten(Dab dab)
    {
        if ((Height ?? _startHeight) is not { } target)
        {
            return;
        }

        Each(dab, texel => Draw(texel, target));
    }

    private void Punch(Dab dab) => Each(dab, texel =>
    {
        if (texel.Weight >= 0.5)
        {
            var cover = texel.Region.Cover;
            cover[texel.At] = new Cover(cover[texel.At]).WithHole(!Invert).Word;
        }
    });

    private void Tint(Dab dab)
    {
        var target = Invert ? GroundColour.None : Colour;
        Each(dab, texel =>
        {
            var share = Share(texel.Weight);
            var bytes = texel.Region.Colour;
            var at = texel.At * 4;
            bytes[at] = (byte)Towards(bytes[at], target.Red, share);
            bytes[at + 1] = (byte)Towards(bytes[at + 1], target.Green, share);
            bytes[at + 2] = (byte)Towards(bytes[at + 2], target.Blue, share);
        });
    }

    private void Roughen(Dab dab)
    {
        var target = Invert ? GroundColour.None.RoughnessByte : GroundColour.Of(roughness: Roughness).RoughnessByte;
        Each(dab, texel =>
        {
            var bytes = texel.Region.Colour;
            var at = (texel.At * 4) + 3;
            bytes[at] = (byte)Towards(bytes[at], target, Share(texel.Weight));
        });
    }

    /// <summary>Draws the height of <paramref name="texel"/> towards <paramref name="target"/>.</summary>
    private void Draw(Texel texel, double target)
    {
        var heights = texel.Region.Heights;
        var was = heights[texel.At];
        heights[texel.At] = (float)(was + (Share(texel.Weight) * (target - was)));
    }

    /// <summary>
    /// Visits every texel of the dab's area that is in a region and inside the dab, with how much of
    /// the dab reaches it, then touches the regions it visited.
    /// </summary>
    private void Each(Dab dab, Action<Texel> visit)
    {
        var size = Terrain.RegionSize;
        for (var rz = TerrainMath.FloorDiv(dab.J0, size); rz <= TerrainMath.FloorDiv(dab.J1, size); rz++)
        {
            for (var rx = TerrainMath.FloorDiv(dab.I0, size); rx <= TerrainMath.FloorDiv(dab.I1, size); rx++)
            {
                if (Terrain.RegionAt(new RegionKey(rx, rz)) is { } region && EachIn(region, dab, visit))
                {
                    region.Touch();
                }
            }
        }
    }

    /// <summary>Visits the texels of one region. Says whether there were any.</summary>
    private bool EachIn(TerrainRegion region, Dab dab, Action<Texel> visit)
    {
        var size = Terrain.RegionSize;
        var spacing = Terrain.Spacing;
        var left = region.Key.X * size;
        var top = region.Key.Z * size;
        var visited = false;
        for (var j = Math.Max(dab.J0, top); j <= Math.Min(dab.J1, top + size - 1); j++)
        {
            var dz = (j * spacing) - dab.Z;
            for (var i = Math.Max(dab.I0, left); i <= Math.Min(dab.I1, left + size - 1); i++)
            {
                var dx = (i * spacing) - dab.X;
                var weight = Brush.WeightAt(Math.Sqrt((dx * dx) + (dz * dz)));
                if (weight > 0)
                {
                    visit(new Texel(region, (int)(((j - top) * size) + (i - left)), i, j, weight));
                    visited = true;
                }
            }
        }

        return visited;
    }

    /// <summary>
    /// Draws each height towards the mean of it and its eight neighbours, all read before any is
    /// written, so the brush has no direction.
    /// </summary>
    private void Smooth(Dab dab)
    {
        var width = (int)(dab.I1 - dab.I0 + 3);
        var depth = (int)(dab.J1 - dab.J0 + 3);
        var before = new double[width * depth];
        for (var row = 0; row < depth; row++)
        {
            for (var column = 0; column < width; column++)
            {
                before[(row * width) + column] =
                    Terrain.TexelHeight(dab.I0 - 1 + column, dab.J0 - 1 + row) ?? double.NaN;
            }
        }

        Each(dab, texel =>
        {
            var was = texel.Region.Heights[texel.At];
            var mean = MeanAround(before, width, (int)(texel.I - dab.I0 + 1), (int)(texel.J - dab.J0 + 1));
            texel.Region.Heights[texel.At] = (float)(was + (Share(texel.Weight) * (mean - was)));
        });
    }

    /// <summary>The mean of the nine heights centred on a column and row, skipping any with no ground.</summary>
    private static double MeanAround(double[] heights, int width, int column, int row)
    {
        var sum = 0.0;
        var count = 0;
        for (var dj = -1; dj <= 1; dj++)
        {
            for (var di = -1; di <= 1; di++)
            {
                var height = heights[((row + dj) * width) + column + di];
                if (!double.IsNaN(height))
                {
                    sum += height;
                    count++;
                }
            }
        }

        return sum / count;
    }

    /// <summary>
    /// Draws the ground towards a straight ramp from the stroke's start, at the height it had then,
    /// to the farthest dab from there, at the height it had when the brush got there. Ground beyond
    /// either end is left alone, so climbing onto a ledge does not level the ledge.
    /// On the way out the far end is the dab itself, and the ramp follows the ground the brush finds;
    /// on the way back it stays put, and the brush lays the ramp over the ground it crossed.
    /// </summary>
    private void Slope(Dab dab)
    {
        var (startX, startZ) = Start!.Value;
        if (_startHeight is not { } low)
        {
            return;
        }

        var outX = dab.X - startX;
        var outZ = dab.Z - startZ;
        var farthest = (outX * outX) + (outZ * outZ);
        if (farthest >= _farDistance)
        {
            if (GroundAt(dab.X, dab.Z) is not { } ground)
            {
                return;
            }

            _far = (dab.X, dab.Z);
            _farHeight = ground;
            _farDistance = farthest;
        }

        var (farX, farZ) = _far!.Value;
        var high = _farHeight!.Value;
        var runX = farX - startX;
        var runZ = farZ - startZ;
        var run = (runX * runX) + (runZ * runZ);
        var spacing = Terrain.Spacing;

        // Too short to have a direction.
        if (run < spacing * spacing * 0.25)
        {
            return;
        }

        Each(dab, texel =>
        {
            var along = ((((texel.I * spacing) - startX) * runX) + (((texel.J * spacing) - startZ) * runZ)) / run;
            if (along >= 0 && along <= 1)
            {
                Draw(texel, low + (along * (high - low)));
            }
        });
    }

    private void PaintCover(Dab dab) => Each(dab, texel =>
    {
        var cover = texel.Region.Cover;
        var was = new Cover(cover[texel.At]);
        var now = Invert
            ? (texel.Weight >= 0.5 ? was.WithAutomatic(true) : was)
            : Laid(Placed(was, texel), Share(texel.Weight));
        cover[texel.At] = now.Word;
    });

    /// <summary>
    /// <paramref name="was"/> as the sets it shows: automatic ground named as the sets its slope and
    /// height choose, so painting over it starts from what was seen rather than from set 0.
    /// </summary>
    private Cover Placed(Cover was, Texel texel)
    {
        if (!was.Automatic)
        {
            return was;
        }

        var spacing = Terrain.Spacing;
        var normal = Terrain.NormalAt(texel.I * spacing, texel.J * spacing);
        var chosen = Terrain.AutoCover.CoverFor(normal?.Y ?? 1, texel.Region.Heights[texel.At]);
        return was
            .WithAutomatic(false)
            .WithBase(chosen.Base)
            .WithOverlay(chosen.Overlay)
            .WithBlend(chosen.Blend);
    }

    /// <summary>
    /// <paramref name="was"/> with <paramref name="share"/> more of <see cref="Set"/> showing.
    /// A texel holds two sets. Where <see cref="Set"/> is one of them the blend moves towards it;
    /// where it is neither, whichever of the two shows more stays as the base and <see cref="Set"/>
    /// becomes the overlay. Whole steps of the blend, and at least one, so a light brush still gets
    /// all the way there.
    /// </summary>
    private Cover Laid(Cover was, double share)
    {
        if (share <= 0)
        {
            return was;
        }

        if (was.Overlay == Set)
        {
            var left = Cover.BlendSteps - was.BlendStep;
            var step = was.BlendStep + (int)Doubles.Ceil(share * left);
            return step >= Cover.BlendSteps
                ? was.WithBase(Set).WithBlend(0)
                : was.WithBlend((double)step / Cover.BlendSteps);
        }

        if (was.Base == Set)
        {
            var step = was.BlendStep - (int)Doubles.Ceil(share * was.BlendStep);
            return was.WithBlend((double)Math.Max(step, 0) / Cover.BlendSteps);
        }

        var kept = (was.BlendStep * 2) > Cover.BlendSteps ? was.Overlay : was.Base;
        return was.WithBase(kept).WithOverlay(Set).WithBlend(share);
    }

    /// <summary>One dab: where it lands after its jitter, and the texels from (<c>I0</c>, <c>J0</c>) to (<c>I1</c>, <c>J1</c>) it reaches.</summary>
    private readonly record struct Dab(double X, double Z, long I0, long J0, long I1, long J1);

    /// <summary>One texel of a dab: its region, its index in the region's maps, where it is, and how much of the dab reaches it.</summary>
    private readonly record struct Texel(TerrainRegion Region, int At, long I, long J, double Weight);
}

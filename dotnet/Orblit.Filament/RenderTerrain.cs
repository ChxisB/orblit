using System.Runtime.InteropServices;

namespace Orblit.Filament;

/// <summary>
/// One square of ground: a height, a cover word and a colour for each texel. The three maps are
/// the same size, which is a power of two on a side once the region joins a terrain.
/// </summary>
public sealed class TerrainRegion
{
    public TerrainRegion(int x, int z, float[] heights, uint[] cover, byte[] colour)
    {
        Size = (int)Math.Round(Math.Sqrt(heights.Length), MidpointRounding.AwayFromZero);
        if (Size * Size != heights.Length || cover.Length != heights.Length || colour.Length != heights.Length * 4)
        {
            throw new ArgumentException(
                "A region is a square of heights, as many cover words, and four colour bytes a texel; "
                + $"these are {heights.Length}, {cover.Length} and {colour.Length}.");
        }

        X = x;
        Z = z;
        Heights = heights;
        Cover = cover;
        Colour = colour;
    }

    /// <summary>Where the region sits on the grid of regions, in whole regions.</summary>
    public int X { get; }

    public int Z { get; }

    /// <summary>Texels along one side.</summary>
    public int Size { get; }

    /// <summary>Metres, row by row.</summary>
    public float[] Heights { get; }

    /// <summary>One word of cover for each texel, which says what covers the ground there.</summary>
    public uint[] Cover { get; }

    /// <summary>Four bytes for each texel.</summary>
    public byte[] Colour { get; }

    /// <summary>Bumped whenever the maps change, so the renderer is sent them only when they have.</summary>
    public int Revision { get; init; }

    /// <summary>
    /// The three maps end to end: heights, then cover, then colour. The renderer reads them in the
    /// host's byte order, which is little on every platform it is built for.
    /// </summary>
    internal void WriteTo(byte[] into, int at)
    {
        var texels = Heights.Length;
        MemoryMarshal.AsBytes(Heights.AsSpan()).CopyTo(into.AsSpan(at));
        MemoryMarshal.AsBytes(Cover.AsSpan()).CopyTo(into.AsSpan(at + (texels * 4)));
        Colour.CopyTo(into, at + (texels * 8));
    }
}

/// <summary>A pair of pictures that cover ground, and how often they repeat.</summary>
public sealed record TerrainSet
{
    /// <summary>Floats of this set in the terrain's float block.</summary>
    public const int Stride = 1;

    /// <summary>What a set with no albedo picture is drawn with: a flat grey, one texel at a time.</summary>
    public static readonly byte[] PlainAlbedo = [190, 190, 190, 128];

    /// <summary>What a set with no normal picture is drawn with: a flat surface.</summary>
    public static readonly byte[] PlainNormal = [128, 128, 255, 230];

    /// <summary>Four bytes a pixel, square.</summary>
    public byte[]? Albedo { get; init; }

    public byte[]? Normal { get; init; }

    /// <summary>How many metres one copy of the pictures covers.</summary>
    public double TileSize { get; init; } = 4;

    /// <summary>Whether the pictures are projected along all three axes, so a cliff face is not stretched.</summary>
    public bool Triplanar { get; init; }
}

/// <summary>
/// How a terrain is drawn, apart from what it is made of. A value with many fields, so it is
/// changed with <c>with</c>.
/// </summary>
public sealed record TerrainOptions
{
    /// <summary>Texels along a region, a power of two.</summary>
    public int RegionSize { get; init; } = 256;

    /// <summary>Metres between texels.</summary>
    public double Spacing { get; init; } = 1;

    /// <summary>Bumped whenever the sets' pictures change.</summary>
    public int PicturesRevision { get; init; }

    public double BlendSharpness { get; init; } = 0.87;

    /// <summary>The set used where the ground is steep and nothing says otherwise.</summary>
    public int AutoSteep { get; init; }

    /// <summary>The set used where the ground is flat and nothing says otherwise.</summary>
    public int AutoFlat { get; init; } = 1;

    public double AutoSlope { get; init; } = 1;

    public double AutoHeightFalloff { get; init; } = 0.1;

    /// <summary>Cells along one side of the mesh, an even number.</summary>
    public int MeshSize { get; init; } = 64;

    /// <summary>How many rings of coarser mesh surround the finest.</summary>
    public int Levels { get; init; } = 6;

    public bool CastShadows { get; init; } = true;

    public bool ReceiveShadows { get; init; } = true;
}

/// <summary>
/// A landscape: regions of height and cover, drawn as a mesh that is finer near the camera. The
/// terrain is validated when it is made, because the renderer leaves out what it cannot read and
/// then the application would believe it was drawn.
/// </summary>
public sealed class RenderTerrain
{
    public const int HeaderInts = 12;
    public const int RegionInts = 3;
    public const int Stride = 4;

    public const int MinRegionSize = 16;
    public const int MaxRegionSize = 2048;
    public const int MaxRegions = 256;

    /// <summary>The furthest apart two regions may be on either axis, in regions.</summary>
    public const int MaxSpan = 128;

    public const int MaxSets = 32;
    public const int MinMeshSize = 16;
    public const int MaxMeshSize = 256;
    public const int MaxLevels = 12;
    public const int MaxTextureSize = 4096;

    public RenderTerrain(
        int key,
        IReadOnlyList<TerrainRegion>? regions = null,
        IReadOnlyList<TerrainSet>? sets = null,
        TerrainOptions? options = null)
    {
        Key = key;
        Regions = regions ?? [];
        Sets = sets ?? [];
        Options = options ?? new TerrainOptions();
        TextureSize = TextureSizeOf(Sets);

        var problem = GridProblem() ?? CoverProblem() ?? SetsProblem() ?? RegionsProblem();
        if (problem is not null)
        {
            throw new ArgumentException(problem);
        }
    }

    /// <summary>This terrain's identity. Two terrains in one scene may not share one.</summary>
    public int Key { get; }

    public IReadOnlyList<TerrainRegion> Regions { get; }

    public IReadOnlyList<TerrainSet> Sets { get; }

    public TerrainOptions Options { get; }

    /// <summary>Pixels along a picture, taken from the first set that has one.</summary>
    public int TextureSize { get; }

    public int Flags => (Options.CastShadows ? 1 : 0) | (Options.ReceiveShadows ? 2 : 0);

    /// <summary>One bit for each set that is projected along all three axes.</summary>
    public int TriplanarMask
    {
        get
        {
            var mask = 0;
            for (var i = 0; i < Sets.Count; i++)
            {
                if (Sets[i].Triplanar)
                {
                    mask |= 1 << i;
                }
            }

            return mask;
        }
    }

    /// <summary>Bytes of pictures: every set's albedo, then every set's normal.</summary>
    public int PicturesLength => Sets.Count * TextureSize * TextureSize * 4 * 2;

    /// <summary>Bytes of one region: a height, a cover word and a colour for each texel.</summary>
    public int RegionLength => Options.RegionSize * Options.RegionSize * 12;

    public int IntCount => HeaderInts + (Regions.Count * RegionInts);

    public int FloatCount => Stride + (Sets.Count * TerrainSet.Stride);

    internal void WriteInts(int[] into, int at, bool picturesArrive, IReadOnlyList<bool> arrived)
    {
        into[at] = Key;
        into[at + 1] = Flags;
        into[at + 2] = Options.RegionSize;
        into[at + 3] = Options.MeshSize;
        into[at + 4] = Options.Levels;
        into[at + 5] = Options.AutoSteep;
        into[at + 6] = Options.AutoFlat;
        into[at + 7] = Sets.Count;
        into[at + 8] = TextureSize;
        into[at + 9] = picturesArrive ? 1 : 0;
        into[at + 10] = TriplanarMask;
        into[at + 11] = Regions.Count;
        at += HeaderInts;
        for (var i = 0; i < Regions.Count; i++)
        {
            into[at] = Regions[i].X;
            into[at + 1] = Regions[i].Z;
            into[at + 2] = arrived[i] ? 1 : 0;
            at += RegionInts;
        }
    }

    internal void WriteFloats(float[] into, int at)
    {
        into[at] = (float)Options.Spacing;
        into[at + 1] = (float)Options.BlendSharpness;
        into[at + 2] = (float)Options.AutoSlope;
        into[at + 3] = (float)Options.AutoHeightFalloff;
        at += Stride;
        foreach (var set in Sets)
        {
            into[at] = (float)set.TileSize;
            at += TerrainSet.Stride;
        }
    }

    /// <summary>
    /// Every set's albedo, then every set's normal. A set without a picture is a flat texel
    /// repeated, so the renderer always reads the same layout.
    /// </summary>
    internal void WritePictures(byte[] into, int at)
    {
        var layer = TextureSize * TextureSize * 4;
        foreach (var normals in new[] { false, true })
        {
            foreach (var set in Sets)
            {
                var picture = normals ? set.Normal : set.Albedo;
                if (picture is not null)
                {
                    picture.CopyTo(into, at);
                }
                else
                {
                    Repeat(normals ? TerrainSet.PlainNormal : TerrainSet.PlainAlbedo, into, at, layer);
                }

                at += layer;
            }
        }
    }

    private static void Repeat(byte[] texel, byte[] into, int at, int length)
    {
        for (var i = 0; i < length; i += texel.Length)
        {
            texel.CopyTo(into, at + i);
        }
    }

    private static int TextureSizeOf(IReadOnlyList<TerrainSet> sets)
    {
        foreach (var set in sets)
        {
            if ((set.Albedo ?? set.Normal) is { } picture)
            {
                return (int)Math.Round(Math.Sqrt(picture.Length / 4), MidpointRounding.AwayFromZero);
            }
        }

        return 1;
    }

    private string? GridProblem()
    {
        if (Options.RegionSize is < MinRegionSize or > MaxRegionSize || (Options.RegionSize & (Options.RegionSize - 1)) != 0)
        {
            return $"A region is a power of two texels across, from {MinRegionSize} to {MaxRegionSize}; this is {Options.RegionSize}.";
        }

        if (!double.IsFinite(Options.Spacing) || Options.Spacing <= 0)
        {
            return $"Spacing is above zero; this is {Options.Spacing}.";
        }

        if (Options.MeshSize % 2 != 0 || Options.MeshSize is < MinMeshSize or > MaxMeshSize)
        {
            return $"The mesh is an even number of cells from {MinMeshSize} to {MaxMeshSize}; this is {Options.MeshSize}.";
        }

        if (Options.Levels is < 1 or > MaxLevels)
        {
            return $"There are 1 to {MaxLevels} levels; this is {Options.Levels}.";
        }

        return null;
    }

    private string? CoverProblem()
    {
        if (Options.AutoSteep is < 0 or >= MaxSets || Options.AutoFlat is < 0 or >= MaxSets)
        {
            return $"The automatic cover names sets 0 to {MaxSets - 1}; these are {Options.AutoSteep} and {Options.AutoFlat}.";
        }

        if (!double.IsFinite(Options.BlendSharpness) || !double.IsFinite(Options.AutoSlope) || !double.IsFinite(Options.AutoHeightFalloff))
        {
            return "The blend and the automatic cover are finite numbers.";
        }

        return null;
    }

    private string? SetsProblem()
    {
        if (Sets.Count > MaxSets)
        {
            return $"A terrain has at most {MaxSets} sets; this has {Sets.Count}.";
        }

        if (TextureSize is < 1 or > MaxTextureSize)
        {
            return $"Pictures are 1 to {MaxTextureSize} pixels across; these are {TextureSize}.";
        }

        var picture = TextureSize * TextureSize * 4;
        for (var i = 0; i < Sets.Count; i++)
        {
            if (!double.IsFinite(Sets[i].TileSize) || Sets[i].TileSize <= 0)
            {
                return $"Set {i} covers {Sets[i].TileSize} metres a copy; it must be above zero.";
            }

            foreach (var bytes in new[] { Sets[i].Albedo, Sets[i].Normal })
            {
                if (bytes is not null && bytes.Length != picture)
                {
                    return $"Every picture is {TextureSize} pixels square, as the first is; set {i} has one of {bytes.Length} bytes.";
                }
            }
        }

        return null;
    }

    private string? RegionsProblem()
    {
        if (Regions.Count > MaxRegions)
        {
            return $"A terrain has at most {MaxRegions} regions; this has {Regions.Count}.";
        }

        if (Regions.Count > 0)
        {
            var wide = Regions.Max(region => region.X) - Regions.Min(region => region.X);
            var deep = Regions.Max(region => region.Z) - Regions.Min(region => region.Z);

            // The renderer leaves such a region out rather than refusing it, and then the view
            // would count it as held and never send it again.
            if (wide >= MaxSpan || deep >= MaxSpan)
            {
                return $"A terrain's regions lie within {MaxSpan} of each other each way; these reach {wide} across and {deep} deep.";
            }
        }

        var seen = new HashSet<(int, int)>();
        foreach (var region in Regions)
        {
            if (region.Size != Options.RegionSize)
            {
                return $"Region ({region.X}, {region.Z}) is {region.Size} across and the terrain's are {Options.RegionSize}.";
            }

            if (!seen.Add((region.X, region.Z)))
            {
                return $"Region ({region.X}, {region.Z}) is named twice.";
            }
        }

        return null;
    }
}

/// <summary>
/// What the renderer already holds of a terrain, which decides what is sent the next time: the
/// pictures only if they changed, and a region only if it is new or its revision moved.
/// </summary>
public sealed class TerrainHeld
{
    private readonly int _regionSize;
    private readonly int _picturesRevision;
    private readonly int _setCount;
    private readonly int _textureSize;
    private readonly Dictionary<(int, int), int> _regions;

    private TerrainHeld(RenderTerrain terrain)
    {
        _regionSize = terrain.Options.RegionSize;
        _picturesRevision = terrain.Options.PicturesRevision;
        _setCount = terrain.Sets.Count;
        _textureSize = terrain.TextureSize;
        _regions = [];
        foreach (var region in terrain.Regions)
        {
            _regions[(region.X, region.Z)] = region.Revision;
        }
    }

    public static TerrainHeld Of(RenderTerrain terrain) => new(terrain);

    public bool HoldsPictures(RenderTerrain terrain) =>
        terrain.Options.PicturesRevision == _picturesRevision
        && terrain.Sets.Count == _setCount
        && terrain.TextureSize == _textureSize;

    public bool HoldsRegion(RenderTerrain terrain, TerrainRegion region) =>
        terrain.Options.RegionSize == _regionSize
        && _regions.TryGetValue((region.X, region.Z), out var held)
        && held == region.Revision;
}

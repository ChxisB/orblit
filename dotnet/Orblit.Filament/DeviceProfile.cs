namespace Orblit.Filament;

/// <summary>
/// How much a device can be asked to do, as one word. Most quality choices hang off this and not
/// off the numbers under it, because the numbers only mean anything together: sixteen cores and a
/// feature level one GPU is still a device that draws the slim surface.
/// </summary>
public enum DeviceTier
{
    Low,
    Medium,
    High,
}

/// <summary>
/// What the device under a viewport can do, as its renderer measured it. Every answer is a fact
/// about the device and the driver, taken once when the renderer started, so a profile can be kept
/// for the life of the view.
/// </summary>
public sealed record DeviceProfile
{
    /// <summary>The format bits <see cref="FormatFamily"/> names. Any other bit is not a family.</summary>
    private const int FamilyBits = 0x3F;

    private static readonly string[] Siblings = [".astc", ".bc", ".etc2"];

    public Backend Api { get; init; } = Backend.Default;

    /// <summary>The renderer's feature level, 0 to 3. Below 3 it draws the slim lit surface: nine samplers and not twelve.</summary>
    public int FeatureLevel { get; init; } = 1;

    /// <summary>Texels on a side of the largest 2D texture.</summary>
    public int MaxTextureSize { get; init; } = 2048;

    public int MaxArrayTextureLayers { get; init; } = 256;

    /// <summary>The block compressed families this device can sample.</summary>
    public FormatFamily TextureFamilies { get; init; }

    /// <summary>Whether a half float texture can be sampled and mipmapped, which an environment prefiltered at run time needs.</summary>
    public bool HalfFloatTextures { get; init; }

    /// <summary>Threads work can spread across. One in a browser without threads.</summary>
    public int WorkerThreads { get; init; } = 1;

    /// <summary>Physical memory, or null where the platform will not say, as a browser will not.</summary>
    public int? SystemMemoryMegabytes { get; init; }

    /// <summary>Whether this device draws the slim lit surface.</summary>
    public bool SlimSurface => FeatureLevel < 3;

    /// <summary>
    /// Low is anything that cannot draw the standard surface, cannot hold a 4096 texture, has two
    /// threads or fewer, or has less than three gigabytes. High needs the standard surface, 8192
    /// textures, eight threads and eight gigabytes that it can prove: a device that will not say how
    /// much memory it has is not given the benefit of the doubt. Everything between is medium. These
    /// are starting points, to be moved by measurement on real hardware.
    /// </summary>
    public DeviceTier Tier
    {
        get
        {
            var memory = SystemMemoryMegabytes;
            if (FeatureLevel < 3 || MaxTextureSize < 4096 || WorkerThreads <= 2 || memory < 3072)
            {
                return DeviceTier.Low;
            }

            return MaxTextureSize >= 8192 && WorkerThreads >= 8 && memory >= 8192
                ? DeviceTier.High
                : DeviceTier.Medium;
        }
    }

    /// <summary>The largest side a texture should be loaded at on this device, before any the asset itself asks for.</summary>
    public int TextureSizeBudget => Math.Min(MaxTextureSize, ByTier(1024, 2048, 4096));

    /// <summary>
    /// Where a frame's texture upload budget starts, in kilobytes. Only where it starts: while
    /// textures arrive the renderer moves it to what fits a frame's slack, between bounds the tier sets.
    /// </summary>
    public int TextureUploadKilobytes => ByTier(4096, 16384, 32768);

    /// <summary>How many Gaussian splats to draw at most.</summary>
    public int SplatBudget => ByTier(250_000, 1_000_000, 3_000_000);

    /// <summary>
    /// How much of a splat capture's view dependent colour to read, 0 to 3. Each degree costs
    /// sixteen bytes a splat of GPU memory, which is the budget a small device runs out of.
    /// </summary>
    public int HarmonicDegree => ByTier(0, 2, 3);

    /// <summary>Whether to sort splats on sixteen bits of depth and not thirty-two. A full sort on a low tier device lags behind a turning camera.</summary>
    public bool CoarseSplatOrder => Tier == DeviceTier.Low;

    /// <summary>
    /// The answers in <see cref="Capability"/> order, as the renderer sends them. An answer that is
    /// missing or negative, from a renderer older than the question or one that could not tell, is
    /// read as the least a device this engine runs on can have. Guessing low costs some quality.
    /// Guessing high costs a crash on the phone that could not keep up.
    /// </summary>
    public static DeviceProfile FromCapabilities(IReadOnlyList<int> answers)
    {
        var api = Known(answers, Capability.Backend);
        var memory = Known(answers, Capability.SystemMemoryMegabytes);
        return new DeviceProfile
        {
            Api = api is { } index && Enum.IsDefined((Backend)index) ? (Backend)index : Backend.Default,
            FeatureLevel = Known(answers, Capability.FeatureLevel) ?? 1,
            MaxTextureSize = Known(answers, Capability.MaxTextureSize) ?? 2048,
            MaxArrayTextureLayers = Known(answers, Capability.MaxArrayTextureLayers) ?? 256,
            TextureFamilies = (FormatFamily)((Known(answers, Capability.CompressedFormats) ?? 0) & FamilyBits),
            HalfFloatTextures = Known(answers, Capability.HalfFloatTextures) == 1,
            WorkerThreads = Math.Max(1, Known(answers, Capability.WorkerThreads) ?? 1),
            // Nought is the platform declining to say, not a machine with none.
            SystemMemoryMegabytes = memory is null or 0 ? null : memory,
        };
    }

    public bool Supports(FormatFamily family) => TextureFamilies.HasFlag(family);

    /// <summary>
    /// The names to fetch for a cooked texture, best first, for a host that hands the renderer
    /// bytes and not a file system. A cooked <c>x.ktx2</c> may have siblings beside it, <c>x.astc.ktx2</c>,
    /// <c>x.bc.ktx2</c> and <c>x.etc2.ktx2</c>, and the renderer loads the first it finds holding a
    /// format this device samples, then <c>x.ktx2</c> itself. A candidate is only a candidate, because
    /// the renderer still checks each file's real format. Anything that is not a cooked set, a PNG
    /// or a name that is already one sibling, is the only candidate for itself.
    /// </summary>
    public IReadOnlyList<string> TextureCandidates(string path)
    {
        const string Cooked = ".ktx2";
        if (!path.EndsWith(Cooked, StringComparison.OrdinalIgnoreCase))
        {
            return [path];
        }

        var stem = path[..^Cooked.Length];
        if (Siblings.Any(sibling => stem.EndsWith(sibling, StringComparison.OrdinalIgnoreCase)))
        {
            return [path];
        }

        var extension = path[^Cooked.Length..];
        var candidates = new List<string>();
        if (Supports(FormatFamily.Astc))
        {
            candidates.Add($"{stem}.astc{extension}");
        }

        // A cooked BC sibling holds BC7 for colour, BC5 for normals and BC4 for one channel.
        if (Supports(FormatFamily.Bc7) || Supports(FormatFamily.Bc45))
        {
            candidates.Add($"{stem}.bc{extension}");
        }

        if (Supports(FormatFamily.Etc2))
        {
            candidates.Add($"{stem}.etc2{extension}");
        }

        candidates.Add(path);
        return candidates;
    }

    private static int? Known(IReadOnlyList<int> answers, Capability question) =>
        (int)question < answers.Count && answers[(int)question] >= 0 ? answers[(int)question] : null;

    private T ByTier<T>(T low, T medium, T high) => Tier switch
    {
        DeviceTier.Low => low,
        DeviceTier.Medium => medium,
        _ => high,
    };
}

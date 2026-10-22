namespace Orblit.Asset;

/// <summary>
/// Turns a PNG or a JPEG into the compressed KTX2 files a device samples. The work is
/// <c>orblit_texture_cook</c>'s. What this adds is deciding the flags from the settings and the target
/// instead of from a file-name convention. <c>tool/cook_textures.sh</c> had to guess: a name ending
/// <c>_normal</c> was a normal map, a name ending <c>_mask</c> was single-channel, and a guess is wrong for
/// exactly the assets whose names came from somewhere else. A <c>.import.json</c> saying
/// <c>"normal": true</c> is not a guess.
/// </summary>
/// <remarks>
/// One cook writes one family per target, not all of them. That is the difference between an app that
/// ships ASTC and BC and ETC2 and one that ships what its device reads.
/// </remarks>
public sealed class TextureImporter(NativeTool? tool = null) : Importer
{
    private static readonly IReadOnlySet<string> Claimed = new HashSet<string> { "png", "jpg", "jpeg", "ktx2" };

    public NativeTool Tool { get; } = tool ?? new NativeTool("orblit_texture_cook", environmentVariable: "ORBLIT_TEXTURE_COOK");

    public override string Name => "texture";

    /// <summary>
    /// Bump this when the flags chosen below change, or when the encoder does. The encoder's own version is
    /// not visible from here, which is why <c>orblit_texture_cook --revisions</c> exists: a build that
    /// wants to be sure feeds it into the target instead.
    /// </summary>
    public override int Version => 1;

    public override IReadOnlySet<string> Extensions => Claimed;

    public override IReadOnlyDictionary<string, object?> ResolveSettings(ImportSettings settings)
    {
        var values = settings.Values;
        return new Dictionary<string, object?>
        {
            ["normal"] = Flag(values, "normal", false),
            ["twoChannelNormals"] = Flag(values, "twoChannelNormals", false),
            ["singleChannel"] = Flag(values, "singleChannel", false),
            ["lossless"] = Flag(values, "lossless", false),
            ["wrap"] = Flag(values, "wrap", false),
            ["mips"] = Flag(values, "mips", true),
            ["colourSpace"] = ColourSpace(values),
            ["cutout"] = Cutout(values),
            ["maxSize"] = MaxSize(values),
            ["uastc"] = WholeNumber(values, "uastc", 2, new Bounds(0, 4)),
            ["zstd"] = WholeNumber(values, "zstd", 19, new Bounds(1, 22)),
        };
    }

    public override async Task<ImportResult> ImportAsync(ImportRequest request)
    {
        var settings = request.Settings;
        var notes = new List<string>();
        var targets = TargetsOf(request);

        // `raw` is not a family the cooker writes. It is the absence of one.
        var compressed = targets.Where(family => family != TextureFamily.Raw).ToList();
        var lossless = settings.GetValueOrDefault("lossless") is true
            || (compressed.Count == 0 && targets.Contains(TextureFamily.Raw));

        var maxSize = settings.GetValueOrDefault("maxSize") as long? ?? request.Target.MaxTextureSize();
        if (settings.GetValueOrDefault("maxSize") is null && maxSize is { } held)
        {
            notes.Add($"held to {held}px by the target, which samples no larger");
        }

        return await NativeWork.WithTemporaryDirectoryAsync("texture", async work =>
        {
            var input = NativeWork.Inside(work, $"in.{request.Id.Extension}");
            await File.WriteAllBytesAsync(input, request.Bytes).ConfigureAwait(false);
            var output = Directory.CreateDirectory(NativeWork.Inside(work, "out")).FullName;

            var families = lossless ? null : string.Join(',', compressed);
            await Tool.RunAsync([.. Selection(settings, input, output, families), .. Tuning(settings, maxSize)], request.Id)
                .ConfigureAwait(false);

            var outputs = new Dictionary<string, byte[]>();
            foreach (var written in Directory.EnumerateFiles(output))
            {
                outputs[Path.GetFileName(written)] = await File.ReadAllBytesAsync(written).ConfigureAwait(false);
            }

            return outputs.Count == 0
                ? throw new ImportFailureException(
                    request.Id,
                    "cooked without error but produced no files, which means the flags asked for nothing. "
                    + $"Targets were {string.Join(", ", targets)}.")
                : new ImportResult { Outputs = outputs, Notes = notes };
        }).ConfigureAwait(false);
    }

    // An empty list means the caller did not say, and every family is written. That is right for a
    // project-wide cook whose bundles are split later, and wrong for an app build, which is why the cook
    // command always names them.
    private static IReadOnlyList<string> TargetsOf(ImportRequest request)
    {
        var families = request.Target.TextureFamilies();
        var targets = families.Count == 0 ? TextureFamily.All : families;
        var unknown = targets.Where(family => !TextureFamily.All.Contains(family)).ToList();
        return unknown.Count == 0
            ? targets
            : throw new ImportFailureException(
                request.Id,
                $"is being cooked for {string.Join(", ", unknown)}, which is not a texture family. "
                + $"The families are {string.Join(", ", TextureFamily.All)}.");
    }

    // What to read and write, which families (none means lossless), and what kind of image it is.
    private static IEnumerable<string> Selection(
        IReadOnlyDictionary<string, object?> settings, string input, string output, string? families)
    {
        yield return input;
        yield return NativeWork.Beside(output, "");
        if (families is null)
        {
            yield return "--lossless";
        }
        else
        {
            yield return "--targets";
            yield return families;
        }

        if (settings.GetValueOrDefault("normal") is true)
        {
            yield return "--normal";
        }

        if (settings.GetValueOrDefault("twoChannelNormals") is true)
        {
            yield return "--two-channel-normals";
        }

        if (settings.GetValueOrDefault("singleChannel") is true)
        {
            yield return "--single-channel";
        }

        if (settings.GetValueOrDefault("colourSpace") is "srgb")
        {
            yield return "--srgb";
        }

        if (settings.GetValueOrDefault("colourSpace") is "linear")
        {
            yield return "--linear";
        }
    }

    // How the encoder is to spend its effort, and how large the result may be.
    private static IEnumerable<string> Tuning(IReadOnlyDictionary<string, object?> settings, long? maxSize)
    {
        if (settings.GetValueOrDefault("cutout") is { } cutout)
        {
            yield return "--cutout";
            yield return TextRules.Print(cutout);
        }

        if (settings.GetValueOrDefault("mips") is not true)
        {
            yield return "--no-mips";
        }

        if (settings.GetValueOrDefault("wrap") is true)
        {
            yield return "--wrap";
        }

        if (maxSize is { } size)
        {
            yield return "--max-size";
            yield return TextRules.Print(size);
        }

        yield return "--uastc";
        yield return TextRules.Print(settings.GetValueOrDefault("uastc"));
        yield return "--zstd";
        yield return TextRules.Print(settings.GetValueOrDefault("zstd"));
        yield return "--quiet";
    }

    private static bool Flag(IReadOnlyDictionary<string, object?> values, string key, bool fallback) =>
        values.GetValueOrDefault(key) switch
        {
            null => fallback,
            bool flag => flag,
            var other => throw new FormatException($"\"{key}\" is {TextRules.Print(other)}, and it has to be true or false."),
        };

    private static long WholeNumber(IReadOnlyDictionary<string, object?> values, string key, long fallback, Bounds bounds) =>
        values.GetValueOrDefault(key) switch
        {
            null => fallback,
            long number when number >= bounds.Min && number <= bounds.Max => number,
            var other => throw new FormatException(
                $"\"{key}\" is {TextRules.Print(other)}, and it has to be a whole number from {bounds.Min} to {bounds.Max}."),
        };

    private static string? ColourSpace(IReadOnlyDictionary<string, object?> values)
    {
        var value = values.GetValueOrDefault("colourSpace") ?? values.GetValueOrDefault("colorSpace");
        return value switch
        {
            null => null,
            "srgb" or "linear" => (string)value,
            _ => throw new FormatException(
                $"\"colourSpace\" is {TextRules.Print(value)}, and it is either \"srgb\", \"linear\", or left out so that the file decides."),
        };
    }

    private static double? Cutout(IReadOnlyDictionary<string, object?> values) =>
        values.GetValueOrDefault("cutout") switch
        {
            null => null,
            long whole when whole is >= 0 and <= 1 => whole,
            double fraction when !(fraction < 0 || fraction > 1) => fraction, // NaN passes
            var other => throw new FormatException($"\"cutout\" is {TextRules.Print(other)}, and an alpha threshold is between 0 and 1."),
        };

    private static long? MaxSize(IReadOnlyDictionary<string, object?> values) =>
        values.GetValueOrDefault("maxSize") switch
        {
            null => null,
            long size when long.IsPow2(size) => size,
            var other => throw new FormatException(
                $"\"maxSize\" is {TextRules.Print(other)}, and it has to be a power of two: mip levels are dropped to reach it, "
                + "and a level is always half the one above."),
        };

    private readonly record struct Bounds(long Min, long Max);
}

namespace Orblit.Asset;

/// <summary>
/// Turns a splat capture into the <c>.osplat</c> a launch can read without parsing. A <c>.ply</c> from a
/// capture pipeline spends its load time on an exponential, a quaternion and a covariance for every splat,
/// and on two passes over its harmonics. None of that happens again once it is cooked, which is the
/// difference between a scene that opens and one that waits.
/// </summary>
/// <remarks>
/// Both settings are about size rather than quality in the abstract: a phone gets fewer bands and fewer
/// splats, and which splats it loses is decided by the same opacity-and-size ranking the renderer would have
/// applied at runtime, so the small file looks like the big one, not like a random half of it.
/// </remarks>
public sealed class SplatImporter(NativeTool? tool = null) : Importer
{
    private static readonly IReadOnlySet<string> Claimed = new HashSet<string> { "ply", "spz", "splat", "osplat" };

    public NativeTool Tool { get; } = tool ?? new NativeTool("orblit_splat_cook", environmentVariable: "ORBLIT_SPLAT_COOK");

    public override string Name => "splats";

    public override int Version => 1;

    public override IReadOnlySet<string> Extensions => Claimed;

    public override IReadOnlyDictionary<string, object?> ResolveSettings(ImportSettings settings)
    {
        var harmonics = settings.Values.GetValueOrDefault("harmonics");
        if (harmonics is not null && harmonics is not (>= 0L and <= 3L))
        {
            throw new FormatException(
                $"\"harmonics\" is {TextRules.Print(harmonics)}, and it is how many bands to keep: 0 to 3.");
        }

        var limit = settings.Values.GetValueOrDefault("limit");
        if (limit is not null && limit is not > 0L)
        {
            throw new FormatException(
                $"\"limit\" is {TextRules.Print(limit)}, and it is the most splats to keep, so it is a "
                + "positive whole number. Leave it out to keep all of them.");
        }

        return new Dictionary<string, object?> { ["harmonics"] = harmonics ?? 3L, ["limit"] = limit };
    }

    public override Task<ImportResult> ImportAsync(ImportRequest request) =>
        NativeWork.WithTemporaryDirectoryAsync("splat", async work =>
        {
            var input = NativeWork.Inside(work, $"in.{request.Id.Extension}");
            await File.WriteAllBytesAsync(input, request.Bytes).ConfigureAwait(false);
            var output = NativeWork.Inside(work, "out.osplat");

            var limit = request.Settings.GetValueOrDefault("limit");
            string[] arguments = [input, output, "--harmonics", TextRules.Print(request.Settings.GetValueOrDefault("harmonics"))];
            await Tool.RunAsync(limit is null ? arguments : [.. arguments, "--limit", TextRules.Print(limit)], request.Id)
                .ConfigureAwait(false);

            return File.Exists(output)
                ? new ImportResult
                {
                    Outputs = new Dictionary<string, byte[]> { ["osplat"] = await File.ReadAllBytesAsync(output).ConfigureAwait(false) },
                }
                : throw new ImportFailureException(request.Id, "cooked no .osplat");
        });
}

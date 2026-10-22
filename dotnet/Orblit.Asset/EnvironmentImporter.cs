namespace Orblit.Asset;

/// <summary>
/// Bakes an HDR or EXR panorama into the two KTX files the renderer lights a scene with: the reflections and
/// the backdrop. The work is Filament's <c>cmgen</c>, at the sizes <c>tool/bake_environment.sh</c> settled
/// on. The skybox is four times the reflection map because the backdrop is looked at directly and the
/// reflections are not, and it is held to 1024 because beyond that nobody can tell and everybody pays.
/// </summary>
/// <remarks>
/// A scene with one skybox slot and three things that want to own it shows "stamps" in the sky. Cooking the
/// backdrop does not cause that, but it is the file that gets blamed for it.
/// </remarks>
public sealed class EnvironmentImporter(NativeTool? tool = null) : Importer
{
    // cmgen names what it writes after the input file, so the input is given the name the outputs should
    // have rather than renamed after.
    private const string Stem = "environment";

    private static readonly IReadOnlySet<string> Claimed = new HashSet<string> { "hdr", "exr" };

    public NativeTool Tool { get; } = tool ?? new NativeTool("cmgen", environmentVariable: "ORBLIT_CMGEN");

    public override string Name => "environment";

    public override int Version => 1;

    public override IReadOnlySet<string> Extensions => Claimed;

    public override IReadOnlyDictionary<string, object?> ResolveSettings(ImportSettings settings)
    {
        var size = Face(settings.Values, "size") ?? 256L;
        var skybox = Face(settings.Values, "skyboxSize");
        return new Dictionary<string, object?>
        {
            ["size"] = size,
            // Four times the reflections, held to 1024: the backdrop is the part anyone looks at directly, and
            // past 1024 it stops paying for itself.
            ["skyboxSize"] = skybox ?? Math.Clamp(size * 4, 1L, 1024L),
        };
    }

    public override Task<ImportResult> ImportAsync(ImportRequest request) =>
        NativeWork.WithTemporaryDirectoryAsync("environment", async work =>
        {
            var input = NativeWork.Inside(work, $"{Stem}.{request.Id.Extension}");
            await File.WriteAllBytesAsync(input, request.Bytes).ConfigureAwait(false);

            var reflections = NativeWork.Inside(work, "ibl");
            var sky = NativeWork.Inside(work, "sky");

            await Tool.RunAsync(
                [
                    "--quiet",
                    "--format=ktx",
                    $"--size={TextRules.Print(request.Settings.GetValueOrDefault("size"))}",
                    $"--deploy={NativeWork.Beside(reflections, Stem)}",
                    input,
                ],
                request.Id).ConfigureAwait(false);
            await Tool.RunAsync(
                [
                    "--quiet",
                    "--format=ktx",
                    $"--size={TextRules.Print(request.Settings.GetValueOrDefault("skyboxSize"))}",
                    $"--extract={NativeWork.Beside(sky, Stem)}",
                    input,
                ],
                request.Id).ConfigureAwait(false);

            return new ImportResult
            {
                Outputs = new Dictionary<string, byte[]>
                {
                    ["ibl.ktx"] = await ReadOrAsync(request, NativeWork.Beside(NativeWork.Beside(reflections, Stem), $"{Stem}_ibl.ktx")).ConfigureAwait(false),
                    ["skybox.ktx"] = await ReadOrAsync(request, NativeWork.Beside(NativeWork.Beside(sky, Stem), $"{Stem}_skybox.ktx")).ConfigureAwait(false),
                    // The harmonics at full precision, which the renderer does not read but a person comparing
                    // two bakes does.
                    ["sh.txt"] = await ReadOrAsync(request, NativeWork.Beside(NativeWork.Beside(reflections, Stem), "sh.txt")).ConfigureAwait(false),
                },
            };
        });

    private static long? Face(IReadOnlyDictionary<string, object?> values, string key) =>
        values.GetValueOrDefault(key) switch
        {
            null => null,
            long size when long.IsPow2(size) => size,
            var other => throw new FormatException($"\"{key}\" is {TextRules.Print(other)}, and a cubemap face is a power of two."),
        };

    private static async Task<byte[]> ReadOrAsync(ImportRequest request, string path) =>
        File.Exists(path)
            ? await File.ReadAllBytesAsync(path).ConfigureAwait(false)
            : throw new ImportFailureException(
                request.Id,
                $"baked without error, but cmgen did not write {path}. A cmgen from a different Filament version names its outputs differently.");
}

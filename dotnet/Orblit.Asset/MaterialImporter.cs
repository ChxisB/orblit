using System.Text;
using Orblit.Json;

namespace Orblit.Asset;

/// <summary>
/// Cooks an author's <c>.oshader</c> into a Filament material package. The text is translated to a
/// <c>.mat</c> and compiled by <c>matc</c> for the backends the target draws with. The outputs are
/// <c>material.filamat</c>, which the renderer loads by name, and <c>params.json</c>, which lists the
/// uniforms with their hints so an editor can show a colour row or a slider without reading the source
/// again. A mistake comes back as <c>file:line: message</c> at the author's own line, whether the translator
/// or matc found it.
/// </summary>
public sealed class MaterialImporter : Importer
{
    private static readonly IReadOnlySet<string> Claimed = new HashSet<string> { "oshader" };

    // Cook workers share one importer. Two of them asking at once is harmless, and a failure is not kept.
    private readonly Lazy<string> _matcVersion;

    public MaterialImporter(NativeTool? tool = null)
    {
        Tool = tool ?? new NativeTool("matc", environmentVariable: "ORBLIT_MATC");
        _matcVersion = new Lazy<string>(AskMatc, LazyThreadSafetyMode.PublicationOnly);
    }

    public NativeTool Tool { get; }

    public override string Name => "material";

    public override int Version => 1;

    public override IReadOnlySet<string> Extensions => Claimed;

    /// <summary>
    /// The matc that cooks must be the matc the runtime Filament was built with, so which one it is belongs in
    /// the key. A package from another version is refused when it loads, which looks like a missing material.
    /// </summary>
    public override IReadOnlyDictionary<string, object?> ResolveSettings(ImportSettings settings) =>
        new Dictionary<string, object?> { ["matc"] = _matcVersion.Value };

    /// <summary>
    /// Every file the source includes, however deep. An include that does not exist is left out. The
    /// translator reports it at its line, and once the file is written the key changes.
    /// </summary>
    public override async Task<IReadOnlySet<AssetId>> DependenciesInAsync(
        AssetId id, byte[] bytes, IReadOnlyDictionary<string, object?> settings, IAssetSource source)
    {
        var files = await ReadIncludesAsync(source, id, TextRules.Decode(bytes)).ConfigureAwait(false);
        return new HashSet<AssetId>(files.Keys.Select(AssetId.Parse));
    }

    public override async Task<ImportResult> ImportAsync(ImportRequest request)
    {
        var id = request.Id;
        var text = TextRules.Decode(request.Bytes);
        var files = await ReadIncludesAsync(request.Source, id, text).ConfigureAwait(false);
        SurfaceTranslation translation;
        try
        {
            translation = SurfaceTranslator.Translate(id.Name.Split('.')[0], id.ToString(), text, include => files.GetValueOrDefault(include));
        }
        catch (SurfaceException error)
        {
            throw new ImportFailureException(id, error.Message);
        }

        return await NativeWork.WithTemporaryDirectoryAsync("material", async work =>
        {
            var input = NativeWork.Inside(work, "in.mat");
            await File.WriteAllTextAsync(input, translation.Material).ConfigureAwait(false);
            var output = NativeWork.Inside(work, "out.filamat");
            try
            {
                await Tool.RunAsync([.. FlagsFor(request.Target), "-o", output, input], id).ConfigureAwait(false);
            }
            catch (ImportFailureException failure)
            {
                var mapped = SurfaceDiagnostics.Of($"{failure.Cause}", translation);
                if (mapped.Count == 0)
                {
                    throw;
                }

                throw new ImportFailureException(id, string.Join('\n', mapped));
            }

            return new ImportResult
            {
                Outputs = new Dictionary<string, byte[]>
                {
                    ["material.filamat"] = await File.ReadAllBytesAsync(output).ConfigureAwait(false),
                    ["params.json"] = Encoding.UTF8.GetBytes(JsonText.Encode(translation.Params.Select(param => param.ToJson()).ToList())),
                },
            };
        }).ConfigureAwait(false);
    }

    // The backends and shader family each target draws with, as the setup script of each platform compiles
    // its own materials.
    private static string[] FlagsFor(CookTarget target) => target.Name switch
    {
        "macos" or "ios" => ["-a", "metal", "-p", "all"],
        "windows" or "linux" => ["-a", "vulkan", "-a", "opengl", "-p", "desktop"],
        "android" => ["-a", "opengl", "-a", "vulkan", "-p", "mobile"],
        "web" => ["-a", "webgpu", "-p", "all"],
        _ => ["-a", "all", "-p", "all"],
    };

    private static async Task<Dictionary<string, string>> ReadIncludesAsync(IAssetSource source, AssetId id, string text)
    {
        var files = new Dictionary<string, string>();
        try
        {
            var queue = SurfaceLines.IncludePaths(id.ToString(), text);
            while (queue.Count > 0)
            {
                var next = queue[^1];
                queue.RemoveAt(queue.Count - 1);
                var include = AssetId.TryParse(next);
                if (files.ContainsKey(next) || include is null || !await source.ExistsAsync(include).ConfigureAwait(false))
                {
                    continue;
                }

                var body = TextRules.Decode(await source.ReadAsync(include).ConfigureAwait(false));
                files[next] = body;
                queue.AddRange(SurfaceLines.IncludePaths(next, body));
            }
        }
        catch (SurfaceException error)
        {
            throw new ImportFailureException(id, error.Message);
        }

        return files;
    }

    private string AskMatc()
    {
        var program = Tool.Locate();
        if (program is null)
        {
            return "missing";
        }

        var result = NativeTool.Capture(program, ["--version"]);
        return result.ExitCode == 0 ? PlainText.Trim(result.Output) : "unknown";
    }
}

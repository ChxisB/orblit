using System.Text;
using Orblit.Json;

namespace Orblit.Asset.Tests;

public sealed class MaterialImporterTests : NativeToolWork
{
    private const string Source = """
        render_mode unshaded;
        uniform vec4 tint : source_color = vec4(1.0);
        void fragment() {
          ALBEDO = tint.rgb;
        }

        """;


    public static bool HasMatc => new NativeTool("matc", environmentVariable: "ORBLIT_MATC").Locate() is not null;

    private static ImportRequest Request(
        string text, string path = "materials/flat.oshader", CookTarget? target = null, Dictionary<string, string>? files = null)
    {
        var assets = new Dictionary<AssetId, byte[]> { [AssetId.Parse(path)] = Encoding.UTF8.GetBytes(text) };
        foreach (var (name, body) in files ?? [])
        {
            assets[AssetId.Parse(name)] = Encoding.UTF8.GetBytes(body);
        }

        return new ImportRequest
        {
            Id = AssetId.Parse(path),
            Bytes = Encoding.UTF8.GetBytes(text),
            Settings = new Dictionary<string, object?>(),
            Target = target ?? CookTargets.Macos,
            Source = new MemoryAssetSource(assets),
        };
    }

    // A matc that records its arguments and then does body. It is a real process run through the real
    // NativeTool, so the flags and the exit code are what the importer would meet.
    private NativeTool FakeMatc(string body) => Script(
        "matc",
        $$"""
        echo "$@" >> "{{Work.FullName}}/record"
        [ "$1" = "--version" ] && { echo 77; exit 0; }
        out=""
        while [ $# -gt 0 ]; do [ "$1" = "-o" ] && out="$2"; shift; done
        {{body}}

        """,
        "matc");

    private List<string> Flags() => [.. File.ReadAllLines(NativeWork.Inside(Work, "record")).Where(line => line != "--version")];

    [ShellFact]
    public async Task WritesThePackageAndTheParameterList()
    {
        var importer = new MaterialImporter(FakeMatc("printf PACKAGE > \"$out\""));

        var result = await importer.ImportAsync(Request(Source));

        Assert.Equal("PACKAGE", Encoding.UTF8.GetString(result.Outputs["material.filamat"]));
        var decoded = Assert.IsAssignableFrom<IReadOnlyList<object?>>(DartJson.Decode(Encoding.UTF8.GetString(result.Outputs["params.json"])));
        var param = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(Assert.Single(decoded));
        Assert.Equal("tint", param["name"]);
        Assert.Equal("vec4", param["type"]);
        Assert.Equal(false, param["instance"]);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<object?>>(Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(param["hints"])["source_color"]));
        Assert.Equal([1.0, 1.0, 1.0, 1.0], Assert.IsAssignableFrom<IReadOnlyList<object?>>(param["default"]));
    }

    [ShellFact]
    public async Task AsksMatcForTheBackendsOfTheTarget()
    {
        var importer = new MaterialImporter(FakeMatc("echo x > \"$out\""));

        await importer.ImportAsync(Request(Source));
        await importer.ImportAsync(Request(Source, target: CookTargets.Android));

        var runs = Flags();
        Assert.StartsWith("-a metal -p all -o ", runs[0]);
        Assert.StartsWith("-a opengl -a vulkan -p mobile -o ", runs[1]);
    }

    [ShellFact]
    public void PutsTheMatcVersionInTheSettings()
    {
        var importer = new MaterialImporter(FakeMatc("true"));
        Assert.Equal("77", Assert.IsType<string>(importer.ResolveSettings(new ImportSettings())["matc"]));
    }

    [ShellFact]
    public async Task ReportsAMatcErrorAtTheLineTheAuthorWrote()
    {
        var importer = new MaterialImporter(FakeMatc("""
            echo "ERROR: 0:14: 'tinnt' : undeclared identifier" >&2
            echo "ERROR: 0:14: '' : compilation terminated" >&2
            exit 1
            """));

        // The material has an 10 line header, so its line 14 is the author's 4.
        var text = Source.Replace("tint.rgb", "tinnt.rgb", StringComparison.Ordinal);
        var failure = await Assert.ThrowsAsync<ImportFailureException>(() => importer.ImportAsync(Request(text)));

        Assert.StartsWith("materials/flat.oshader:4: ", failure.Reason);
        Assert.Contains("'tinnt' : undeclared identifier", failure.Reason);
        Assert.DoesNotContain("compilation terminated", failure.Reason);
    }

    [ShellFact]
    public async Task ReportsWhatTheTranslatorRefusesWithoutStartingMatc()
    {
        var importer = new MaterialImporter(FakeMatc("exit 1"));

        var failure = await Assert.ThrowsAsync<ImportFailureException>(
            () => importer.ImportAsync(Request("void vertex() {}\nvoid fragment() {}")));

        Assert.Equal("materials/flat.oshader:1: `vertex()` is not supported yet", failure.Reason);
        Assert.False(File.Exists(NativeWork.Inside(Work, "record")));
    }

    [ShellFact]
    public async Task ReadsIncludedFilesAndListsThemAsDependencies()
    {
        var importer = new MaterialImporter(FakeMatc("echo x > \"$out\""));
        var request = Request(
            "#include \"util.oshaderinc\"\nvoid fragment() { ROUGHNESS = half(1.0); }",
            files: new Dictionary<string, string> { ["materials/util.oshaderinc"] = "float half(float x) { return x * 0.5; }" });

        await importer.ImportAsync(request);
        var dependencies = await importer.DependenciesInAsync(request.Id, request.Bytes, new Dictionary<string, object?>(), request.Source);

        Assert.Equal([AssetId.Parse("materials/util.oshaderinc")], dependencies);
    }

    [ShellFact]
    public async Task AnIncludeInsideAnIncludeIsADependencyToo()
    {
        var importer = new MaterialImporter(FakeMatc("echo x > \"$out\""));
        var request = Request(
            "#include \"a.oshaderinc\"\nvoid fragment() { ROUGHNESS = half(1.0); }",
            files: new Dictionary<string, string>
            {
                ["materials/a.oshaderinc"] = "#include \"deep/b.oshaderinc\"",
                ["materials/deep/b.oshaderinc"] = "float half(float x) { return x; }",
            });

        var dependencies = await importer.DependenciesInAsync(request.Id, request.Bytes, new Dictionary<string, object?>(), request.Source);

        Assert.Equal(
            [AssetId.Parse("materials/a.oshaderinc"), AssetId.Parse("materials/deep/b.oshaderinc")],
            dependencies.OrderBy(id => id.ToString(), StringComparer.Ordinal));
        await importer.ImportAsync(request);
    }

    [ShellFact]
    public async Task AnIncludeNobodyWroteIsToldAtItsLineNotAsAMissingAsset()
    {
        var importer = new MaterialImporter(FakeMatc("exit 1"));
        var request = Request("#include \"gone.oshaderinc\"\nvoid fragment() {}");

        Assert.Empty(await importer.DependenciesInAsync(request.Id, request.Bytes, new Dictionary<string, object?>(), request.Source));
        var failure = await Assert.ThrowsAsync<ImportFailureException>(() => importer.ImportAsync(request));
        Assert.Equal("materials/flat.oshader:1: no file `materials/gone.oshaderinc` to include", failure.Reason);
    }

    [Fact(Skip = "no matc: set ORBLIT_MATC to run this", SkipUnless = nameof(HasMatc))]
    public async Task CooksWithTheRealMatc()
    {
        var importer = new MaterialImporter();

        var good = await importer.ImportAsync(Request(Source));
        Assert.True(good.Outputs["material.filamat"].Length > 1000);

        var text = Source.Replace("tint.rgb", "tinnt.rgb", StringComparison.Ordinal);
        var failure = await Assert.ThrowsAsync<ImportFailureException>(() => importer.ImportAsync(Request(text)));
        Assert.StartsWith("materials/flat.oshader:4: ", failure.Reason);
    }
}

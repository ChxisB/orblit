using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Orblit.Codegen.Tests;

/// <summary>Compiles C# source with the generator attached, the way a project build does.</summary>
internal sealed class Compiled
{
    private static readonly List<MetadataReference> References =
        [.. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path))];

    private Compiled(Compilation output, GeneratorDriverRunResult run, string path)
    {
        Output = output;
        Run = run;
        SourcePath = path;
    }

    public Compilation Output { get; }

    public GeneratorDriverRunResult Run { get; }

    public string SourcePath { get; }

    /// <summary>Errors from the user's code and from what was generated.</summary>
    public IEnumerable<Diagnostic> Errors => Output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);

    public IEnumerable<Diagnostic> Reported => Run.Diagnostics;

    /// <summary>The hint names of everything the generator wrote.</summary>
    public IEnumerable<string> Generated => Run.GeneratedTrees.Select(tree => Path.GetFileName(tree.FilePath));

    /// <summary>Source that names the attribute and the kinds without saying where they live.</summary>
    public static Compiled Of(string source, string assembly = "Game", string projectDir = "", string path = "lib/test.cs") =>
        Raw("using Orblit;\nusing Orblit.Core;\n" + source, assembly, projectDir, path);

    public static Compiled Raw(string code, string assembly = "Game", string projectDir = "", string path = "lib/test.cs")
    {
        var parse = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var compilation = CSharpCompilation.Create(
            assembly,
            [CSharpSyntaxTree.ParseText(code, parse, path)],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var driver = CSharpGeneratorDriver.Create(
            [new ComponentGenerator().AsSourceGenerator()],
            additionalTexts: null,
            parseOptions: parse,
            optionsProvider: new Options(projectDir));
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        return new Compiled(output, driver.GetRunResult(), path);
    }

    /// <summary>What the scanner makes of each marked type, without the generator's pipeline around it.</summary>
    public ScanResult Scan()
    {
        var components = new List<ComponentDeclaration>();
        var errors = new List<ComponentError>();
        foreach (var type in Output.Assembly.GlobalNamespace.GetTypeMembers())
        {
            var attribute = type.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == ComponentScanner.AttributeName);
            if (attribute is null)
            {
                continue;
            }

            var (component, error) = ComponentScanner.ScanType(type, attribute, SourcePath);
            if (component is not null)
            {
                components.Add(component);
            }

            if (error is not null)
            {
                errors.Add(error);
            }
        }

        return new ScanResult(components, errors);
    }

    /// <summary>The text the generator wrote under a hint name.</summary>
    public string Source(string hint) =>
        Run.GeneratedTrees.Single(tree => Path.GetFileName(tree.FilePath) == hint).ToString();

    public Assembly Load()
    {
        using var stream = new MemoryStream();
        var emitted = Output.Emit(stream);
        Assert.True(emitted.Success, string.Join("\n", emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        stream.Position = 0;
        return new AssemblyLoadContext(null, isCollectible: true).LoadFromStream(stream);
    }

    /// <summary>A named public type of the loaded assembly.</summary>
    public Type TypeOf(string name) => Load().GetType(name, throwOnError: true)!;

    private sealed class Options(string projectDir) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new Global(projectDir);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GlobalOptions;
    }

    private sealed class Global(string projectDir) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
        {
            value = key == "build_property.ProjectDir" ? projectDir : null;
            return value is not null;
        }
    }
}

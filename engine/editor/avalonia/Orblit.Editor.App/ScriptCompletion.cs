using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Orblit.Editor.App.Shell;

namespace Orblit.Editor.App;

/// <summary>The completions at one caret. The text from <see cref="Start"/> to the caret is what they are filtered by.</summary>
internal sealed class CompletionSet(Document document, CompletionService service, CompletionList list, int length)
{
    public int Start => list.Span.Start;

    /// <summary>The length of the text the completions were asked for.</summary>
    public int Length => length;

    public IReadOnlyList<CompletionItem> Items => list.ItemsList;

    /// <summary>What picking the item does to the text as it was asked: the span it replaces, the new text and the caret after.</summary>
    public Task<CompletionChange> ChangeOf(CompletionItem item) => service.GetChangeAsync(document, item);

    /// <summary>
    /// One line on what the item is: the summary of its documentation, or its signature when it has none. The
    /// signature is the first line of Roslyn's description and the summary follows it.
    /// </summary>
    public async Task<string> LineOf(CompletionItem item)
    {
        var lines = ((await service.GetDescriptionAsync(document, item))?.Text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.Length > 1 ? string.Join(" ", lines[1..]) : lines.FirstOrDefault() ?? "";
    }
}

/// <summary>
/// The project's C# in a Roslyn workspace, against the .NET runtime and the given assemblies. It answers the
/// completions at a caret and the errors in a text, for the script open in the Script workspace.
/// </summary>
// shortcut: the engine's source generators do not run, and no package a project adds is referenced. A member a
// generator writes, such as a signal's event, and a type from another package show as errors. Load the project's
// own build when scripts lean on either.
internal sealed class ScriptCompletion
{
    // The runtime and the engine's API are tens of megabytes, and a new workspace comes with every script opened.
    // shortcut: an assembly rebuilt while the editor runs keeps its old members until a restart. Key by the file's
    // time when engine work in the editor needs it.
    static readonly ConcurrentDictionary<string, MetadataReference> s_references = new();
    readonly ProjectId _project = ProjectId.CreateNewId();
    readonly Dictionary<string, DocumentId> _documents;
    readonly Lazy<Solution> _solution;

    // The solution is built on the first ask, which runs off the UI thread, because it reads the references.
    ScriptCompletion(Dictionary<string, string> scripts, IReadOnlyList<string> references)
    {
        _documents = scripts.Keys.ToDictionary(path => path, path => DocumentId.CreateNewId(_project, path));
        _solution = new Lazy<Solution>(() => Build(scripts, references));
    }

    /// <summary>Reads every script of the project. A script that cannot be read is left out.</summary>
    public static ScriptCompletion For(IScriptFiles files)
    {
        // The runtime's own folder, because the editor's trusted list also holds the editor's packages.
        var runtime = Path.GetDirectoryName(typeof(object).Assembly.Location);
        var trusted = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "").Split(Path.PathSeparator);
        Dictionary<string, string> scripts = [];
        foreach (var path in files.Scripts())
        {
            try
            {
                scripts[path] = files.ReadScript(path);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // A file deleted or locked since the list was made has nothing to offer.
            }
        }

        // The trusted list can name a file twice, and Roslyn refuses a reference it is given twice.
        return new ScriptCompletion(scripts, [.. trusted.Where(path => Path.GetDirectoryName(path) == runtime).Concat(files.ScriptReferences()).Distinct()]);
    }

    Solution Build(Dictionary<string, string> scripts, IReadOnlyList<string> references)
    {
        var info = ProjectInfo.Create(_project, VersionStamp.Create(), "Scripts", "Scripts", LanguageNames.CSharp,
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable),
            parseOptions: new CSharpParseOptions(LanguageVersion.Latest),
            metadataReferences: references.Select(path => s_references.GetOrAdd(path, Reference)));
        var solution = new AdhocWorkspace().CurrentSolution.AddProject(info);
        foreach (var (path, text) in scripts)
        {
            solution = solution.AddDocument(_documents[path], path, SourceText.From(text));
        }

        return solution;
    }

    // The documentation beside an assembly gives its members their summaries. The runtime's folder ships none.
    static MetadataReference Reference(string path)
    {
        var xml = Path.ChangeExtension(path, ".xml");
        return MetadataReference.CreateFromFile(path, documentation: File.Exists(xml) ? XmlDocumentationProvider.CreateFromFile(xml) : null);
    }

    /// <summary>The completions at the caret in the script's text, or none when Roslyn offers nothing there.</summary>
    public async Task<CompletionSet?> Complete(string path, string text, int caret)
    {
        var document = With(path, text);
        // Every C# document has the service, because the workspace loads the C# features.
        var service = CompletionService.GetService(document)!;
        var list = await service.GetCompletionsAsync(document, caret);
        return list.ItemsList.Count == 0 ? null : new CompletionSet(document, service, list, text.Length);
    }
    /// <summary>The errors in the script's text, with lines and columns counted from one.</summary>
    public async Task<IReadOnlyList<ScriptProblem>> Problems(string path, string text)
    {
        var model = await With(path, text).GetSemanticModelAsync();
        return [.. model!.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic =>
            {
                var at = diagnostic.Location.GetLineSpan().StartLinePosition;
                return new ScriptProblem(path, at.Line + 1, at.Character + 1, true, $"{diagnostic.Id}: {diagnostic.GetMessage(CultureInfo.InvariantCulture)}");
            })];
    }

    // The open script's text stands in for what was read. A script the list did not hold is added.
    Document With(string path, string text)
    {
        if (_documents.TryGetValue(path, out var id))
        {
            return _solution.Value.WithDocumentText(id, SourceText.From(text)).GetDocument(id)!;
        }

        var added = DocumentId.CreateNewId(_project, path);
        return _solution.Value.AddDocument(added, path, SourceText.From(text)).GetDocument(added)!;
    }
}

using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Orblit.Editor.App.Shell;

namespace Orblit.Editor.App;

/// <summary>The completions at one caret. The text from <see cref="Start"/> to the caret is what they are filtered by.</summary>
internal sealed class CompletionSet(Document document, CompletionService service, CompletionList list)
{
    public int Start => list.Span.Start;

    public IReadOnlyList<CompletionItem> Items => list.ItemsList;

    /// <summary>The text that replaces the typed part when the item is picked, from <see cref="Start"/>.</summary>
    public async Task<string> TextOf(CompletionItem item) => (await service.GetChangeAsync(document, item)).TextChange.NewText ?? item.DisplayText;
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
    readonly Solution _solution;
    readonly ProjectId _project;
    readonly Dictionary<string, DocumentId> _documents;

    ScriptCompletion(Solution solution, ProjectId project, Dictionary<string, DocumentId> documents)
    {
        _solution = solution;
        _project = project;
        _documents = documents;
    }

    /// <summary>Reads every script of the project. A script that cannot be read is left out.</summary>
    public static ScriptCompletion For(IScriptFiles files)
    {
        // The runtime's own folder, because the editor's trusted list also holds the editor's packages.
        var runtime = Path.GetDirectoryName(typeof(object).Assembly.Location);
        var trusted = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "").Split(Path.PathSeparator);
        var references = trusted.Where(path => Path.GetDirectoryName(path) == runtime).Concat(files.ScriptReferences())
            .Select(path => MetadataReference.CreateFromFile(path));
        var project = ProjectId.CreateNewId();
        var info = ProjectInfo.Create(project, VersionStamp.Create(), "Scripts", "Scripts", LanguageNames.CSharp,
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable),
            parseOptions: new CSharpParseOptions(LanguageVersion.Latest),
            metadataReferences: references);
        var solution = new AdhocWorkspace().CurrentSolution.AddProject(info);
        Dictionary<string, DocumentId> documents = [];
        foreach (var path in files.Scripts())
        {
            try
            {
                var id = DocumentId.CreateNewId(project, path);
                solution = solution.AddDocument(id, path, SourceText.From(files.ReadScript(path)));
                documents[path] = id;
            }
            catch (IOException)
            {
                // A file deleted since the list was made has nothing to offer.
            }
        }

        return new ScriptCompletion(solution, project, documents);
    }

    /// <summary>The completions at the caret in the script's text, or none when Roslyn offers nothing there.</summary>
    public async Task<CompletionSet?> Complete(string path, string text, int caret)
    {
        var document = With(path, text);
        // Every C# document has the service, because the workspace loads the C# features.
        var service = CompletionService.GetService(document)!;
        var list = await service.GetCompletionsAsync(document, caret);
        return list.ItemsList.Count == 0 ? null : new CompletionSet(document, service, list);
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
            return _solution.WithDocumentText(id, SourceText.From(text)).GetDocument(id)!;
        }

        var added = DocumentId.CreateNewId(_project, path);
        return _solution.AddDocument(added, path, SourceText.From(text)).GetDocument(added)!;
    }
}

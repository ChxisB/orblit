using Avalonia.Controls;
using Avalonia.Input;
using Orblit.Editor.App.Graph;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// One graph script open in the Graph workspace: its file, its side panels, the canvas of the function on show and
/// the card over it that reads the graph out. Disposing it lets the engine drop its copy, so an unsaved edit is gone.
/// </summary>
internal sealed class GraphSheet : IDisposable
{
    readonly SheetShared _shared;

    public GraphSheet(string script, IGraphFile graph, SheetShared shared)
    {
        Script = script;
        Graph = graph;
        _shared = shared;
        Panels = new ScriptPanels(graph);
        Inspector = new StepInspector(graph, () => Canvas?.AddStep());
        graph.Changed += () =>
        {
            IsEdited = true;
            Edited?.Invoke();
        };
        // The canvas shows the function it is given when it is made, so another function needs a new canvas.
        Panels.FunctionOpened += _ => ShowCanvas();
        Panels.StepRevealed += id => Canvas?.Reveal(id);
        Sentences = new SentencesCard(graph);
        Sentences.StepPicked += Panels.Reveal;
        ShowCanvas();
    }

    /// <summary>Raised when the graph changes.</summary>
    public event Action? Edited;

    /// <summary>The res:// path of the file.</summary>
    public string Script { get; }

    public IGraphFile Graph { get; }

    public ScriptPanels Panels { get; }

    public StepInspector Inspector { get; }

    /// <summary>The graph read out as sentences, on a card over the canvas.</summary>
    public SentencesCard Sentences { get; }

    /// <summary>Holds the canvas, which is replaced when another function opens.</summary>
    public Border Host { get; } = new();

    public GraphCanvas? Canvas => Host.Child as GraphCanvas;

    /// <summary>Whether the graph differs from the file on disk.</summary>
    public bool IsEdited { get; private set; }

    /// <summary>Writes the graph to its file. Throws <see cref="IOException"/> when it cannot be written.</summary>
    public void Save()
    {
        Graph.Save();
        IsEdited = false;
    }

    /// <summary>Counts the graph as saved, for a close that drops the edits.</summary>
    public void MarkSaved() => IsEdited = false;

    public void ShowStop(DebugPlace? place) => Canvas?.ShowStop(place);

    /// <summary>The title of a step in the function on show, or null when the place is in another script or function.</summary>
    public string? StepTitle(DebugPlace place) =>
        place.Script == Script && Graph.Function?.Name == place.Function ? Canvas?.Scene.Find(place.Step)?.Title : null;

    public void Dispose() => Graph.Dispose();

    void ShowCanvas()
    {
        var canvas = new GraphCanvas(Graph, _shared.Command);
        canvas.Scene.Script = Script;
        canvas.Scene.Breakpoints = _shared.Breakpoints;
        canvas.Reload();
        canvas.Probe(_shared.Debugger);
        canvas.ShowStop(_shared.Debugger.Current);
        canvas.SelectionChanged += Inspector.Show;
        // The card outlives each canvas, and a control has one parent at a time.
        (Sentences.Parent as Panel)?.Children.Remove(Sentences);
        canvas.Carry(Sentences);
        Host.Child = canvas;
        // A new canvas has nothing selected, so the inspector is cleared with it.
        Inspector.Show(null);
    }
}

/// <summary>What every open graph uses: the breakpoints, the debugger and the platform's command key.</summary>
internal sealed record SheetShared(GraphBreakpoints Breakpoints, GraphDebugger Debugger, KeyModifiers Command);

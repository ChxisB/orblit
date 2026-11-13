using Avalonia.Controls;
using Orblit.Editor.App.Graph;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// One graph script open in the Graph workspace: its file, its side panels and the canvas of the function on show.
/// Disposing it lets the engine drop its copy, so an unsaved edit is gone.
/// </summary>
internal sealed class GraphSheet : IDisposable
{
    readonly GraphBreakpoints _breakpoints;
    readonly GraphDebugger _debugger;

    public GraphSheet(string script, IGraphFile graph, GraphBreakpoints breakpoints, GraphDebugger debugger)
    {
        Script = script;
        Graph = graph;
        _breakpoints = breakpoints;
        _debugger = debugger;
        Panels = new ScriptPanels(graph);
        Inspector = new StepInspector(graph);
        graph.Changed += () =>
        {
            IsEdited = true;
            Edited?.Invoke();
        };
        // The canvas shows the function it is given when it is made, so another function needs a new canvas.
        Panels.FunctionOpened += _ => ShowCanvas();
        Panels.StepRevealed += id => Canvas?.Reveal(id);
        ShowCanvas();
    }

    /// <summary>Raised when the graph changes.</summary>
    public event Action? Edited;

    /// <summary>The res:// path of the file.</summary>
    public string Script { get; }

    public IGraphFile Graph { get; }

    public ScriptPanels Panels { get; }

    public StepInspector Inspector { get; }

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

    public void Dispose() => Graph.Dispose();

    void ShowCanvas()
    {
        var canvas = new GraphCanvas(Graph);
        canvas.Scene.Script = Script;
        canvas.Scene.Breakpoints = _breakpoints;
        canvas.Reload();
        canvas.Probe(_debugger);
        canvas.ShowStop(_debugger.Current);
        canvas.SelectionChanged += Inspector.Show;
        Host.Child = canvas;
        // A new canvas has nothing selected, so the inspector is cleared with it.
        Inspector.Show(null);
    }
}

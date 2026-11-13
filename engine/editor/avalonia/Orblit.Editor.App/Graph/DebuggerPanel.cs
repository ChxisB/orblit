using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>
/// What a debug session shows: the state of the game, the buttons that move it on, the stack with the selected level
/// marked, and the locals and members of that level. It is built again from the debugger after every change.
/// </summary>
internal sealed class DebuggerPanel : StackPanel
{
    static readonly (string Id, string Label, DebugCommand Command)[] s_buttons =
    [
        ("pause", "Pause", DebugCommand.Break),
        ("continue", "Continue", DebugCommand.Continue),
        ("step", "Step into", DebugCommand.Step),
        ("next", "Step over", DebugCommand.Next),
        ("out", "Step out", DebugCommand.Out),
    ];

    readonly GraphDebugger _debugger;

    public DebuggerPanel(GraphDebugger debugger)
    {
        _debugger = debugger;
        Spacing = 4;
        _debugger.Changed += Rebuild;
        Rebuild();
    }

    /// <summary>The control with this id, such as <c>debugger:frame:0</c>. Null when the panel has none.</summary>
    internal T? Find<T>(string id) where T : Control => this.GetVisualDescendants().OfType<T>().FirstOrDefault(control => (control.Tag as string) == id);

    void Rebuild()
    {
        var state = _debugger.State;
        Children.Clear();
        Children.Add(Line("debugger:status", Describe(state)));
        Children.Add(Buttons());
        AddStack(state);
        AddValues("Locals", "local", state.Locals);
        AddValues("Members", "member", state.Members);
    }

    static string Describe(DebugState state) =>
        !state.Connected ? "No game is connected."
        : state.Stopped ? $"Stopped: {state.Reason}."
        : "The game is running.";

    WrapPanel Buttons()
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var (id, label, command) in s_buttons)
        {
            var button = PanelFields.Small($"debugger:{id}", label, () => _debugger.Send(command));
            button.Margin = new Thickness(0, 0, 4, 4);
            button.IsEnabled = command == DebugCommand.Break ? _debugger.CanPause : _debugger.CanResume;
            row.Children.Add(button);
        }

        return row;
    }

    void AddStack(DebugState state)
    {
        if (!state.Stopped)
        {
            return;
        }

        Children.Add(new TextBlock { Text = "Stack", FontWeight = FontWeight.Bold, Margin = new Thickness(0, 6, 0, 0) });
        for (var at = 0; at < state.Stack.Count; at++)
        {
            var level = at;
            var button = PanelFields.Small($"debugger:frame:{at}", FrameText(state.Stack[at], at == state.Level), () => _debugger.SelectLevel(level));
            button.HorizontalAlignment = HorizontalAlignment.Left;
            Children.Add(button);
        }
    }

    static string FrameText(DebugFrame frame, bool selected)
    {
        var place = GraphBreakpoints.Split(frame.Source) is not null ? $"{frame.Function}, step {frame.Line}" : $"{frame.Function}, {frame.Source} line {frame.Line}";
        return selected ? $"> {place}" : place;
    }

    void AddValues(string title, string kind, IReadOnlyList<DebugValue> values)
    {
        if (values.Count == 0)
        {
            return;
        }

        Children.Add(new TextBlock { Text = title, FontWeight = FontWeight.Bold, Margin = new Thickness(0, 6, 0, 0) });
        foreach (var value in values)
        {
            Children.Add(Line($"debugger:{kind}:{value.Name}", $"{value.Name} = {value.Value}"));
        }
    }

    static TextBlock Line(string id, string text) => new() { Text = text, Tag = id, FontSize = 12, TextWrapping = TextWrapping.Wrap };
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>
/// What a debug session shows: a bar with the state of the game and the buttons that move it on, then while the
/// game is stopped the stack with the selected level marked, and the locals and members of that level. It is
/// built again from the debugger after every change.
/// </summary>
internal sealed class DebuggerPanel : StackPanel
{
    static readonly (string Id, string Icon, string Tip, DebugCommand Command)[] s_buttons =
    [
        ("pause", "Pause", "Pause", DebugCommand.Break),
        ("continue", "DebugContinue", "Continue", DebugCommand.Continue),
        ("step", "DebugStep", "Step into", DebugCommand.Step),
        ("next", "DebugNext", "Step over", DebugCommand.Next),
        ("out", "DebugOut", "Step out", DebugCommand.Out),
    ];

    readonly GraphDebugger _debugger;
    readonly Func<DebugPlace, string?> _stepTitle;
    readonly StackPanel _details = new() { Spacing = 4, Margin = new Thickness(12, 0, 12, 10) };

    /// <summary>The step title gives the name of a step on show, or null when the canvas does not show it.</summary>
    public DebuggerPanel(GraphDebugger debugger, Func<DebugPlace, string?> stepTitle)
    {
        _debugger = debugger;
        _stepTitle = stepTitle;
        _debugger.Changed += Rebuild;
        Rebuild();
    }

    /// <summary>The control with this id, such as <c>debugger:frame:0</c>. Null when the panel has none.</summary>
    internal T? Find<T>(string id) where T : Control => this.GetVisualDescendants().OfType<T>().FirstOrDefault(control => (control.Tag as string) == id);

    void Rebuild()
    {
        var state = _debugger.State;
        Children.Clear();
        _details.Children.Clear();
        Children.Add(Bar(state));
        AddStack(state);
        AddValues("Locals", "local", state.Locals);
        AddValues("Members", "member", state.Members);
        if (_details.Children.Count > 0)
        {
            Children.Add(_details);
        }
    }

    // The game pauses at the top of the stack, whichever level the person has selected.
    string Describe(DebugState state) =>
        !state.Connected ? "No game is connected."
        : !state.Stopped ? "The game is running."
        : _debugger.PlaceAt(0) is { } place && _stepTitle(place) is { } title ? $"Paused at {title}."
        : $"Paused: {state.Reason}.";

    Grid Bar(DebugState state)
    {
        var bar = new Grid { Height = 44, Margin = new Thickness(12, 0, 8, 0), ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        var icon = Parts.RowIcon("Debug", EditorBrushes.TextDim);
        var status = new TextBlock
        {
            Text = Describe(state),
            Tag = "debugger:status",
            FontSize = 12,
            Foreground = state.Stopped ? EditorBrushes.Text : EditorBrushes.TextDim,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        foreach (var (id, name, tip, command) in s_buttons)
        {
            var button = Parts.IconButton(name, tip);
            button.Tag = $"debugger:{id}";
            button.IsEnabled = command == DebugCommand.Break ? _debugger.CanPause : _debugger.CanResume;
            button.Click += (_, _) => _debugger.Send(command);
            buttons.Children.Add(button);
        }
        Grid.SetColumn(status, 1);
        Grid.SetColumn(buttons, 2);
        bar.Children.AddRange([icon, status, buttons]);
        return bar;
    }

    void AddStack(DebugState state)
    {
        if (!state.Stopped)
        {
            return;
        }

        _details.Children.Add(Heading("Stack"));
        for (var at = 0; at < state.Stack.Count; at++)
        {
            var level = at;
            var button = PanelFields.Small($"debugger:frame:{at}", FrameText(state.Stack[at], at == state.Level), () => _debugger.SelectLevel(level));
            button.HorizontalAlignment = HorizontalAlignment.Left;
            _details.Children.Add(button);
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

        _details.Children.Add(Heading(title));
        foreach (var value in values)
        {
            _details.Children.Add(Line($"debugger:{kind}:{value.Name}", $"{value.Name} = {value.Value}"));
        }
    }

    static TextBlock Heading(string text) => new() { Text = text, FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = EditorBrushes.TextDim, Margin = new Thickness(0, 6, 0, 0) };

    static TextBlock Line(string id, string text) => new() { Text = text, Tag = id, FontSize = 12, TextWrapping = TextWrapping.Wrap };
}

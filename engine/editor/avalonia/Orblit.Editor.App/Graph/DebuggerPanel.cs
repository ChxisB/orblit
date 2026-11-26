using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>
/// What a debug session shows: a bar with the state of the game and the icon buttons that move it on, then while
/// the game is stopped the stack with the selected level marked, and the locals and members of that level. With no
/// game, a line says how to start one. A button that is off says why on its card. It is built again from the
/// debugger after every change.
/// </summary>
internal sealed class DebuggerPanel : StackPanel
{
    readonly GraphDebugger _debugger;
    readonly Func<DebugPlace, string?> _stepTitle;
    readonly string _playKey;
    readonly (string Id, string Icon, string Word, string Does, string Key, DebugCommand Command)[] _buttons;
    readonly StackPanel _details = new() { Spacing = Gaps.Tight, Margin = new Thickness(Gaps.Group, 0, Gaps.Group, Gaps.Group) };

    /// <summary>The step title gives the name of a step on show, or null when the canvas does not show it.</summary>
    public DebuggerPanel(GraphDebugger debugger, Func<DebugPlace, string?> stepTitle, KeyModifiers command)
    {
        _debugger = debugger;
        _stepTitle = stepTitle;
        _playKey = Parts.KeyText(new KeyGesture(Key.B, command));
        _buttons = Buttons(command);
        _debugger.Changed += Rebuild;
        Rebuild();
    }

    // A list of cases, so it is data. The keys are the ones the window gives each button.
    static (string Id, string Icon, string Word, string Does, string Key, DebugCommand Command)[] Buttons(KeyModifiers command) =>
    [
        ("pause", "Pause", "Pause", "Stops the game at the next step it runs.", Parts.KeyText(new KeyGesture(Key.Y, command)), DebugCommand.Break),
        ("continue", "DebugContinue", "Carry on", "Lets the game run until a step marked Pause here.", "F12", DebugCommand.Continue),
        ("next", "DebugNext", "Next step", "Runs this step and stops at the next one.", "F10", DebugCommand.Next),
        ("step", "DebugStep", "Go into", "Goes into the action this step runs and stops at its first step.", "F11", DebugCommand.Step),
        ("out", "DebugOut", "Come out", "Finishes this action and stops where it was run from.", "⇧F11", DebugCommand.Out),
    ];

    /// <summary>The control with this id, such as <c>debugger:frame:0</c>. Null when the panel has none.</summary>
    internal T? Find<T>(string id) where T : Control => this.GetVisualDescendants().OfType<T>().FirstOrDefault(control => (control.Tag as string) == id);

    void Rebuild()
    {
        var state = _debugger.State;
        Children.Clear();
        _details.Children.Clear();
        Children.Add(Bar(state));
        if (!state.Connected)
        {
            _details.Children.Add(Line("debugger:hint", $"Nothing is playing. Press {_playKey}, and these step through the graph one step at a time.", EditorBrushes.TextDim));
        }

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
        !state.Connected ? "The game is not running"
        : !state.Stopped ? "The game is running."
        : _debugger.PlaceAt(0) is { } place && _stepTitle(place) is { } title ? $"Paused at step {place.Step}, {title}."
        : $"Paused: {state.Reason}.";

    string WhyOff(DebugState state, DebugCommand command) =>
        !state.Connected ? $"Nothing is playing. Press {_playKey} first."
        : command == DebugCommand.Break ? "The game is paused already."
        : "Pause the game first.";

    Grid Bar(DebugState state)
    {
        var bar = new Grid { Height = 44, Margin = new Thickness(Gaps.Group, 0, Gaps.Item, 0), ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
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
        ToolTip.SetTip(status, status.Text);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Tight, VerticalAlignment = VerticalAlignment.Center };
        foreach (var (id, mark, word, does, key, command) in _buttons)
        {
            var card = new HoverCard(word, does, key);
            var button = new Button
            {
                Tag = $"debugger:{id}",
                IsEnabled = command == DebugCommand.Break ? _debugger.CanPause : _debugger.CanResume,
            };
            card.SaysWhyOff(Parts.Dress(button, mark, card), WhyOff(state, command));
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

    static TextBlock Heading(string text) => new() { Text = text, FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = EditorBrushes.TextDim, Margin = new Thickness(0, Gaps.Item, 0, 0) };

    static TextBlock Line(string id, string text, IBrush? brush = null) => new() { Text = text, Tag = id, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = brush };
}

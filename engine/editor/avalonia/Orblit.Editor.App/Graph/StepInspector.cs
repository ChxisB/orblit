using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>
/// The selected step: the value of each input nothing is wired to, the other parameters it holds, and what each
/// value pin offers (see <see cref="PinEdits"/>). An edit the engine refuses is shown as the reason it gave.
/// </summary>
internal sealed class StepInspector : Border
{
    const string NoStep = "No step selected.";

    // The pin count is set on the canvas and the engine refuses it as a parameter. The Entry step's signature has its own panel.
    static readonly HashSet<string> s_hidden = ["pins"];
    static readonly HashSet<string> s_signature = ["arguments", "locals", "returns", "description"];

    readonly IGraphFile _file;
    readonly PinEdits _edits;
    readonly StackPanel _body = new() { Spacing = 6, Margin = new Thickness(10) };
    readonly TextBlock _heading = new() { FontWeight = FontWeight.Bold };
    readonly TextBlock _notice = new() { Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap, IsVisible = false, FontSize = 12 };
    int? _stepId;

    public StepInspector(IGraphFile file)
    {
        _file = file;
        _edits = new PinEdits(file);
        Background = EditorBrushes.Panel;
        BorderThickness = new Thickness(0, 1, 0, 0);
        BorderBrush = EditorBrushes.TabInactive;
        Child = new ScrollViewer { Content = _body, MaxHeight = 280, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        _file.Changed += Rebuild;
        Rebuild();
    }

    /// <summary>The kind of the step on show, or a hint when none is.</summary>
    internal string Heading => _heading.Text ?? "";

    /// <summary>The reason the engine gave for the last edit it refused, or empty after an edit it took.</summary>
    internal string Notice => _notice.IsVisible ? _notice.Text ?? "" : "";

    /// <summary>The control with this id, such as <c>input:1:value</c>. Null when the inspector has none.</summary>
    internal T? Find<T>(string id) where T : Control => _body.GetVisualDescendants().OfType<T>().FirstOrDefault(control => (control.Tag as string) == id);

    /// <summary>Shows the step with this id. Null, or an id the function does not have, shows nothing.</summary>
    public void Show(int? stepId)
    {
        _stepId = stepId;
        _notice.IsVisible = false;
        Rebuild();
    }

    void Rebuild()
    {
        _body.Children.Clear();
        var function = _file.Function;
        var step = function?.Steps.FirstOrDefault(found => found.Id == _stepId);
        _heading.Text = step?.Kind ?? NoStep;
        _body.Children.Add(_heading);
        _body.Children.Add(_notice);
        if (function is null || step is null)
        {
            return;
        }

        AddParameters(step);
        AddPins(step, function.Wires);
    }

    // A refused result leaves nothing changed and raises no event, so the inspector goes back to the document itself.
    void Apply(GraphEditResult result)
    {
        _notice.IsVisible = !result.Ok;
        _notice.Text = result.Reason;
        if (!result.Ok)
        {
            Rebuild();
        }
    }

    // shortcut: a parameter nobody has set yet is not listed, because the document holds only the parameters
    // that were set. List the ones a kind takes when the engine says which they are.
    void AddParameters(GraphStep step)
    {
        var held = step.Inputs.Select(port => port.Parameter).ToHashSet();
        foreach (var (name, text) in step.Parameters)
        {
            if (held.Contains(name) || s_hidden.Contains(name) || (step.Kind == "entry" && s_signature.Contains(name)))
            {
                continue;
            }

            _body.Children.Add(Labeled(name, PanelFields.Box($"parameter:{name}", text, value => Apply(_file.SetStepParameter(step.Id, name, value)))));
        }
    }

    void AddPins(GraphStep step, IReadOnlyList<GraphWire> wires)
    {
        for (var at = 0; at < step.Inputs.Count; at++)
        {
            if (step.Inputs[at].Sort == GraphPortSort.Data)
            {
                _body.Children.Add(InputRow(new Pin(step, true, at), wires.Any(wire => wire.ToStep == step.Id && wire.ToPort == at)));
            }
        }

        for (var at = 0; at < step.Outputs.Count; at++)
        {
            if (step.Outputs[at].Sort == GraphPortSort.Data)
            {
                var pin = new Pin(step, false, at);
                _body.Children.Add(PinRow(pin, Buttons($"output:{at}", pin)));
            }
        }
    }

    Control InputRow(Pin pin, bool wired)
    {
        var id = $"input:{pin.Index}";
        if (wired)
        {
            return PinRow(pin, new TextBlock { Text = "Wired.", Tag = $"{id}:wired", FontSize = 12 });
        }

        return PinRow(pin, pin.Port.Parameter == "" ? Buttons(id, pin) : ValueAndButtons(id, pin));
    }

    Control ValueAndButtons(string id, Pin pin)
    {
        var step = pin.Step;
        var parameter = pin.Port.Parameter;
        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(PanelFields.Box($"{id}:value", step.Parameters.GetValueOrDefault(parameter, ""), text => Apply(_file.SetStepParameter(step.Id, parameter, text))));
        stack.Children.Add(Buttons(id, pin));
        return stack;
    }

    static StackPanel PinRow(Pin pin, Control body)
    {
        var port = pin.Port;
        var row = new StackPanel { Spacing = 2 };
        row.Children.Add(new TextBlock { Text = $"{port.Name} ({VariantTypes.NameOf(port.Type)})", FontSize = 12 });
        row.Children.Add(body);
        return row;
    }

    static StackPanel Labeled(string label, Control box)
    {
        var row = new StackPanel { Spacing = 2 };
        row.Children.Add(new TextBlock { Text = label, FontSize = 12 });
        row.Children.Add(box);
        return row;
    }

    // Reset needs a value on an input to put back. Split and combine need a type that has parts.
    WrapPanel Buttons(string id, Pin pin)
    {
        var buttons = new WrapPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
        var port = pin.Port;
        if (pin.IsInput && port.Parameter != "" && VariantText.ZeroOf(port.Type) is not null)
        {
            buttons.Children.Add(PanelFields.Small($"{id}:reset", "Reset", () => Apply(_edits.Reset(pin))));
        }

        buttons.Children.Add(PanelFields.Small($"{id}:variable", "Variable", () => Apply(_edits.Promote(pin, false))));
        buttons.Children.Add(PanelFields.Small($"{id}:local", "Local", () => Apply(_edits.Promote(pin, true))));
        if (VariantTypes.PartsOf(port.Type) > 0)
        {
            buttons.Children.Add(pin.IsInput
                ? PanelFields.Small($"{id}:combine", "Combine", () => Apply(_edits.Combine(pin)))
                : PanelFields.Small($"{id}:split", "Split", () => Apply(_edits.Split(pin))));
        }

        return buttons;
    }
}

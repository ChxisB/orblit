using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>
/// The picked step: its number, its plain name with the engine's name beside it, and what it does. It gets lists the
/// value of each input nothing is wired to, with where the value comes from, and the other parameters the step
/// holds. It gives lists the value outputs (see <see cref="PinEdits"/>). An edit the engine refuses is shown as the
/// reason it gave.
/// </summary>
internal sealed class StepInspector : Border
{
    const string NoStep = "No step picked";

    // The pin count is set on the canvas and the engine refuses it as a parameter. The Entry step's signature has its own panel.
    static readonly HashSet<string> s_hidden = ["pins"];
    static readonly HashSet<string> s_signature = ["arguments", "locals", "returns", "description"];

    readonly IGraphFile _file;
    readonly PinEdits _edits;
    readonly Button _addStep;
    readonly StackPanel _body = new() { Spacing = Gaps.Item, Margin = new Thickness(Gaps.Group) };
    readonly TextBlock _number = new() { FontSize = 11, Foreground = EditorBrushes.TextDim };
    readonly TextBlock _heading = new() { FontSize = 15, FontWeight = FontWeight.SemiBold };
    readonly TextBlock _engine = new() { FontFamily = EditorTheme.Mono, FontSize = 10.5, Foreground = EditorBrushes.Unit, VerticalAlignment = VerticalAlignment.Center };
    readonly StackPanel _title;
    readonly TextBlock _about = new() { FontSize = 12, Foreground = EditorBrushes.TextSoft, TextWrapping = TextWrapping.Wrap };
    readonly TextBlock _notice = new() { Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap, IsVisible = false, FontSize = 12 };
    int? _stepId;

    /// <summary>Add a step opens the list of steps on the canvas on show.</summary>
    public StepInspector(IGraphFile file, Action addStep)
    {
        _file = file;
        _edits = new PinEdits(file);
        _title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Item, Children = { _heading, _engine } };
        _addStep = Tool("inspector:add_step", new HoverCard("Add a step", "Opens the list of steps to add to the graph.", "⇧A"), addStep);
        _addStep.HorizontalAlignment = HorizontalAlignment.Center;
        Child = new ScrollViewer { Content = _body, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        _file.Changed += Rebuild;
        Rebuild();
    }

    /// <summary>The plain name of the step on show, or a hint when none is.</summary>
    internal string Heading => _heading.Text ?? "";

    /// <summary>The reason the engine gave for the last edit it refused, or empty after an edit it took.</summary>
    internal string Notice => _notice.IsVisible ? _notice.Text ?? "" : "";

    /// <summary>The control with this id, such as <c>input:1:value</c>. Null when the inspector has none.</summary>
    internal T? Find<T>(string id) where T : Control => _body.GetVisualDescendants().OfType<T>().FirstOrDefault(control => (control.Tag as string) == id);

    /// <summary>What the inspector shows with no step picked.</summary>
    internal static Control Empty() => Parts.EmptyState("GraphNode", NoStep, "Pick a step to see what goes in and where it comes from.");

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
        if (function is null || step is null)
        {
            _heading.Text = NoStep;
            _body.Children.AddRange([Empty(), _addStep]);
            return;
        }

        ShowName(step);
        _body.Children.AddRange([_number, _title, _about, _notice]);
        AddSection("It gets", [.. Parameters(step), .. Inputs(step, function.Wires)]);
        AddSection("It gives", [.. Outputs(step)]);
    }

    // A step with no plain row goes by its kind, so the engine name would only say it again.
    void ShowName(GraphStep step)
    {
        var name = StepNames.OfKind(step.Kind);
        _number.Text = $"Step {step.Id}";
        _heading.Text = StepNames.NameOf(step.Kind);
        _engine.Text = step.Kind;
        _engine.IsVisible = name is not null;
        _about.Text = name is null ? "" : $"{name.Line} Good for {name.GoodFor}";
        _about.IsVisible = name is not null;
    }

    void AddSection(string title, Control[] rows)
    {
        if (rows.Length == 0)
        {
            return;
        }

        _body.Children.Add(new TextBlock { Text = title, FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = EditorBrushes.TextDim, Margin = new Thickness(0, Gaps.Item, 0, 0) });
        _body.Children.AddRange(rows);
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
    IEnumerable<Control> Parameters(GraphStep step)
    {
        var held = step.Inputs.Select(port => port.Parameter).ToHashSet();
        foreach (var (name, text) in step.Parameters)
        {
            if (held.Contains(name) || s_hidden.Contains(name) || (step.Kind == "entry" && s_signature.Contains(name)))
            {
                continue;
            }

            yield return Labeled(name, PanelFields.Box($"parameter:{name}", text, value => Apply(_file.SetStepParameter(step.Id, name, value))));
        }
    }

    IEnumerable<Control> Inputs(GraphStep step, IReadOnlyList<GraphWire> wires) =>
        Enumerable.Range(0, step.Inputs.Count)
            .Where(at => step.Inputs[at].Sort == GraphPortSort.Data)
            .Select(at => InputRow(new Pin(step, true, at), wires.Any(wire => wire.ToStep == step.Id && wire.ToPort == at)));

    IEnumerable<Control> Outputs(GraphStep step) =>
        Enumerable.Range(0, step.Outputs.Count)
            .Where(at => step.Outputs[at].Sort == GraphPortSort.Data)
            .Select(at => OutputRow(new Pin(step, false, at)));

    Control InputRow(Pin pin, bool wired)
    {
        var id = $"input:{pin.Index}";
        if (wired)
        {
            return PinRow(pin, new TextBlock { Text = "Comes from a wire.", Tag = $"{id}:wired", FontSize = 12, Foreground = EditorBrushes.TextDim });
        }

        var typed = pin.Port.Parameter != "";
        var rows = new List<Control>();
        if (typed)
        {
            rows.Add(ValueRow(id, pin));
        }

        rows.Add(Source(id, pin, typed ? "Typed here" : "Nothing yet"));
        if (VariantTypes.PartsOf(pin.Port.Type) > 0)
        {
            rows.Add(Tool($"{id}:combine", new HoverCard("Build from parts", "Adds a step that takes each part, like x, y and z, and builds the value here."), () => Apply(_edits.Combine(pin))));
        }

        return PinRow(pin, [.. rows]);
    }

    Control OutputRow(Pin pin)
    {
        var id = $"output:{pin.Index}";
        var buttons = new WrapPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(Tool($"{id}:variable", new HoverCard("Remember it", "Makes a remembered value and stores this in it. The whole script can use it."), () => Apply(_edits.Promote(pin, false))));
        buttons.Children.Add(Tool($"{id}:local", new HoverCard("Keep as a local", "Makes a local and stores this in it. It lasts while this function runs."), () => Apply(_edits.Promote(pin, true))));
        if (VariantTypes.PartsOf(pin.Port.Type) > 0)
        {
            buttons.Children.Add(Tool($"{id}:split", new HoverCard("Split into parts", "Adds a step with one output for each part, like x, y and z."), () => Apply(_edits.Split(pin))));
        }

        return PinRow(pin, buttons);
    }

    // Reset needs a value to put back, which a type with no parts or zero lacks.
    Control ValueRow(string id, Pin pin)
    {
        var step = pin.Step;
        var parameter = pin.Port.Parameter;
        var box = PanelFields.Box($"{id}:value", step.Parameters.GetValueOrDefault(parameter, ""), text => Apply(_file.SetStepParameter(step.Id, parameter, text)));
        if (VariantText.ZeroOf(pin.Port.Type) is null)
        {
            return box;
        }

        var reset = Tool($"{id}:reset", new HoverCard("Reset", "Sets it back to zero or empty."), () => Apply(_edits.Reset(pin)));
        reset.Margin = new Thickness(Gaps.Tight, 0, 0, 0);
        DockPanel.SetDock(reset, Dock.Right);
        return new DockPanel { Children = { reset, box } };
    }

    // The first choice is where the value comes from now. Picking another makes it and wires it to the pin.
    Control Source(string id, Pin pin, string now)
    {
        var choice = new Segments(now, "Remembered", "Local");
        choice.Choices[1].Tag = $"{id}:variable";
        choice.Choices[2].Tag = $"{id}:local";
        new HoverCard("Remembered", "Makes a remembered value and wires it here. The whole script can use it.").On(choice.Choices[1]);
        new HoverCard("Local", "Makes a local and wires it here. It lasts while this function runs.").On(choice.Choices[2]);
        choice.Changed += () => Apply(_edits.Promote(pin, choice.SelectedIndex == 2));
        var heading = new TextBlock { Text = $"Where the {pin.Port.Name.ToLowerInvariant()} comes from", FontSize = 11, Foreground = EditorBrushes.TextDim, TextWrapping = TextWrapping.Wrap };
        return new StackPanel { Spacing = Gaps.Tight, Children = { heading, choice } };
    }

    static StackPanel PinRow(Pin pin, params Control[] body)
    {
        var port = pin.Port;
        var type = new TextBlock { Text = VariantTypes.NameOf(port.Type), FontSize = 11, Foreground = EditorBrushes.TextDim, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(type, Dock.Right);
        var name = new TextBlock { Text = char.ToUpperInvariant(port.Name[0]) + port.Name[1..], FontSize = 12 };
        var row = new StackPanel { Spacing = Gaps.Tight, Margin = new Thickness(0, Gaps.Tight, 0, 0), Children = { new DockPanel { Children = { type, name } } } };
        row.Children.AddRange(body);
        return row;
    }

    static StackPanel Labeled(string label, Control box)
    {
        var row = new StackPanel { Spacing = Gaps.Tight };
        row.Children.Add(new TextBlock { Text = label, FontSize = 12 });
        row.Children.Add(box);
        return row;
    }

    static Button Tool(string id, HoverCard card, Action run)
    {
        var button = new Button
        {
            Tag = id,
            Content = card.Title,
            HorizontalAlignment = HorizontalAlignment.Left,
            Classes = { EditorTheme.TextButton, EditorTheme.Ghost, EditorTheme.Small },
        };
        button.Click += (_, _) => run();
        return card.On(button);
    }
}

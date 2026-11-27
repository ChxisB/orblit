using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>
/// The panels of a graph script, each in plain words with the engine's name beside it: its problems, its starts,
/// the values it remembers, its actions, the signature and locals of the function on show, its messages, and the
/// steps that use each message. A start is a function that overrides a virtual of the base type. Every other
/// function is an action. Each row edits one thing, and most drag onto the canvas. An edit the engine refuses is
/// shown as the reason it gave, and the row goes back to what the engine holds.
/// </summary>
internal sealed class ScriptPanels : Border
{
    readonly IScriptDeclarations _script;
    readonly StackPanel _body = new() { Spacing = Gaps.Group, Margin = new Thickness(Gaps.Group) };
    IReadOnlyList<GraphStepEntry>? _overrides;
    readonly TextBlock _notice = new() { Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap, IsVisible = false, FontSize = 12 };
    readonly VariableTool _variableTool = new();
    readonly Flyout _variableFlyout = new() { FlyoutPresenterClasses = { EditorTheme.Flush } };

    public ScriptPanels(IScriptDeclarations script)
    {
        _script = script;
        Child = new ScrollViewer { Content = _body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        _variableFlyout.Content = _variableTool;
        _variableTool.Cancelled += _variableFlyout.Hide;
        _variableTool.Requested += AddVariable;
        _script.Changed += Rebuild;
        Rebuild();
    }

    /// <summary>Raised when a person opens another function, so the canvas can show it. Opening is not an edit.</summary>
    public event Action<string>? FunctionOpened;

    /// <summary>Raised when a person picks a link to a step, after its function is open, so the canvas can show the step.</summary>
    public event Action<int>? StepRevealed;

    /// <summary>The reason the engine gave for the last edit it refused, or empty after an edit it took.</summary>
    internal string Notice => _notice.IsVisible ? _notice.Text ?? "" : "";

    /// <summary>The control with this id, such as <c>variable:total:name</c>. Null when the panels have none.</summary>
    internal T? Find<T>(string id) where T : Control => _body.GetVisualDescendants().OfType<T>().FirstOrDefault(control => (control.Tag as string) == id);

    /// <summary>The popover the Remembers add button opens.</summary>
    internal VariableTool VariableTool => _variableTool;

    void Rebuild()
    {
        _body.Children.Clear();
        _body.Children.Add(_notice);
        AddProblems();
        AddStarts();
        AddVariables();
        AddActions();
        AddLocals();
        AddSignals();
        AddConnections();
    }

    // A result the engine refused leaves nothing changed and raises no event, so the panels go back to the document themselves.
    void Apply(GraphEditResult result)
    {
        _notice.IsVisible = !result.Ok;
        _notice.Text = result.Reason;
        if (!result.Ok)
        {
            Rebuild();
        }
    }

    // A section's plain name in bold with the engine's name beside it in dim mono, what it holds in a dim line
    // under them, and its add button on the right.
    static Grid Header(string title, string? engine, string line, Button? add = null)
    {
        // The engine's name goes under the plain one in a narrow list.
        var name = new WrapPanel { ItemSpacing = Gaps.Item, Children = { new TextBlock { Text = title, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap } } };
        if (engine is not null)
        {
            name.Children.Add(new TextBlock { Text = engine, FontFamily = EditorTheme.Mono, FontSize = 10.5, Foreground = EditorBrushes.Unit, VerticalAlignment = VerticalAlignment.Center });
        }

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, Gaps.Item, 0, 0) };
        header.Children.Add(new StackPanel { Spacing = Gaps.Joined, Children = { name, Dim(line) } });
        if (add is not null)
        {
            add.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetColumn(add, 1);
            header.Children.Add(add);
        }

        return header;
    }

    static TextBlock Dim(string line, string? id = null) => new() { Text = line, Tag = id, FontSize = 11, Foreground = EditorBrushes.TextDim, TextWrapping = TextWrapping.Wrap };

    static Button AddButton(string id, HoverCard card, Action click) => card.On(PanelFields.Small(id, "+ Add", click));

    void AddVariables()
    {
        var variables = _script.Document.Variables;
        // The button opens the popover itself. The click starts the popover over with a free name.
        var add = AddButton("variable:add", new HoverCard("Add a remembered value", "A value the graph keeps between steps, like health."), () => _variableTool.Reset(PanelFields.Unique(variables.Select(v => v.Name), "value")));
        add.Flyout = _variableFlyout;
        _body.Children.Add(Header("Remembers", "Variables", "Values the graph keeps, like opened.", add));
        foreach (var variable in variables)
        {
            _body.Children.Add(VariableRow(variable));
        }
    }

    // The popover closes first, because the edit rebuilds the panels and the button it hangs from goes.
    void AddVariable(string name, int type, string valueText, bool shown)
    {
        _variableFlyout.Hide();
        var result = _script.AddVariable(name, type);
        if (result.Ok && valueText != "")
        {
            result = _script.SetVariableDefault(name, valueText);
        }

        if (result.Ok && shown)
        {
            result = _script.SetVariableExported(name, true);
        }

        Apply(result);
    }

    Control VariableRow(GraphVariable variable)
    {
        var id = $"variable:{variable.Name}";
        var name = variable.Name;
        var detail = new StackPanel { Spacing = Gaps.Tight, Margin = new Thickness(Gaps.Dialog, 0, 0, Gaps.Tight) };
        // The type has its own line, because a narrow column cannot hold it beside the name.
        var type = PanelFields.TypeBox($"{id}:type", variable.Type, picked => Apply(_script.SetVariableType(name, picked)));
        type.HorizontalAlignment = HorizontalAlignment.Stretch;
        detail.Children.Add(type);
        detail.Children.Add(PanelFields.Box($"{id}:default", variable.Default, text => Apply(_script.SetVariableDefault(name, text)), "starts as nothing"));
        detail.Children.Add(PanelFields.Check($"{id}:exported", "Show in the Inspector", variable.Exported, on => Apply(_script.SetVariableExported(name, on))));
        detail.Children.Add(PanelFields.Box($"{id}:category", variable.Category, text => Apply(_script.SetVariableInfo(name, text, variable.Description)), "group in the Inspector"));
        detail.Children.Add(PanelFields.Box($"{id}:description", variable.Description, text => Apply(_script.SetVariableInfo(name, variable.Category, text)), "what it is for"));
        return Row(id, new GraphDrop(GraphDropKind.Variable, name), detail, text => _script.RenameVariable(name, text), () => _script.RemoveVariable(name));
    }

    // A named thing with a grip, a name and a remove button, and the lines that edit the rest of it.
    StackPanel Row(string id, GraphDrop drop, Control? detail, Func<string, GraphEditResult> rename, Func<GraphEditResult> remove)
    {
        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = Gaps.Tight };
        Control[] cells = [PanelFields.Handle($"{id}:drag", drop), PanelFields.Box($"{id}:name", drop.Value, text => Apply(rename(text))), RemoveButton(id, drop.Value, remove)];
        for (var at = 0; at < cells.Length; at++)
        {
            Grid.SetColumn(cells[at], at);
            top.Children.Add(cells[at]);
        }

        var row = new StackPanel { Spacing = Gaps.Joined };
        row.Children.Add(top);
        if (detail is not null)
        {
            row.Children.Add(detail);
        }

        return row;
    }

    // The base type does not change while a script is open, so the engine is asked once.
    IReadOnlyList<GraphStepEntry> Overrides => _overrides ??= _script.FindOverrides();

    bool IsStart(string function) => Overrides.Any(entry => entry.Label == function);

    void AddStarts()
    {
        var functions = _script.Document.Functions;
        var free = Overrides.Where(entry => functions.All(function => function.Name != entry.Label)).DistinctBy(entry => entry.Label).ToList();
        var card = new HoverCard("Add a start", "Picks what makes the graph run, like every frame.");
        // The button opens its menu itself.
        var add = AddButton("start:add", card, () => { });
        add.Flyout = StartMenu(free);
        add.IsEnabled = free.Count > 0;
        card.SaysWhyOff(add, Overrides.Count == 0 ? "This kind of thing has no starts." : "Every start is in the graph already.");
        _body.Children.Add(Header("Starts when", null, "What makes the graph run.", add));
        var starts = functions.Where(function => IsStart(function.Name)).ToList();
        if (starts.Count == 0)
        {
            _body.Children.Add(Dim("Nothing starts this graph yet. Add picks what does, like every frame.", "starts:empty"));
        }

        foreach (var start in starts)
        {
            _body.Children.Add(StartRow(start.Name));
            AddSignatureIfOnShow(start.Name);
        }
    }

    // The plain starts by group, then every other start by its engine name, A to Z, for people who know them.
    MenuFlyout StartMenu(IReadOnlyList<GraphStepEntry> free)
    {
        var menu = new MenuFlyout();
        foreach (var group in StartNames.Rows.Where(row => free.Any(entry => entry.Label == row.Engine)).GroupBy(row => row.Group))
        {
            menu.Items.Add(Parts.MenuHeading(group.Key));
            foreach (var row in group)
            {
                menu.Items.Add(StartItem(free.First(entry => entry.Label == row.Engine)));
            }
        }

        var rest = free.Where(entry => StartNames.Of(entry.Label) is null).OrderBy(entry => entry.Label, StringComparer.Ordinal).ToList();
        if (rest.Count > 0)
        {
            var all = new MenuItem { Header = Parts.Noted("All starts, A to Z", $"{rest.Count} more, for experts") };
            AutomationProperties.SetName(all, "All starts, A to Z");
            foreach (var entry in rest)
            {
                all.Items.Add(StartItem(entry));
            }

            menu.Items.Add(all);
        }

        return menu;
    }

    MenuItem StartItem(GraphStepEntry entry)
    {
        var row = StartNames.Of(entry.Label);
        var name = row?.Sentence ?? entry.Label;
        var item = new MenuItem { Header = Parts.Noted(name, row?.Line, row?.Engine) };
        AutomationProperties.SetName(item, name);
        item.Click += (_, _) => AddStart(entry);
        return item;
    }

    // Choosing a start adds the function that overrides it and opens that function.
    void AddStart(GraphStepEntry entry)
    {
        var result = _script.AddOverride(entry);
        Apply(result);
        if (result.Ok)
        {
            Open(entry.Label);
        }
    }

    // A start goes by what makes it run. Its name is the engine's, so it has no box to rename it and no grip.
    Grid StartRow(string name)
    {
        var plain = StartNames.Of(name);
        var label = Parts.Noted(plain?.Sentence ?? name, null, plain?.Engine);
        label.Tag = $"function:{name}:start";
        label.VerticalAlignment = VerticalAlignment.Center;
        var show = ShowButton(name);
        var remove = RemoveButton($"function:{name}", plain?.Sentence ?? name, () => _script.RemoveFunction(name));
        Grid.SetColumn(show, 1);
        Grid.SetColumn(remove, 2);
        return new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = Gaps.Tight, Children = { label, show, remove } };
    }

    void AddActions()
    {
        var functions = _script.Document.Functions;
        var card = new HoverCard("New action", "A set of steps you can run from many places, like open_door.");
        _body.Children.Add(Header("My actions", "Functions", "Sets of steps to reuse.", AddButton("function:add", card, () => Apply(_script.AddFunction(PanelFields.Unique(functions.Select(f => f.Name), "action"))))));
        var actions = functions.Where(function => !IsStart(function.Name)).ToList();
        if (actions.Count == 0)
        {
            _body.Children.Add(Dim("An action is a set of steps you can run from many places, like open_door.", "actions:empty"));
        }

        foreach (var action in actions)
        {
            _body.Children.Add(ActionRow(action.Name));
            AddSignatureIfOnShow(action.Name);
        }
    }

    Control ActionRow(string name)
    {
        var row = Row($"function:{name}", new GraphDrop(GraphDropKind.Function, name), null, text => _script.RenameFunction(name, text), () => _script.RemoveFunction(name));
        row.Children.Add(ShowButton(name));
        return row;
    }

    // The function on show is the one the canvas holds, so its button is off and says so.
    Button ShowButton(string name)
    {
        var isOpen = name == _script.Function?.Name;
        var card = new HoverCard("Show", "Shows its steps on the canvas.");
        var show = card.On(PanelFields.Small($"function:{name}:open", isOpen ? "Showing" : "Show", () => Open(name)));
        show.IsEnabled = !isOpen;
        card.SaysWhyOff(show, "Its steps are on the canvas already.");
        return show;
    }

    Button RemoveButton(string id, string name, Func<GraphEditResult> remove) =>
        new HoverCard("Remove", $"Takes {name} out of the graph.").On(PanelFields.Small($"{id}:remove", "x", () => Apply(remove())));

    // The signature edits sit under the row of the function on show.
    void AddSignatureIfOnShow(string name)
    {
        if (name == _script.Function?.Name && FunctionSignature.Of(_script.Function) is { } signature)
        {
            _body.Children.Add(SignatureEditor(signature));
        }
    }

    void Open(string name)
    {
        _script.OpenFunction(name);
        Rebuild();
        FunctionOpened?.Invoke(name);
    }

    // The arguments, the type returned and the description live on the Entry step of the function on show.
    Control SignatureEditor(FunctionSignature signature)
    {
        var stack = new StackPanel { Spacing = Gaps.Tight, Margin = new Thickness(Gaps.Dialog, 0, 0, Gaps.Tight) };
        stack.Children.Add(new TextBlock { Text = "It gets", FontSize = 12 });
        stack.Children.Add(new TypedListEditor("function:arguments", signature.Arguments, null, list => Apply(Set(signature, "arguments", GraphTyped.WriteList(list)))));
        stack.Children.Add(new TextBlock { Text = "It gives back", FontSize = 12 });
        stack.Children.Add(PanelFields.TypeBox("function:returns", signature.Returns, type => Apply(Set(signature, "returns", type.ToString()))));
        stack.Children.Add(new TextBlock { Text = "What it does", FontSize = 12 });
        stack.Children.Add(PanelFields.Box("function:description", signature.Description, text => Apply(Set(signature, "description", VariantText.Quote(text))), "Shows in the step list and on hover."));
        return stack;
    }

    GraphEditResult Set(FunctionSignature signature, string parameter, string valueText) => _script.SetStepParameter(signature.StepId, parameter, valueText);

    void AddLocals()
    {
        if (FunctionSignature.Of(_script.Function) is not { } signature)
        {
            return;
        }

        _body.Children.Add(Header($"Values only {_script.Function!.Name} uses", "Locals", "They start fresh each time it runs."));
        _body.Children.Add(new TypedListEditor("local", signature.Locals, GraphDropKind.Local, list => Apply(Set(signature, "locals", GraphTyped.WriteList(list)))));
    }

    void AddProblems()
    {
        _body.Children.Add(Header("Problems", null, "What is wrong, and at which step. Click one to go there."));
        _body.Children.Add(new ProblemsList(_script.Document, Reveal));
    }

    void AddConnections()
    {
        _body.Children.Add(Header("Where messages are used", null, "The steps that send or wait for each message."));
        _body.Children.Add(new ConnectionsList(_script.Document, Reveal));
    }

    /// <summary>Opens the function when another is on show, then asks the canvas to show the step.</summary>
    internal void Reveal(string function, int step)
    {
        if (function != _script.Function?.Name)
        {
            Open(function);
        }

        StepRevealed?.Invoke(step);
    }

    void AddSignals()
    {
        var signals = _script.Document.Signals;
        var card = new HoverCard("New message", "Something the graph can tell other things, like opened.");
        _body.Children.Add(Header("Messages", "Signals", "What the graph can tell other things.", AddButton("signal:add", card, () => Apply(_script.AddSignal(PanelFields.Unique(signals.Select(s => s.Name), "message"))))));
        foreach (var signal in signals)
        {
            var name = signal.Name;
            var arguments = new TypedListEditor($"signal:{name}:arguments", signal.Arguments, null, list => Apply(_script.SetSignalArguments(name, list)));
            arguments.Margin = new Thickness(Gaps.Dialog, 0, 0, Gaps.Tight);
            var row = Row($"signal:{name}", new GraphDrop(GraphDropKind.Signal, name), arguments, text => _script.RenameSignal(name, text), () => _script.RemoveSignal(name));
            _body.Children.Add(row);
        }
    }
}

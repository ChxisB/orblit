using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>
/// The panels of a graph script: its problems, its variables, its functions with the signature of the one on show,
/// the locals of that function, its signals, and the steps that use each signal. Each row edits one thing and drags onto the canvas. An edit the engine
/// refuses is shown as the reason it gave, and the row goes back to what the engine holds.
/// </summary>
internal sealed class ScriptPanels : Border
{
    readonly IScriptDeclarations _script;
    readonly StackPanel _body = new() { Spacing = 10, Margin = new Thickness(10) };
    IReadOnlyList<GraphStepEntry>? _overrides;
    readonly TextBlock _notice = new() { Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap, IsVisible = false, FontSize = 12 };

    public ScriptPanels(IScriptDeclarations script)
    {
        _script = script;
        Background = EditorBrushes.Panel;
        Child = new ScrollViewer { Content = _body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
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

    void Rebuild()
    {
        _body.Children.Clear();
        _body.Children.Add(_notice);
        AddProblems();
        AddVariables();
        AddFunctions();
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

    Control Header(string title, string id, Action add)
    {
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 6, 0, 0) };
        header.Children.Add(new TextBlock { Text = title, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center });
        var button = PanelFields.Small(id, "+", add);
        Grid.SetColumn(button, 1);
        header.Children.Add(button);
        return header;
    }

    void AddVariables()
    {
        var variables = _script.Document.Variables;
        _body.Children.Add(Header("Variables", "variable:add", () => Apply(_script.AddVariable(PanelFields.Unique(variables.Select(v => v.Name), "variable"), 0))));
        foreach (var variable in variables)
        {
            _body.Children.Add(VariableRow(variable));
        }
    }

    Control VariableRow(GraphVariable variable)
    {
        var id = $"variable:{variable.Name}";
        var name = variable.Name;
        var detail = new StackPanel { Spacing = 2, Margin = new Thickness(16, 0, 0, 4) };
        detail.Children.Add(PanelFields.Box($"{id}:default", variable.Default, text => Apply(_script.SetVariableDefault(name, text)), "default"));
        detail.Children.Add(PanelFields.Check($"{id}:exported", "Export", variable.Exported, on => Apply(_script.SetVariableExported(name, on))));
        detail.Children.Add(PanelFields.Box($"{id}:category", variable.Category, text => Apply(_script.SetVariableInfo(name, text, variable.Description)), "category"));
        detail.Children.Add(PanelFields.Box($"{id}:description", variable.Description, text => Apply(_script.SetVariableInfo(name, variable.Category, text)), "description"));
        return Row(id, new GraphDrop(GraphDropKind.Variable, name), name, variable.Type, detail, text => _script.RenameVariable(name, text), type => _script.SetVariableType(name, type), () => _script.RemoveVariable(name));
    }

    // A named thing with a grip, a name, a type and a remove button, and the lines that edit the rest of it.
    StackPanel Row(string id, GraphDrop drop, string name, int? type, Control? detail, Func<string, GraphEditResult> rename, Func<int, GraphEditResult>? retype, Func<GraphEditResult> remove)
    {
        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), ColumnSpacing = 4 };
        var cells = new List<Control>
        {
            PanelFields.Handle($"{id}:drag", drop),
            PanelFields.Box($"{id}:name", name, text => Apply(rename(text))),
        };
        if (type is { } current && retype is not null)
        {
            cells.Add(PanelFields.TypeBox($"{id}:type", current, picked => Apply(retype(picked))));
        }

        cells.Add(PanelFields.Small($"{id}:remove", "x", () => Apply(remove())));
        for (var at = 0; at < cells.Count; at++)
        {
            Grid.SetColumn(cells[at], at == cells.Count - 1 ? 3 : at);
            top.Children.Add(cells[at]);
        }

        var row = new StackPanel { Spacing = 2 };
        row.Children.Add(top);
        if (detail is not null)
        {
            row.Children.Add(detail);
        }

        return row;
    }

    void AddFunctions()
    {
        var functions = _script.Document.Functions;
        _body.Children.Add(Header("Functions", "function:add", () => Apply(_script.AddFunction(PanelFields.Unique(functions.Select(f => f.Name), "function")))));
        if (Overridable(functions) is { Count: > 0 } overridable)
        {
            _body.Children.Add(OverridePicker(overridable));
        }

        foreach (var function in functions)
        {
            _body.Children.Add(FunctionRow(function));
        }

        if (FunctionSignature.Of(_script.Function) is { } signature)
        {
            _body.Children.Add(SignatureEditor(signature));
        }
    }

    // The base type does not change while a script is open, so the engine is asked once.
    List<GraphStepEntry> Overridable(IReadOnlyList<GraphFunction> functions)
    {
        _overrides ??= _script.FindOverrides();
        var taken = functions.Select(f => f.Name).ToHashSet();
        return _overrides.Where(entry => !taken.Contains(entry.Label)).DistinctBy(entry => entry.Label).ToList();
    }

    // Choosing a virtual adds the function that overrides it and opens that function.
    ComboBox OverridePicker(List<GraphStepEntry> entries)
    {
        var box = new ComboBox { ItemsSource = entries.Select(entry => $"{entry.Label} ({entry.Group})").ToList(), PlaceholderText = "Override a virtual", Tag = "function:override", MinWidth = 90, FontSize = 12 };
        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedIndex < 0)
            {
                return;
            }

            var entry = entries[box.SelectedIndex];
            var result = _script.AddOverride(entry);
            Apply(result);
            if (result.Ok)
            {
                Open(entry.Label);
            }
        };
        return box;
    }

    Control FunctionRow(GraphFunction function)
    {
        var name = function.Name;
        var isOpen = name == _script.Function?.Name;
        var open = PanelFields.Small($"function:{name}:open", isOpen ? "Open" : "Show", () => Open(name));
        open.IsEnabled = !isOpen;
        var row = Row($"function:{name}", new GraphDrop(GraphDropKind.Function, name), name, null, null, text => _script.RenameFunction(name, text), null, () => _script.RemoveFunction(name));
        row.Children.Add(open);
        return row;
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
        var stack = new StackPanel { Spacing = 2, Margin = new Thickness(16, 0, 0, 4) };
        stack.Children.Add(new TextBlock { Text = $"{_script.Function!.Name} takes", FontSize = 12 });
        stack.Children.Add(new TypedListEditor("function:arguments", signature.Arguments, null, list => Apply(Set(signature, "arguments", GraphTyped.WriteList(list)))));
        stack.Children.Add(new TextBlock { Text = "and returns", FontSize = 12 });
        stack.Children.Add(PanelFields.TypeBox("function:returns", signature.Returns, type => Apply(Set(signature, "returns", type.ToString()))));
        stack.Children.Add(PanelFields.Box("function:description", signature.Description, text => Apply(Set(signature, "description", VariantText.Quote(text))), "description"));
        return stack;
    }

    GraphEditResult Set(FunctionSignature signature, string parameter, string valueText) => _script.SetStepParameter(signature.StepId, parameter, valueText);

    void AddLocals()
    {
        if (FunctionSignature.Of(_script.Function) is not { } signature)
        {
            return;
        }

        _body.Children.Add(new TextBlock { Text = $"Locals of {_script.Function!.Name}", FontWeight = FontWeight.Bold, Margin = new Thickness(0, 6, 0, 0) });
        _body.Children.Add(new TypedListEditor("local", signature.Locals, GraphDropKind.Local, list => Apply(Set(signature, "locals", GraphTyped.WriteList(list)))));
    }

    void AddProblems()
    {
        _body.Children.Add(new TextBlock { Text = "Problems", FontWeight = FontWeight.Bold, Margin = new Thickness(0, 6, 0, 0) });
        _body.Children.Add(new ProblemsList(_script.Document, Reveal));
    }

    void AddConnections()
    {
        _body.Children.Add(new TextBlock { Text = "Connections", FontWeight = FontWeight.Bold, Margin = new Thickness(0, 6, 0, 0) });
        _body.Children.Add(new ConnectionsList(_script.Document, Reveal));
    }

    void Reveal(string function, int step)
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
        _body.Children.Add(Header("Signals", "signal:add", () => Apply(_script.AddSignal(PanelFields.Unique(signals.Select(s => s.Name), "signal")))));
        foreach (var signal in signals)
        {
            var name = signal.Name;
            var arguments = new TypedListEditor($"signal:{name}:arguments", signal.Arguments, null, list => Apply(_script.SetSignalArguments(name, list)));
            arguments.Margin = new Thickness(16, 0, 0, 4);
            var row = Row($"signal:{name}", new GraphDrop(GraphDropKind.Signal, name), name, null, arguments, text => _script.RenameSignal(name, text), null, () => _script.RemoveSignal(name));
            _body.Children.Add(row);
        }
    }
}

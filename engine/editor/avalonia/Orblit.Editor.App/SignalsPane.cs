using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App;

/// <summary>
/// The Signals tab: the selected entity's signals under the class that declares each, its own class first, and
/// each saved connection under its signal. A signal shows its plain name first and the engine's name in dim mono.
/// Connect… opens a dialog that picks the entity and the method to call.
/// </summary>
internal sealed class SignalsPane : DockPanel
{
    const double RowHeight = 28;
    const string MethodRule = "Name the method with letters, numbers and _ only.";

    // The plain name of each common signal, and how it reads after "When Box". A list of cases, so it is data.
    static readonly Dictionary<string, (string Name, string When)> s_plain = new()
    {
        ["visibility_changed"] = ("Shown or hidden", "is shown or hidden"),
        ["ready"] = ("Ready", "is ready"),
        ["renamed"] = ("Renamed", "is renamed"),
        ["stage_entered"] = ("Entered the scene", "enters the scene"),
        ["stage_exiting"] = ("Leaving the scene", "is leaving the scene"),
        ["stage_exited"] = ("Left the scene", "has left the scene"),
        ["child_entered_stage"] = ("A child came in", "gets a new child"),
        ["child_exiting_stage"] = ("A child is leaving", "is losing a child"),
        ["child_order_changed"] = ("Children moved", "has its children moved"),
        ["timeout"] = ("Time is up", "runs out of time"),
        ["body_entered"] = ("A body came in", "has a body come in"),
        ["body_exited"] = ("A body went out", "has a body go out"),
        ["pressed"] = ("Pressed", "is pressed"),
        ["finished"] = ("Finished", "finishes"),
    };

    readonly TextBox _filter = new()
    {
        PlaceholderText = "Find a signal",
        Height = 28,
        MinHeight = 0,
        InnerRightContent = Parts.SearchIcon(),
        Margin = new Thickness(10, 8, 10, 2),
    };
    readonly TextBlock _lead = new() { FontSize = 12, Foreground = EditorBrushes.TextDim, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12, 4, 12, 4) };
    readonly StackPanel _list = new() { Margin = new Thickness(6, 0, 6, 8) };
    readonly StackPanel _empty = Parts.EmptyState("Signals", "Nothing selected", "Select an entity to see its signals and where they go.");
    readonly ChoiceList _targets = new("Filter entities");
    readonly TextBox _method = new() { MinHeight = 0, Height = 28, FontFamily = EditorTheme.Mono, FontSize = 12 };
    readonly TextBlock _methodLine = new()
    {
        Text = "The method runs on the thing you pick, each time this happens. Its script needs a method by this name.",
        FontSize = 11,
        Foreground = EditorBrushes.TextDim,
        TextWrapping = TextWrapping.Wrap,
    };
    List<(Choice Choice, OutlineNode Entity)> _choices = [];
    IReadOnlyList<EntitySignal> _signals = [];
    IEditorScene? _scene;
    OutlineNode? _root;
    OutlineNode? _selected;
    EntitySignal? _connecting;

    public SignalsPane()
    {
        Dialog = new Dialog("", "Pick who hears about it, and what they do.", new StackPanel { Spacing = 10, Children = { Parts.Labeled("Tell", _targets), Parts.Labeled("Run", _method, _methodLine) } })
        {
            CardWidth = 320,
            EngineName = "Connect a Signal to a Method",
        };
        Dialog.MainButton.Content = Parts.Keyed("Connect", "↵");
        SetDock(_filter, Dock.Top);
        SetDock(_lead, Dock.Top);
        Children.Add(_filter);
        Children.Add(_lead);
        Children.Add(_empty);
        Children.Add(new ScrollViewer { Content = _list });
        // TextChanged is not raised for a value set in code, so the text property is watched.
        _filter.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) { Fill(); } };
        _targets.PickedChanged += AllowConnect;
        _method.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) { AllowConnect(); } };
        Dialog.MainButton.Click += (_, _) => Run(ConnectPicked);
        Show(null, null, null);
    }

    /// <summary>Raised after the scene took a connection or lost one.</summary>
    public event Action<EntityEdit>? Changed;

    /// <summary>Raised with the reason when the scene refused a connection.</summary>
    public event Action<string>? Failed;

    internal Dialog Dialog { get; }

    internal TextBox FilterBox => _filter;

    internal ChoiceList TargetList => _targets;

    internal TextBox MethodBox => _method;

    internal StackPanel List => _list;

    /// <summary>The line under the search that says what the signals are for.</summary>
    internal TextBlock Lead => _lead;

    /// <summary>The plain name of a signal, or its own name when it has none.</summary>
    internal static string PlainOf(string signal) => s_plain.TryGetValue(signal, out var plain) ? plain.Name : signal;

    public void Show(IEditorScene? scene, OutlineNode? root, OutlineNode? selected)
    {
        (_scene, _root, _selected) = (scene, root, selected);
        _signals = scene is null || selected is null ? [] : scene.SignalsOf(selected.Handle);
        Fill();
    }

    // A script's signals come first under its file's name, then the entity's own class, then each class under it.
    // Only the own class says it has none. A search matches the plain name or the engine's name.
    void Fill()
    {
        _list.Children.Clear();
        _empty.IsVisible = _selected is null;
        _lead.IsVisible = _selected is not null;
        if (_selected is null)
        {
            return;
        }
        var name = _selected.Name;
        _lead.Text = $"Things {name} can tell others when they happen. Connect one to run a method on another thing.";
        var text = _filter.Text?.Trim() ?? "";
        var script = _scene!.ScriptOf(_selected.Handle);
        var chain = _scene.ClassChainOf(_selected.Handle);
        var groups = chain.Select((className, at) => (Class: className, Bar: new ClassBar(className, at == 0 ? $"From {name} itself" : PlainNames.FromAny(className))));
        if (script is not null)
        {
            groups = groups.Prepend(("", new ClassBar(Path.GetFileName(script))));
        }
        foreach (var (owner, bar) in groups)
        {
            var own = _signals.Where(signal => signal.Class == owner && Matches(signal.Name, text)).ToList();
            var isOwnClass = chain.Count > 0 && owner == chain[0];
            if (own.Count == 0 && (text.Length > 0 || !isOwnClass))
            {
                continue;
            }
            var rows = new StackPanel { Spacing = 2, Margin = new Thickness(0, 2, 0, 8) };
            foreach (var signal in own)
            {
                rows.Children.Add(SignalRow(signal));
                rows.Children.AddRange(signal.Links.Select(link => LinkRow(signal, link)));
            }
            if (own.Count == 0)
            {
                rows.Children.Add(new TextBlock { Text = $"{name} has no signals of its own.", FontSize = 11, Foreground = EditorBrushes.TextDim, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6, 2) });
            }
            _list.Children.Add(bar);
            _list.Children.Add(rows);
        }
    }

    static bool Matches(string signal, string text) =>
        signal.Contains(text, StringComparison.OrdinalIgnoreCase) || PlainOf(signal).Contains(text, StringComparison.OrdinalIgnoreCase);

    // The plain name, then the engine's name with its arguments in dim mono, then how many links it has.
    Control SignalRow(EntitySignal signal)
    {
        var connect = new HoverCard("Connect…", "Pick a thing to run a method on each time this happens.").On(
            new Button { Content = "Connect…", Classes = { EditorTheme.TextButton, EditorTheme.Small }, VerticalAlignment = VerticalAlignment.Center });
        connect.Click += (_, _) => OpenConnect(signal);
        var plain = PlainOf(signal.Name);
        var row = new DockPanel { Height = RowHeight, Margin = new Thickness(6, 0), Children = { Parts.Right(connect) } };
        if (signal.Links.Count > 0)
        {
            row.Children.Add(Parts.Right(new TextBlock { Text = $"{signal.Links.Count} joined", FontSize = 11, Foreground = EditorBrushes.TextDim, Margin = new Thickness(8, 0), VerticalAlignment = VerticalAlignment.Center }));
        }
        row.Children.Add(Parts.RowIcon("Signals"));
        if (plain != signal.Name)
        {
            var words = new TextBlock { Text = plain, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            SetDock(words, Dock.Left);
            row.Children.Add(words);
        }
        var engine = Parts.Trimmed($"{signal.Name}({string.Join(", ", signal.Arguments)})", $"{signal.Name}({string.Join(", ", signal.Arguments)})");
        engine.FontFamily = EditorTheme.Mono;
        engine.FontSize = plain == signal.Name ? 12 : 10.5;
        engine.Foreground = plain == signal.Name ? EditorBrushes.Text : EditorBrushes.Unit;
        row.Children.Add(engine);
        return row;
    }

    // A saved connection can name an object outside the scene, which the outline does not hold.
    Control LinkRow(EntitySignal signal, SignalConnection link)
    {
        var target = _root?.Find(link.Target)?.Name ?? "another object";
        var remove = new HoverCard("Disconnect", $"{target} stops running {link.Method} when this happens.").On(Parts.Tight(Parts.IconButton("Close", "")));
        remove.Click += (_, _) => Run(() =>
        {
            _scene!.Disconnect(_selected!.Handle, signal.Name, link.Target, link.Method);
            return new EntityEdit(_selected.Handle, $"Disconnected {signal.Name} from {target}.{link.Method}.");
        });
        return new DockPanel
        {
            Height = 24,
            Margin = new Thickness(26, 0, 6, 0),
            Children =
            {
                Parts.Right(remove),
                new TextBlock
                {
                    Text = $"{target} · {link.Method}",
                    FontSize = 11,
                    Foreground = EditorBrushes.TextDim,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
            },
        };
    }

    // Any entity in the scene can take the call, the selected one too.
    void OpenConnect(EntitySignal signal)
    {
        _connecting = signal;
        _choices = [.. MoveTools.Tree(_root!, _root!.Name, 0)
            .Select(entry => (new Choice(EditorIcons.ForClass(entry.Entity.ClassName), entry.Entity.Name) { Tint = EditorBrushes.TreeIcon, Depth = entry.Depth, Line = PlainNames.NameOf(entry.Entity.ClassName) }, entry.Entity))];
        Dialog.Title = s_plain.TryGetValue(signal.Name, out var plain) ? $"When {_selected!.Name} {plain.When}…" : $"When {_selected!.Name} sends {signal.Name}…";
        _method.Text = MethodFor(signal.Name);
        _targets.Show([.. _choices.Select(choice => choice.Choice)]);
        AllowConnect();
        Dialog.Open();
        _targets.SearchBox.Focus();
    }

    /// <summary>The method name Connect offers first: On and the signal in Pascal case, such as OnVisibilityChanged.</summary>
    internal static string MethodFor(string signal) =>
        "On" + string.Concat(signal.Split('_', StringSplitOptions.RemoveEmptyEntries).Select(word => char.ToUpperInvariant(word[0]) + word[1..]));

    void AllowConnect()
    {
        var why = _targets.Picked is null ? "Pick who to tell first." : IsIdentifier(_method.Text ?? "") ? null : MethodRule;
        Dialog.MainButton.IsEnabled = why is null;
        new HoverCard("Connect", why ?? "Joins them. Undo takes it out again.", "↵").On(Dialog.MainButton);
    }

    static bool IsIdentifier(string name) =>
        name.Length > 0 && (char.IsLetter(name[0]) || name[0] == '_') && name.All(c => char.IsLetterOrDigit(c) || c == '_');

    // Two entities can share a name and a depth, so the lit choice is found by reference, not by value.
    EntityEdit ConnectPicked()
    {
        var target = _choices.Single(choice => ReferenceEquals(choice.Choice, _targets.Picked)).Entity;
        var method = _method.Text!;
        _scene!.Connect(_selected!.Handle, _connecting!.Name, target.Handle, method);
        Dialog.Close();
        return new EntityEdit(_selected.Handle, $"Connected {_connecting.Name} to {target.Name}.{method}.");
    }

    void Run(Func<EntityEdit> change)
    {
        try
        {
            Changed?.Invoke(change());
        }
        catch (InvalidOperationException error)
        {
            Failed?.Invoke(error.Message);
        }
    }
}

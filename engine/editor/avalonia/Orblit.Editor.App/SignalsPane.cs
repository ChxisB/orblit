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
/// each saved connection under its signal. Connect opens a dialog that picks the entity and the method to call.
/// </summary>
internal sealed class SignalsPane : DockPanel
{
    const double RowHeight = 28;

    readonly TextBox _filter = new()
    {
        PlaceholderText = "Filter signals",
        Height = 28,
        MinHeight = 0,
        InnerRightContent = Parts.SearchIcon(),
        Margin = new Thickness(10, 8, 10, 2),
    };
    readonly StackPanel _list = new() { Margin = new Thickness(6, 0, 6, 8) };
    readonly StackPanel _empty = Parts.EmptyState("Signals", "Nothing selected", "Select an entity to see its signals and where they go.");
    readonly ChoiceList _targets = new("Filter entities");
    readonly TextBox _method = new() { MinHeight = 0, Height = 28, FontFamily = EditorTheme.Mono, FontSize = 12 };
    List<(Choice Choice, OutlineNode Entity)> _choices = [];
    IReadOnlyList<EntitySignal> _signals = [];
    IEditorScene? _scene;
    OutlineNode? _root;
    OutlineNode? _selected;
    EntitySignal? _connecting;

    public SignalsPane()
    {
        Dialog = new Dialog("", "", new StackPanel { Spacing = 10, Children = { _targets, Parts.Labeled("Method", _method) } }) { CardWidth = 300, MainText = "Connect" };
        SetDock(_filter, Dock.Top);
        Children.Add(_filter);
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

    public void Show(IEditorScene? scene, OutlineNode? root, OutlineNode? selected)
    {
        (_scene, _root, _selected) = (scene, root, selected);
        _signals = scene is null || selected is null ? [] : scene.SignalsOf(selected.Handle);
        Fill();
    }

    // A script's signals come first under its file's name, then each class from the entity's own down to Entity.
    void Fill()
    {
        _list.Children.Clear();
        _empty.IsVisible = _selected is null;
        if (_selected is null)
        {
            return;
        }
        var text = _filter.Text?.Trim() ?? "";
        var script = _scene!.ScriptOf(_selected.Handle);
        var groups = _scene.ClassChainOf(_selected.Handle).Select(name => (Title: name, Class: name));
        if (script is not null)
        {
            groups = groups.Prepend((Path.GetFileName(script), ""));
        }
        foreach (var (title, owner) in groups)
        {
            var own = _signals.Where(signal => signal.Class == owner && signal.Name.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
            if (own.Count == 0 && text.Length > 0)
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
                rows.Children.Add(new TextBlock { Text = "No signals of its own.", FontSize = 11, Foreground = EditorBrushes.Disabled, Margin = new Thickness(6, 2) });
            }
            _list.Children.Add(new ClassBar(title));
            _list.Children.Add(rows);
        }
    }

    Control SignalRow(EntitySignal signal)
    {
        var connect = new Button { Content = "Connect", Classes = { EditorTheme.TextButton, EditorTheme.Small }, VerticalAlignment = VerticalAlignment.Center };
        ToolTip.SetTip(connect, $"Connect {signal.Name} to a method");
        connect.Click += (_, _) => OpenConnect(signal);
        return new DockPanel
        {
            Height = RowHeight,
            Margin = new Thickness(6, 0),
            Children =
            {
                Parts.Right(connect),
                Parts.RowIcon("Signals"),
                new TextBlock
                {
                    Text = $"{signal.Name}({string.Join(", ", signal.Arguments)})",
                    FontFamily = EditorTheme.Mono,
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
            },
        };
    }

    // A saved connection can name an object outside the scene, which the outline does not hold.
    Control LinkRow(EntitySignal signal, SignalConnection link)
    {
        var target = _root?.Find(link.Target)?.Name ?? "another object";
        var remove = Parts.Tight(Parts.IconButton("Close", $"Disconnect {signal.Name} from {target}.{link.Method}"));
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
            .Select(entry => (new Choice(EditorIcons.ForClass(entry.Entity.ClassName), entry.Entity.Name) { Tint = EditorBrushes.TreeIcon, Depth = entry.Depth }, entry.Entity))];
        Dialog.Title = $"Connect {signal.Name}";
        Dialog.Why.Text = $"{_selected!.Name} calls the method on the entity you pick.";
        _method.Text = MethodFor(signal.Name);
        _targets.Show([.. _choices.Select(choice => choice.Choice)]);
        AllowConnect();
        Dialog.Open();
        _targets.SearchBox.Focus();
    }

    /// <summary>The method name Connect offers first: On and the signal in Pascal case, such as OnVisibilityChanged.</summary>
    internal static string MethodFor(string signal) =>
        "On" + string.Concat(signal.Split('_', StringSplitOptions.RemoveEmptyEntries).Select(word => char.ToUpperInvariant(word[0]) + word[1..]));

    void AllowConnect() => Dialog.MainButton.IsEnabled = _targets.Picked is not null && IsIdentifier(_method.Text ?? "");

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

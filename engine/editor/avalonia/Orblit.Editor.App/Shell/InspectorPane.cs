using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The body of the Inspector tab: a history row, a search row and the property list. The history row walks back
/// and forward through the things shown, and its chip names the one shown, its kind in plain words and its class
/// in dim mono, and lists the recent ones. Holding Back lists them too. Another scene starts a new history.
/// </summary>
internal sealed class InspectorPane : Grid
{
    internal const double HistoryHeight = 36;

    // The box is 28 high and the list starts one pixel under it.
    const double FilterRowHeight = 29;
    const double BoxHeight = 28;

    // A chip on a row of its own is 38 high. Here it shares the 36 pixel row with the arrows.
    const double ChipHeight = 32;

    // How long Back is held before the list of recent things opens.
    static readonly TimeSpan s_holdTime = TimeSpan.FromMilliseconds(450);

    readonly InspectorPanel _panel;
    readonly ScrollViewer _viewer;
    readonly TextBox _filter = new()
    {
        PlaceholderText = "Find a setting",
        Height = BoxHeight,
        MinHeight = 0,
        InnerRightContent = Parts.SearchIcon(),
    };
    readonly Button _back = Parts.Tight(Parts.IconButton("Back", ""));
    readonly Button _forward = Parts.Tight(Parts.IconButton("Forward", ""));
    readonly DispatcherTimer _hold = new() { Interval = s_holdTime };
    readonly ContentControl _icon = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _name = new() { FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _kind = new() { FontSize = 11.5, Foreground = EditorBrushes.TextDim, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _class = new() { FontFamily = EditorTheme.Mono, FontSize = 10.5, Foreground = EditorBrushes.Unit, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    // The class fills what the icon, the name and the kind leave, so a long name trims before the chevron.
    readonly DockPanel _inspected = new() { HorizontalSpacing = Gaps.Tight, IsVisible = false };
    readonly MenuFlyout _recent = new();
    readonly Button _chip;
    readonly Button _more = Parts.Dress(new Button(), "GuiTabMenuHl", new HoverCard("More", "Open or close every section, and copy settings to another thing."));
    readonly List<Inspected> _seen = [];
    int _at = -1;
    string? _file;
    bool _held;

    public InspectorPane(InspectorPanel panel)
    {
        _panel = panel;
        _viewer = new ScrollViewer { Content = panel };
        RowDefinitions = new RowDefinitions($"{HistoryHeight},{FilterRowHeight},*");
        _chip = ObjectChip(_inspected, _recent);
        var back = new HoverCard("Back", "Look at the thing you inspected before. Hold for the list.");
        back.SaysWhyOff(back.On(_back), "Nothing was inspected before this.");
        var forward = new HoverCard("Forward", "Look at the next thing again.");
        forward.SaysWhyOff(forward.On(_forward), "You are at the newest. Go back first.");
        _back.IsEnabled = false;
        _forward.IsEnabled = false;
        _back.Click += (_, _) => Back();
        _forward.Click += (_, _) => Step(1);
        // The button takes the press for its click, so the hold listens on the way down.
        _back.AddHandler(PointerPressedEvent, (_, _) => Hold(), RoutingStrategies.Tunnel);
        _back.AddHandler(PointerReleasedEvent, (_, _) => _hold.Stop(), RoutingStrategies.Tunnel);
        _back.PointerCaptureLost += (_, _) => _hold.Stop();
        _hold.Tick += (_, _) => ShowHeld();
        _recent.Opening += (_, _) => FillRecent();
        _more.Flyout = More();
        // TextChanged waits for a render, so the property is the one that fires at once.
        _filter.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                _panel.SetFilter(_filter.Text ?? "");
            }
        };

        _inspected.Children.AddRange([_icon, _name, _kind, _class]);
        var history = new Grid { ColumnDefinitions = new ColumnDefinitions("24,4,24,8,*"), Margin = new Thickness(Gaps.Item, 0) };
        Place(history, _back, 0, VerticalAlignment.Center);
        Place(history, _forward, 2, VerticalAlignment.Center);
        Place(history, _chip, 4, VerticalAlignment.Center);
        var filter = new Grid { ColumnDefinitions = new ColumnDefinitions($"*,{Gaps.Item},Auto"), Margin = new Thickness(Gaps.Item, 0) };
        Place(filter, _filter, 0, VerticalAlignment.Top);
        // A pixel down, so the 26 pixel button shares the middle of the 28 pixel box.
        _more.Margin = new Thickness(0, 1, 0, 0);
        Place(filter, _more, 2, VerticalAlignment.Top);
        SetRow(filter, 1);
        SetRow(_viewer, 2);
        SetRow(Empty, 2);
        SetRow(Loading, 2);
        Loading.IsVisible = false;
        Children.Add(history);
        Children.Add(filter);
        Children.Add(_viewer);
        Children.Add(Empty);
        Children.Add(Loading);
    }

    /// <summary>What the pane says while the engine starts.</summary>
    internal StackPanel Loading { get; } = Parts.EmptyState("ListSelect", "Loading", "The engine is starting.");

    internal ScrollViewer Viewer => _viewer;

    /// <summary>What the pane says while nothing is picked.</summary>
    internal StackPanel Empty { get; } = Parts.EmptyState("ListSelect", "Nothing selected", "Click a thing in the view or the Scene panel to change its settings here.");

    internal TextBlock InspectedName => _name;

    internal EditorIcon InspectedIcon => (EditorIcon)_icon.Content!;

    /// <summary>The kind of the inspected thing in plain words. Empty when its class has none.</summary>
    internal TextBlock InspectedKind => _kind;

    /// <summary>The class of the inspected thing, the engine's name for its kind.</summary>
    internal TextBlock InspectedClass => _class;

    internal Button Chip => _chip;

    internal MenuFlyout Recent => _recent;

    internal TextBox FilterBox => _filter;

    internal Button BackButton => _back;

    internal Button ForwardButton => _forward;

    internal Button MoreButton => _more;

    /// <summary>Raised with the handle of an object the history goes back to.</summary>
    public event Action<nint>? Picked;

    /// <summary>Says the editor is loading in place of saying nothing is selected.</summary>
    public void ShowStarting(bool starting)
    {
        Loading.IsVisible = starting;
        Empty.IsVisible = !starting && !_inspected.IsVisible;
    }

    /// <summary>
    /// Shows the picked entities in the property list, names the selected one, which is last, or counts them in the
    /// chip, and adds the selected one to the history.
    /// </summary>
    public void Show(IEditorScene? scene, IReadOnlyList<OutlineNode> picked)
    {
        var node = picked.Count == 0 ? null : picked[^1];
        _panel.Show(scene, picked);
        _inspected.IsVisible = node is not null;
        Empty.IsVisible = node is null;
        _icon.Content = node is null ? null : Parts.RowIcon(EditorIcons.ForClass(node.ClassName));
        _name.Text = picked.Count > 1 ? $"{picked.Count} selected" : node?.Name ?? "";
        var className = _panel.Chain.FirstOrDefault() ?? "";
        _kind.Text = PlainNames.Of(className)?.Name ?? "";
        _kind.IsVisible = _kind.Text != "";
        _class.Text = className;
        Remember(scene?.SceneFile, node);
    }

    // The object at the cursor shown again, as after a drag or a step back, adds nothing. A new one drops the
    // steps forward of the cursor, as a browser does.
    void Remember(string? file, OutlineNode? node)
    {
        if (file != _file)
        {
            _seen.Clear();
            _at = -1;
            _file = file;
        }
        if (node is not null && (_at < 0 || _seen[_at].Handle != node.Handle))
        {
            _seen.RemoveRange(_at + 1, _seen.Count - _at - 1);
            _seen.Add(new Inspected(node.Handle, node.Name, node.ClassName));
            _at = _seen.Count - 1;
        }
        ShowSteps();
    }

    void ShowSteps()
    {
        _back.IsEnabled = _at > 0;
        _forward.IsEnabled = _at < _seen.Count - 1;
    }

    // A press starts the hold over again, since a list that opens can take the release away from the button.
    void Hold()
    {
        _held = false;
        if (_back.IsEnabled)
        {
            _hold.Start();
        }
    }

    void ShowHeld()
    {
        _hold.Stop();
        _held = true;
        _recent.ShowAt(_back);
    }

    // The release that ends a hold also clicks, and that click only closes the hold.
    void Back()
    {
        if (_held)
        {
            _held = false;
            return;
        }
        Step(-1);
    }

    // An object deleted since leaves nothing selected, and the cursor still moves past it.
    void Step(int by)
    {
        _at += by;
        ShowSteps();
        Picked?.Invoke(_seen[_at].Handle);
    }

    // Under a heading, newest first, each thing once with its kind in plain words.
    void FillRecent()
    {
        _recent.Items.Clear();
        _recent.Items.Add(Parts.MenuHeading("Recently inspected"));
        foreach (var seen in Enumerable.Reverse(_seen).DistinctBy(seen => seen.Handle))
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = Gaps.Tight,
                Children = { new TextBlock { Text = seen.Name }, new TextBlock { Text = PlainNames.NameOf(seen.ClassName), FontSize = 11, Foreground = EditorBrushes.TextDim, VerticalAlignment = VerticalAlignment.Center } },
            };
            var item = new MenuItem { Header = row, Icon = Parts.RowIcon(EditorIcons.ForClass(seen.ClassName)) };
            AutomationProperties.SetName(item, seen.Name);
            item.Click += (_, _) => Picked?.Invoke(seen.Handle);
            _recent.Items.Add(item);
        }
    }

    // Paste is off until settings are copied, and its note says so.
    MenuFlyout More()
    {
        var paste = Parts.ActionItem("Paste settings", _panel.PasteProperties);
        var unique = Parts.ActionItem("Stop sharing parts", _panel.MakeResourcesUnique);
        unique.Header = Parts.Noted("Stop sharing parts", "So changing this thing leaves others alone", "Make Sub-Resources Unique");
        var more = new MenuFlyout
        {
            Items =
            {
                Parts.ActionItem("Open all sections", () => _panel.FoldAll(true), note: "Shows every setting"),
                Parts.ActionItem("Close all sections", () => _panel.FoldAll(false), note: "Shows only the headings"),
                new Separator(),
                Parts.ActionItem("Copy all settings", _panel.CopyProperties, note: "To paste onto another thing"),
                paste,
                unique,
            },
        };
        more.Opening += (_, _) =>
        {
            paste.IsEnabled = _panel.HasCopied;
            paste.Header = Parts.Noted("Paste settings", _panel.HasCopied ? "Onto this thing" : "Copy settings first.");
        };
        return more;
    }

    // The object chip: the icon, the name, the kind and the class in quieter voices on a raised tone, with a
    // chevron that says it opens the list.
    static Button ObjectChip(Control inspected, MenuFlyout recent)
    {
        var chevron = new EditorIcon("GuiDropdown") { VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(chevron, Dock.Right);
        var chip = new Button
        {
            Height = ChipHeight,
            MinHeight = 0,
            MinWidth = 0,
            Padding = new Thickness(Gaps.Item, 0),
            Background = EditorBrushes.Raised,
            BorderBrush = EditorBrushes.LineStrong,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = new DockPanel { Children = { chevron, inspected } },
            Flyout = recent,
        };
        return new HoverCard("Recently inspected", "Pick one to look at it again.").On(chip);
    }

    readonly record struct Inspected(nint Handle, string Name, string ClassName);

    static void Place(Grid grid, Control child, int column, VerticalAlignment vertical)
    {
        SetColumn(child, column);
        child.VerticalAlignment = vertical;
        grid.Children.Add(child);
    }
}

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>What a status message reports. It sets the mark in front of the message.</summary>
internal enum StatusKind
{
    Info,
    Done,
    Failed,
}

/// <summary>
/// The bar under the viewport. Panel tabs on the left, each opening its panel over the bar, with a badge on Output
/// while errors wait to be read. On the right the last message with a mark for its kind, a rule, the version and
/// the pin.
/// </summary>
internal sealed class BottomBar : Grid
{
    // shortcut: copied from the engine's version file, read it through the bridge when it gains a version call.
    internal const string Version = "1.4.stable";

    const double TabHeight = 26;
    const double SmallIcon = 13;

    // Name and icon file. The order is the order of the panels.
    static readonly (string Name, string Icon)[] Panels =
    [
        ("Output", "Output"),
        ("Debugger", "Debug"),
    ];

    // The mark and its tint for each kind, in the order of StatusKind. A list of cases, so it is data.
    static readonly (string Icon, IBrush Tint)[] s_kinds =
    [
        ("Info", EditorBrushes.TextDim),
        ("Tick", EditorBrushes.Done),
        ("FaultCircle", EditorBrushes.Fault),
    ];

    readonly Button[] _tabs;
    readonly TextBlock _unreadCount = new() { FontSize = 10.5, FontWeight = FontWeight.SemiBold, Foreground = EditorBrushes.FaultText };
    readonly Border _unread;
    readonly ContentControl _statusIcon = new() { Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _status = new() { Text = "Starting the engine.", FontSize = 12, Foreground = EditorBrushes.TextSoft, VerticalAlignment = VerticalAlignment.Center };
    readonly Border _separator = Parts.Rule();
    readonly Button _version = new()
    {
        Padding = new Thickness(4, 0),
        BorderThickness = new Thickness(0),
        MinWidth = 0,
        MinHeight = 0,
        FontSize = 11,
        Foreground = EditorBrushes.TextDim,
        VerticalAlignment = VerticalAlignment.Center,
        Content = new TextBlock { Text = Version },
    };
    // The pin keeps the game run from opening a panel on its own. FixedToggle never flips itself, so a click does it here.
    readonly FixedToggle _pin = new HoverCard("Pin", "Keeps the bottom panel as it is when a game starts.").On(new FixedToggle("Pin", "", false) { Margin = new Thickness(2, 0, 0, 0) });
    int _open = -1;
    int _errors;

    public BottomBar()
    {
        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto");
        _unread = new Border
        {
            Padding = new Thickness(6, 1),
            CornerRadius = new CornerRadius(999),
            Background = EditorBrushes.FaultSoft,
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
            Child = _unreadCount,
        };
        _tabs = [.. Panels.Select(Tab)];
        ((StackPanel)_tabs[0].Content!).Children.Add(_unread);
        foreach (var (tab, index) in _tabs.Select((tab, index) => (tab, index)))
        {
            tab.Click += (_, _) => Open(_open == index ? -1 : index);
        }

        ToolTip.SetTip(_version, "Copy the version");
        _version.Click += (_, _) => VersionCopyRequested?.Invoke();
        _pin.Click += (_, _) => _pin.IsChecked = _pin.IsChecked != true;
        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        left.Classes.Add(EditorTheme.Joined);
        left.Children.AddRange(_tabs);
        var right = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
        _separator.Margin = new Thickness(8, 0);
        _pin.Margin = new Thickness(4, 0, 0, 0);
        right.Children.AddRange([_statusIcon, _status, _separator, _version, _pin]);
        SetColumn(right, 2);
        Children.Add(left);
        Children.Add(right);
        Say(_status.Text, StatusKind.Info);
        Open(-1);
    }

    /// <summary>Raised with the index of the panel now open, or -1 when every panel is shut.</summary>
    public event Action<int>? PanelChanged;

    /// <summary>Raised when the version is pressed. The window puts it on the clipboard.</summary>
    public event Action? VersionCopyRequested;

    internal IReadOnlyList<Button> Tabs => _tabs;

    internal Button OutputTab => _tabs[0];

    internal bool OutputOpen => _open == 0;

    internal string Status => _status.Text ?? "";

    internal EditorIcon StatusIcon => (EditorIcon)_statusIcon.Content!;

    /// <summary>The badge on the Output tab. It counts the errors logged while Output was shut.</summary>
    internal Border Unread => _unread;

    internal Border Separator => _separator;

    internal Button VersionButton => _version;

    internal FixedToggle PinButton => _pin;

    /// <summary>Shows the message on the right of the bar with the mark of its kind.</summary>
    internal void Say(string text, StatusKind kind)
    {
        var (icon, tint) = s_kinds[(int)kind];
        var mark = new EditorIcon(icon) { Width = SmallIcon, Height = SmallIcon };
        TextElement.SetForeground(mark, tint);
        _statusIcon.Content = mark;
        _status.Text = text;
    }

    /// <summary>Counts errors logged while Output is shut. An open Output shows them already.</summary>
    internal void AddErrors(int count)
    {
        if (!OutputOpen)
        {
            ShowErrors(_errors + count);
        }
    }

    /// <summary>
    /// Opens the Output panel for the editor's own reasons, such as a game starting. The pin stops it, and a panel
    /// the user opened stays.
    /// </summary>
    internal void OpenOutputUnlessPinned()
    {
        if (_pin.IsChecked != true && _open < 0)
        {
            Open(0);
        }
    }

    /// <summary>Opens the Debugger panel, for a game that stopped. A stop is asked for, so the pin does not hold it back.</summary>
    internal void OpenDebugger() => Open(1);

    /// <summary>The index of the panel now open, or -1 when every panel is shut.</summary>
    internal int OpenPanel => _open;

    /// <summary>Opens the panel at this index, or shuts every panel for -1. One panel is open at a time. Opening Output reads its errors, so the badge goes.</summary>
    internal void Open(int index)
    {
        _open = index;
        if (index == 0)
        {
            ShowErrors(0);
        }

        for (var at = 0; at < _tabs.Length; at++)
        {
            _tabs[at].Foreground = at == index ? EditorBrushes.Text : EditorBrushes.TextDim;
            _tabs[at].Background = at == index ? EditorBrushes.Hover : Brushes.Transparent;
        }

        PanelChanged?.Invoke(index);
    }

    void ShowErrors(int count)
    {
        _errors = count;
        _unreadCount.Text = count.ToString(CultureInfo.InvariantCulture);
        _unread.IsVisible = count > 0;
    }

    static Button Tab((string Name, string Icon) panel)
    {
        return new Button
        {
            Height = TabHeight,
            Padding = new Thickness(10, 0),
            BorderThickness = new Thickness(0),
            CornerRadius = EditorTheme.Corners,
            MinWidth = 0,
            MinHeight = 0,
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            FontWeight = FontWeight.Medium,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    new EditorIcon(panel.Icon) { Width = SmallIcon, Height = SmallIcon, VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock { Text = panel.Name, VerticalAlignment = VerticalAlignment.Center },
                },
            },
        };
    }
}

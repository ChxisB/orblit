using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The log of the engine and of the games it ran, on an island of its own. The strip holds the title, a filter, a
/// chip for each level with its count, Copy all and Clear. Each line shows the time it came, its level and, for a
/// fault, the file it came from. The panel only shows the lines it is given and asks the window to copy or clear them.
/// </summary>
internal sealed class OutputPanel : Border
{
    // shortcut: a fixed height with no drag handle on its top edge. Add the drag when a panel needs more room.
    internal const double PanelHeight = 200;

    internal const double StripHeight = 36;

    const double FilterWidth = 180;
    const double BoxHeight = 28;
    const double LineIcon = 13;
    const double StampWidth = 64;

    /// <summary>The class of the time on each line.</summary>
    internal const string Stamp = "stamp";

    // The mark, its tint, the text's tint and the chip's tip of each level, in the order of LogLevel. A list of
    // cases, so it is data. Plain messages keep the panel's text colour.
    static readonly (string Icon, IBrush Tint, IBrush? Text, string Tip)[] s_levels =
    [
        ("FaultCircle", EditorBrushes.Fault, EditorBrushes.FaultText, "Errors"),
        ("WarnTriangle", EditorBrushes.Warn, EditorBrushes.Warn, "Warnings"),
        ("InfoCircle", EditorBrushes.TextDim, null, "Messages"),
    ];

    readonly ListBox _list = new()
    {
        Background = null,
        BorderThickness = new Thickness(0),
        FontFamily = EditorTheme.Mono,
        // The list builds a row with no entry while it recycles one.
        ItemTemplate = new FuncDataTemplate<LogEntry>((entry, _) => entry is null ? null : Row(entry)),
        Styles =
        {
            new Style(x => x.OfType<ListBoxItem>())
            {
                Setters = { new Setter(TemplatedControl.PaddingProperty, new Thickness(12, 3)), new Setter(Layoutable.MinHeightProperty, 0.0) },
            },
        },
    };
    readonly TextBox _filter = new()
    {
        PlaceholderText = "Filter messages",
        Width = FilterWidth,
        Height = BoxHeight,
        MinHeight = 0,
        VerticalAlignment = VerticalAlignment.Center,
        InnerRightContent = Parts.SearchIcon(),
    };
    readonly Chip[] _chips = [.. s_levels.Select(level => new Chip(level.Icon, level.Tint, level.Text, level.Tip))];
    readonly Button _copy = Parts.IconButton("ActionCopy", "Copy all");
    readonly Button _clear = Parts.IconButton("Clear", "Clear Output");
    // The time each line came, kept in step with the lines. The engine writes no time of its own.
    readonly List<DateTimeOffset> _times = [];
    IReadOnlyList<string> _lines = [];
    List<LogEntry> _entries = [];
    bool _refreshPending;

    public OutputPanel()
    {
        Height = PanelHeight;
        Classes.Add(EditorTheme.Island);
        _clear.Click += (_, _) => ClearRequested?.Invoke();
        _copy.Click += (_, _) => CopyRequested?.Invoke(string.Join('\n', _lines));
        // TextChanged waits for a render, so the property is the one that fires at once.
        _filter.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                ShowEntries();
            }
        };
        foreach (var chip in _chips)
        {
            chip.IsCheckedChanged += (_, _) => ShowEntries();
        }

        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        tools.Children.Add(_filter);
        tools.Children.AddRange(_chips);
        tools.Children.Add(Parts.Rule());
        tools.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { _copy, _clear } });
        DockPanel.SetDock(tools, Dock.Right);
        Strip = new Border
        {
            Height = StripHeight,
            Padding = new Thickness(12, 0, 6, 0),
            BorderBrush = EditorBrushes.Line,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = new DockPanel
            {
                Children = { tools, new TextBlock { Text = "Output", FontSize = 12, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center } },
            },
        };
        DockPanel.SetDock(Strip, Dock.Top);
        Child = new DockPanel { Children = { Strip, _list } };
    }

    /// <summary>Raised when the clear button is pressed. The panel has not changed, it follows the lines.</summary>
    public event Action? ClearRequested;

    /// <summary>Raised with every line, as the engine wrote them, when Copy all is pressed.</summary>
    public event Action<string>? CopyRequested;

    /// <summary>Raised with the number of errors in lines that just arrived, so the bar can say there are some.</summary>
    public event Action<int>? ErrorsAdded;

    internal ListBox List => _list;

    internal Border Strip { get; }

    internal TextBox FilterBox => _filter;

    /// <summary>The error, warning and info chips, in that order.</summary>
    internal IReadOnlyList<Chip> Chips => _chips;

    internal Button CopyButton => _copy;

    internal Button ClearButton => _clear;

    /// <summary>Shows these lines and keeps showing them as they change, scrolled to the newest, each with the time it came.</summary>
    public void Show(ReadOnlyObservableCollection<string> lines, TimeProvider time)
    {
        _lines = lines;
        _times.AddRange(lines.Select(_ => time.GetLocalNow()));
        ((INotifyCollectionChanged)lines).CollectionChanged += (_, e) =>
        {
            KeepTimes(e, time.GetLocalNow());
            var errors = e.NewItems is { } added ? LogEntry.Read([.. added.Cast<string>()]).Count(entry => entry.Level == LogLevel.Error) : 0;
            if (errors > 0)
            {
                ErrorsAdded?.Invoke(errors);
            }

            RefreshSoon();
        };
        Refresh();
    }

    // The log adds lines at the end, drops them from the front and clears, which are the changes followed here.
    void KeepTimes(NotifyCollectionChangedEventArgs e, DateTimeOffset now)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                _times.InsertRange(e.NewStartingIndex, Enumerable.Repeat(now, e.NewItems!.Count));
                break;
            case NotifyCollectionChangedAction.Remove:
                _times.RemoveRange(e.OldStartingIndex, e.OldItems!.Count);
                break;
            default:
                _times.Clear();
                _times.AddRange(_lines.Select(_ => now));
                break;
        }
    }

    // The log grows a line at a time, so one refresh reads a whole batch.
    void RefreshSoon()
    {
        if (_refreshPending)
        {
            return;
        }

        _refreshPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _refreshPending = false;
            Refresh();
        }, DispatcherPriority.Background);
    }

    // shortcut: each batch reads the whole log again, at most OutputLog.MaxLines lines. Read only the added lines if a
    // busy game makes the panel slow.
    void Refresh()
    {
        _entries = LogEntry.Read(_lines, _times);
        for (var at = 0; at < _chips.Length; at++)
        {
            _chips[at].Count.Text = _entries.Count(entry => entry.Level == (LogLevel)at).ToString(CultureInfo.InvariantCulture);
        }

        ShowEntries();
    }

    void ShowEntries()
    {
        var filter = (_filter.Text ?? "").Trim();
        var shown = _entries.Where(entry => _chips[(int)entry.Level].IsChecked == true && Matches(entry, filter)).ToList();
        _list.ItemsSource = shown;
        // After the layout that measures the new rows. Before it the scroll stops at the old end.
        Dispatcher.UIThread.Post(() => _list.ScrollIntoView(shown.Count - 1), DispatcherPriority.Background);
    }

    static bool Matches(LogEntry entry, string filter) =>
        entry.Text.Contains(filter, StringComparison.OrdinalIgnoreCase) || entry.Source.Contains(filter, StringComparison.OrdinalIgnoreCase);

    // The time, the level's mark, the text, and the place the engine named on the right. A long line wraps.
    static DockPanel Row(LogEntry entry)
    {
        var (icon, tint, text, _) = s_levels[(int)entry.Level];
        var stamp = new TextBlock
        {
            Text = entry.Time.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            Width = StampWidth,
            Foreground = EditorBrushes.Disabled,
            Classes = { Stamp },
        };
        var mark = new EditorIcon(icon) { Width = LineIcon, Height = LineIcon, Margin = new Thickness(0, 2, 8, 0), VerticalAlignment = VerticalAlignment.Top };
        TextElement.SetForeground(mark, tint);
        DockPanel.SetDock(stamp, Dock.Left);
        DockPanel.SetDock(mark, Dock.Left);
        var row = new DockPanel { Children = { stamp, mark } };
        if (entry.Source != "")
        {
            var source = new TextBlock { Text = entry.Source, FontSize = 11, Foreground = EditorBrushes.TextDim, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };
            DockPanel.SetDock(source, Dock.Right);
            row.Children.Add(source);
        }

        var message = new TextBlock { Text = entry.Text, TextWrapping = TextWrapping.Wrap };
        if (text is not null)
        {
            message.Foreground = text;
        }

        row.Children.Add(message);
        return row;
    }

    /// <summary>A level's switch: its mark and how many lines it has. It is lit while the level's lines show.</summary>
    internal sealed class Chip : ToggleButton
    {
        const double ChipHeight = 24;
        const double ChipIcon = 12;

        public Chip(string icon, IBrush tint, IBrush? text, string tip)
        {
            var mark = new EditorIcon(icon) { Width = ChipIcon, Height = ChipIcon, VerticalAlignment = VerticalAlignment.Center };
            TextElement.SetForeground(mark, tint);
            Count = new TextBlock { Text = "0", FontFamily = EditorTheme.Mono, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
            if (text is not null)
            {
                Count.Foreground = text;
            }

            Classes.Add(EditorTheme.TextButton);
            Height = ChipHeight;
            MinHeight = 0;
            MinWidth = 0;
            Padding = new Thickness(9, 0);
            VerticalAlignment = VerticalAlignment.Center;
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Children = { mark, Count } };
            IsChecked = true;
            IsCheckedChanged += (_, _) => Opacity = IsChecked == true ? 1 : 0.4;
            ToolTip.SetTip(this, tip);
        }

        internal TextBlock Count { get; }

        // Drawn as a worded button, so the chip shares the raised look of the other buttons.
        protected override Type StyleKeyOverride => typeof(Button);
    }
}

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The Asset Store workspace: a card for each kit the store's index lists, a search over them and a drawer with
/// the chosen kit's details. It reads the index when Reload is pressed. It names kits and does not install them,
/// so Install and the Installed and Updates filters wait on an installer.
/// </summary>
internal sealed class StoreView : Border
{
    const double CardWidth = 250;
    // Half the 14 pixel gap between cards, as a margin on every side of each.
    const double Half = 7;

    readonly Func<CancellationToken, Task<string>> _read;
    readonly Button _reload = new() { Classes = { EditorTheme.TextButton, EditorTheme.Small } };
    readonly TextBlock _status = new() { Text = "Press Reload to read the store.", FontSize = 12, Foreground = EditorBrushes.TextDim };
    readonly TextBox _search = new() { Width = 260, PlaceholderText = "Search kits", InnerLeftContent = Parts.FieldIcon("Lens", EditorBrushes.TextDim), Classes = { EditorTheme.Field } };
    readonly Segments _filter = new("All", "Installed", "Updates");
    readonly UniformGrid _cards = new() { Margin = new Thickness(18 - Half, 12 - Half, 18 - Half, 18 - Half) };
    readonly Decorator _note = new() { Margin = new Thickness(0, 80, 0, 0) };
    readonly Border _drawer = new() { Classes = { EditorTheme.Island }, Width = 340, Margin = new Thickness(6, 0, 0, 0), IsVisible = false };
    readonly Decorator _drawerBanner = new();
    readonly TextBlock _drawerName = new() { FontSize = 15, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _drawerBy = new() { FontSize = 12, Foreground = EditorBrushes.TextDim };
    readonly TextBlock _drawerSummary = new() { LineHeight = 20, TextWrapping = TextWrapping.Wrap };
    readonly TextBlock _version = Fact();
    readonly TextBlock _publisher = Fact();
    readonly TextBlock _slug = Fact();
    readonly Button _close = Parts.IconButton("Close", "Close the details");
    IReadOnlyList<KitEntry> _kits = [];
    KitEntry? _chosen;
    bool _readOnce;
    bool _loading;

    // shortcut: the index is read when Reload is pressed and not on first show, so a tab click sends no request. Read it on first show once the store lists kits.
    public StoreView(Func<CancellationToken, Task<string>> read)
    {
        _read = read;
        _reload.Content = Parts.Worded("Reload", "Reload");
        _reload.Click += async (_, _) => await LoadAsync();
        foreach (var choice in _filter.Choices.Skip(1))
        {
            Unbuilt.Disable(choice);
        }
        // The property changes as the text is set. The TextChanged event waits for a render.
        _search.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                ShowCards();
            }
        };
        _close.Click += (_, _) => Choose(null);
        _drawer.Child = DrawerBody();
        Classes.Add(EditorTheme.Island);
        ClipToBounds = true;
        Child = Kits();
        ShowCards();
    }

    internal Button ReloadButton => _reload;

    internal TextBlock Status => _status;

    internal TextBox SearchBox => _search;

    internal Segments Filter => _filter;

    /// <summary>The kits the cards show, in the index's order.</summary>
    internal IEnumerable<KitEntry> Shown => _cards.Children.OfType<Control>().Select(card => card.Tag).OfType<KitEntry>();

    /// <summary>The kit cards, each tagged with its kit, and the Publish card last.</summary>
    internal IEnumerable<Control> Cards => _cards.Children;

    /// <summary>The kit's details. The window puts it beside the bottom bar, as tall as the column.</summary>
    internal Border Drawer => _drawer;

    internal Button CloseButton => _close;

    /// <summary>The drawer's name, publisher, version and slug, in the order they show.</summary>
    internal string[] Details => [_drawerName.Text!, _drawerBy.Text!, _version.Text!, _publisher.Text!, _slug.Text!];

    /// <summary>
    /// Reads the index and shows its kits in place of the cards before. A failure is said in the status and the
    /// cards before stay. A press while a read is running does nothing.
    /// </summary>
    public async Task LoadAsync()
    {
        if (_loading)
        {
            return;
        }

        _loading = true;
        _reload.IsEnabled = false;
        _status.Text = "Reading the store.";
        try
        {
            _kits = KitIndex.Parse(await _read(CancellationToken.None));
            _readOnce = true;
            _chosen = null;
            _status.Text = _kits.Count switch
            {
                0 => "No kits in the index.",
                1 => "1 kit in the index.",
                var count => $"{count} kits in the index.",
            };
            ShowCards();
        }
        catch (Exception error) when (error is IOException or FormatException)
        {
            _status.Text = error.Message;
        }
        finally
        {
            _loading = false;
            _reload.IsEnabled = true;
        }
    }

    /// <summary>Opens the drawer on a kit, as a click on its card does, or closes it for none.</summary>
    public void Choose(KitEntry? kit)
    {
        _chosen = kit;
        MarkChosen();
        if (kit is null)
        {
            return;
        }
        _drawerBanner.Child = Parts.Banner("AssetStore", Tone(kit), 190);
        _drawerName.Text = kit.Name;
        _drawerBy.Text = $"by {kit.Publisher}";
        _drawerSummary.Text = kit.Summary.Length > 0 ? kit.Summary : "No summary.";
        _drawerSummary.Foreground = kit.Summary.Length > 0 ? EditorBrushes.TextSoft : EditorBrushes.Disabled;
        _version.Text = kit.Version;
        _publisher.Text = kit.Publisher;
        _slug.Text = kit.Slug;
    }

    // The chosen kit's card has the accent edge and a soft ring, and the drawer shows while a kit is chosen.
    void MarkChosen()
    {
        _drawer.IsVisible = _chosen is not null;
        foreach (var card in _cards.Children.OfType<Border>())
        {
            var chosen = Equals(card.Tag, _chosen);
            card.BorderBrush = chosen ? EditorBrushes.Accent : EditorBrushes.Line;
            card.BoxShadow = chosen ? BoxShadows.Parse("0 0 0 3 #29E5893F") : default;
        }
    }

    DockPanel Kits()
    {
        var title = new StackPanel { Children = { new TextBlock { Text = "Asset Store", FontSize = 20, FontWeight = FontWeight.SemiBold }, _status } };
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Children = { _search, _filter, _reload } };
        DockPanel.SetDock(tools, Dock.Right);
        var header = new DockPanel { Margin = new Thickness(18, 16, 18, 6), Children = { tools, title } };
        DockPanel.SetDock(header, Dock.Top);
        var scroll = new ScrollViewer { Content = new StackPanel { Children = { _cards, _note } } };
        // The cards fill the row: as many 250 pixel columns as fit, each stretched to share what is left.
        scroll.SizeChanged += (_, e) => _cards.Columns = Math.Max(1, (int)((e.NewSize.Width - _cards.Margin.Left - _cards.Margin.Right) / (CardWidth + 2 * Half)));
        return new DockPanel { Children = { header, scroll } };
    }

    // A kit's place in the whole index picks its banner tone.
    int Tone(KitEntry kit) => Math.Max(0, _kits.ToList().IndexOf(kit));

    void ShowCards()
    {
        var words = (_search.Text ?? "").Trim();
        var shown = _kits.Where(kit => words.Length == 0 || Matches(kit, words)).ToList();
        _cards.Children.Clear();
        _cards.Children.AddRange(shown.Select(Card));
        _cards.Children.Add(PublishCard());
        MarkChosen();
        _note.Child = shown.Count > 0 ? null
            : !_readOnce ? Parts.EmptyState("AssetStore", "Nothing read yet", "Reload reads the store's index of kits.")
            : _kits.Count == 0 ? Parts.EmptyState("AssetStore", "No kits yet", "The store's index lists no kits.")
            : Parts.EmptyState("Search", "Nothing matches", "No kit's name, publisher or summary has these words.");
    }

    static bool Matches(KitEntry kit, string words) =>
        new[] { kit.Name, kit.Publisher, kit.Summary }.Any(text => text.Contains(words, StringComparison.OrdinalIgnoreCase));

    Border Card(KitEntry kit)
    {
        var install = new Button { Content = "Install", HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Classes = { EditorTheme.TextButton, EditorTheme.Primary, EditorTheme.Small } };
        var page = Parts.IconButton("ExternalLink", "Open the kit page");
        page.VerticalAlignment = VerticalAlignment.Center;
        Unbuilt.Disable(install);
        Unbuilt.Disable(page);
        DockPanel.SetDock(page, Dock.Right);
        var version = Badge(kit.Version);
        DockPanel.SetDock(version, Dock.Right);
        var name = new TextBlock { Text = kit.Name, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        var body = new StackPanel
        {
            Spacing = 6,
            Margin = new Thickness(14, 12, 14, 14),
            Children =
            {
                new DockPanel { Children = { version, name } },
                new TextBlock { Text = $"by {kit.Publisher}", FontSize = 11, Foreground = EditorBrushes.TextDim },
                Summary(kit),
                new DockPanel { Children = { page, install } },
            },
        };
        var card = new Border
        {
            Tag = kit,
            Classes = { EditorTheme.Island },
            Margin = new Thickness(Half),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new StackPanel { Children = { Parts.Banner("AssetStore", Tone(kit), 132), body } },
        };
        card.Tapped += (_, _) => Choose(kit);
        return card;
    }

    static TextBlock Summary(KitEntry kit) => new()
    {
        Text = kit.Summary.Length > 0 ? kit.Summary : "No summary.",
        FontSize = 12,
        MinHeight = 36,
        TextWrapping = TextWrapping.Wrap,
        Foreground = kit.Summary.Length > 0 ? EditorBrushes.TextSoft : EditorBrushes.Disabled,
    };

    static Panel PublishCard()
    {
        var learn = new Button { Content = Parts.Worded("ExternalLink", "Learn how"), HorizontalAlignment = HorizontalAlignment.Center, Classes = { EditorTheme.TextButton, EditorTheme.Ghost, EditorTheme.Small } };
        // No page on the site says how to publish a kit yet.
        Unbuilt.Disable(learn);
        var words = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(18),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = "Publish your own kit", FontSize = 12, Foreground = EditorBrushes.TextSoft, HorizontalAlignment = HorizontalAlignment.Center },
                new TextBlock { Text = "Pack a folder of your project as a kit for the index.", FontSize = 11, MaxWidth = 200, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Foreground = EditorBrushes.TextDim },
                learn,
            },
        };
        var edge = new Rectangle { Stroke = EditorBrushes.LineStrong, StrokeThickness = 1, StrokeDashArray = [4, 3], RadiusX = 10, RadiusY = 10 };
        return new Panel { MinHeight = 250, Margin = new Thickness(Half), Children = { edge, words } };
    }

    StackPanel DrawerBody()
    {
        var install = new Button { Content = "Install", Classes = { EditorTheme.TextButton, EditorTheme.Primary } };
        var page = new Button { Content = Parts.Worded("ExternalLink", "Open page"), Classes = { EditorTheme.TextButton, EditorTheme.Ghost } };
        Unbuilt.Disable(install);
        Unbuilt.Disable(page);
        DockPanel.SetDock(_close, Dock.Right);
        var facts = new Border
        {
            BorderBrush = EditorBrushes.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Child = new StackPanel { Children = { FactRow("Version", _version, false), FactRow("Publisher", _publisher, true), FactRow("Slug", _slug, true) } },
        };
        var body = new StackPanel
        {
            Spacing = 12,
            Margin = new Thickness(16),
            Children =
            {
                new StackPanel { Spacing = 4, Children = { new DockPanel { Children = { _close, _drawerName } }, _drawerBy } },
                _drawerSummary,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { install, page } },
                facts,
            },
        };
        return new StackPanel { Children = { _drawerBanner, body } };
    }

    static TextBlock Fact() => new() { FontFamily = EditorTheme.Mono, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };

    static Border FactRow(string name, TextBlock value, bool lineAbove)
    {
        DockPanel.SetDock(value, Dock.Right);
        return new Border
        {
            Height = 34,
            Padding = new Thickness(12, 0),
            BorderBrush = EditorBrushes.Line,
            BorderThickness = new Thickness(0, lineAbove ? 1 : 0, 0, 0),
            Child = new DockPanel { Children = { value, new TextBlock { Text = name, FontSize = 12, Foreground = EditorBrushes.TextDim, VerticalAlignment = VerticalAlignment.Center } } },
        };
    }

    static Border Badge(string text) => new()
    {
        Padding = new Thickness(6, 2),
        CornerRadius = new CornerRadius(999),
        Background = EditorBrushes.Line,
        VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock { Text = text, FontFamily = EditorTheme.Mono, FontSize = 10.5, FontWeight = FontWeight.SemiBold, Foreground = EditorBrushes.TextSoft },
    };
}

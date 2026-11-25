using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>The small controls that more than one area of the shell uses.</summary>
internal static class Parts
{
    // A glow and a deep tone for each banner, taken in turn so an item keeps its colour as a search narrows the list.
    static readonly (Color Glow, Color Deep)[] s_banners =
    [
        (Color.Parse("#AAFF9A4D"), Color.Parse("#C2410C")),
        (Color.Parse("#AA5AA2FF"), Color.Parse("#1E3A8A")),
        (Color.Parse("#AA8BD450"), Color.Parse("#166534")),
    ];

    public static Button IconButton(string icon, string tip) => Flat(new Button(), icon, tip);

    public static ToggleButton IconToggle(string icon, string tip) => Flat(new ToggleButton(), icon, tip);

    /// <summary>The magnifier at the right of a filter box. Centered, because a stretched icon draws squashed.</summary>
    public static EditorIcon SearchIcon() => new("Search") { Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };

    /// <summary>
    /// The icon of a tree row: pale, 7 pixels clear of the name, and a tint where the row has one. It is
    /// centered, because a row with an eye button is taller than the icon and a stretched icon draws squashed.
    /// </summary>
    public static EditorIcon RowIcon(string name, IBrush? tint = null)
    {
        var icon = new EditorIcon(name) { Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
        TextElement.SetForeground(icon, tint ?? EditorBrushes.TreeIcon);
        return icon;
    }

    /// <summary>A small rounded tag in mono type, such as a version or a kind.</summary>
    public static Border Badge(string text) => new()
    {
        Padding = new Thickness(6, 2),
        CornerRadius = new CornerRadius(999),
        Background = EditorBrushes.Line,
        VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock { Text = text, FontFamily = EditorTheme.Mono, FontSize = 10.5, FontWeight = FontWeight.SemiBold, Foreground = EditorBrushes.TextSoft },
    };

    /// <summary>A row of controls on a small island of its own, 2 pixels apart with 3 pixels around them.</summary>
    public static Border Pill(IEnumerable<Control> items)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        row.Children.AddRange(items);
        return new Border { Classes = { EditorTheme.Island }, Padding = new Thickness(3), VerticalAlignment = VerticalAlignment.Center, Child = row };
    }

    /// <summary>
    /// A pill that floats 10 pixels in from an edge of the viewport, on a see-through fill with a stronger edge and
    /// a shadow. It does not clip, so the shadow shows.
    /// </summary>
    public static Border Float(IEnumerable<Control> items)
    {
        var pill = Pill(items);
        pill.Margin = new Thickness(10);
        pill.Background = EditorBrushes.Floating;
        pill.BorderBrush = EditorBrushes.LineStrong;
        pill.BoxShadow = BoxShadows.Parse("0 6 20 0 #59000000");
        pill.ClipToBounds = false;
        return pill;
    }

    /// <summary>A thin upright line between groups in a pill, 18 high and 3 pixels clear on each side.</summary>
    public static Border Rule() => new() { Width = 1, Height = 18, Margin = new Thickness(3, 0), Background = EditorBrushes.LineStrong };

    /// <summary>A button 24 pixels square: the icon and 4 pixels of padding, no border.</summary>
    public static Button Tight(Button button)
    {
        button.Padding = new Thickness(4);
        button.BorderThickness = new Thickness(0);
        return button;
    }

    /// <summary>A field with its name over it in small dim text, and the lines that go under it, such as a fault.</summary>
    public static StackPanel Labeled(string label, params Control[] rows)
    {
        var group = new StackPanel { Spacing = 4 };
        group.Children.Add(new TextBlock { Text = label, FontSize = 11, Foreground = EditorBrushes.TextDim });
        group.Children.AddRange(rows);
        return group;
    }

    /// <summary>The 14 pixel mark at the left of a field, such as a folder.</summary>
    public static EditorIcon FieldIcon(string name, IBrush tint)
    {
        var icon = new EditorIcon(name) { Width = 14, Height = 14, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        TextElement.SetForeground(icon, tint);
        return icon;
    }

    /// <summary>A key on a small gray cap, as a hint beside the thing it runs.</summary>
    public static Border Kbd(string key) => new()
    {
        Padding = new Thickness(5, 3),
        CornerRadius = new CornerRadius(4),
        Background = EditorBrushes.Line,
        VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock { Text = key, FontFamily = EditorTheme.Mono, FontSize = 10.5, Foreground = EditorBrushes.TextSoft },
    };

    /// <summary>A worded button that names the key which runs it, on a shade of the button's fill in the button's own text colour.</summary>
    public static StackPanel Keyed(string word, string key) => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 6,
        Children =
        {
            new TextBlock { Text = word, VerticalAlignment = VerticalAlignment.Center },
            new Border
            {
                Padding = new Thickness(5, 3),
                CornerRadius = new CornerRadius(4),
                Background = EditorBrushes.Shade,
                Opacity = 0.7,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = key, FontFamily = EditorTheme.Mono, FontSize = 10.5 },
            },
        },
    };

    /// <summary>A key as the macOS menus write it: the modifiers as marks in their fixed order, then the key.</summary>
    // shortcut: the macOS marks only. Add the Windows and Linux words when the editor ships there.
    public static string KeyText(KeyGesture gesture)
    {
        string Mark(KeyModifiers modifier, string mark) => gesture.KeyModifiers.HasFlag(modifier) ? mark : "";

        var key = gesture.Key switch
        {
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            Key.OemQuestion when gesture.KeyModifiers.HasFlag(KeyModifiers.Shift) => "?",
            Key.OemQuestion => "/",
            Key.OemPipe => "\\",
            >= Key.D0 and <= Key.D9 => $"{gesture.Key - Key.D0}",
            _ => gesture.Key.ToString(),
        };
        // A question mark already holds its shift.
        var shift = key == "?" ? "" : Mark(KeyModifiers.Shift, "⇧");
        return Mark(KeyModifiers.Control, "⌃") + Mark(KeyModifiers.Alt, "⌥") + shift + Mark(KeyModifiers.Meta, "⌘") + key;
    }

    /// <summary>The line under a field that says why its value cannot be used. It waits hidden until there is something to say.</summary>
    public static TextBlock FaultLine() => new() { FontSize = 11, Foreground = EditorBrushes.FaultText, TextWrapping = TextWrapping.Wrap, IsVisible = false };

    /// <summary>A heading that names a group of a menu. It is off, so it takes no hover and no click.</summary>
    public static MenuItem MenuHeading(string group) => new()
    {
        IsEnabled = false,
        Header = new TextBlock { Text = group.ToUpperInvariant(), FontSize = 10.5, FontWeight = FontWeight.SemiBold, LetterSpacing = 0.6, Foreground = EditorBrushes.Unit },
    };

    /// <summary>
    /// A menu item with its key. A note puts a dim line under the name that says what the item does. The item
    /// carries its name for a screen reader, because a noted header is not plain text.
    /// </summary>
    public static MenuItem ActionItem(string header, Action run, KeyGesture? key = null, string? note = null)
    {
        var item = new MenuItem { Header = note is null ? header : Noted(header, note), InputGesture = key };
        AutomationProperties.SetName(item, header);
        item.Click += (_, _) => run();
        return item;
    }

    /// <summary>
    /// An item that presses the button it stands for, so the menus and a key run the same code. The item is on
    /// while the button is on.
    /// </summary>
    public static MenuItem PressItem(string header, Button button, KeyGesture? key = null, string? note = null)
    {
        var item = ActionItem(header, () => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)), key, note);
        item.Bind(InputElement.IsEnabledProperty, button.GetObservable(InputElement.IsEnabledProperty));
        return item;
    }

    /// <summary>
    /// A menu item's header: its name with the engine's name beside it in small dim mono, and its note in a dim
    /// line under them. Each part shows only when it is given.
    /// </summary>
    internal static StackPanel Noted(string name, string? note, string? engine = null)
    {
        var top = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new TextBlock { Text = name } } };
        if (engine is not null)
        {
            top.Children.Add(new TextBlock { Text = engine, FontFamily = EditorTheme.Mono, FontSize = 10.5, Foreground = EditorBrushes.Unit, VerticalAlignment = VerticalAlignment.Center });
        }

        var header = new StackPanel { Spacing = 1, Children = { top } };
        if (note is not null)
        {
            header.Children.Add(new TextBlock { Text = note, FontSize = 11, Foreground = EditorBrushes.TextDim });
        }

        return header;
    }

    /// <summary>An island's top strip, like a dock's: a row with 6 pixels around it and a line under it. Items dock left unless <see cref="Right"/> docked them.</summary>
    public static Border Bar(params Control[] items)
    {
        var row = new DockPanel { LastChildFill = false };
        foreach (var item in items)
        {
            row.Children.Add(item);
        }

        return new Border
        {
            Height = DockTabs.StripHeight,
            Padding = new Thickness(6),
            BorderBrush = EditorBrushes.Line,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = row,
        };
    }

    /// <summary>Docks an item of a <see cref="Bar"/> at its right end.</summary>
    public static Control Right(Control control)
    {
        DockPanel.SetDock(control, Dock.Right);
        return control;
    }

    /// <summary>What a tab with nothing to show says: a dim mark, a line on what fills it, and a line on how.</summary>
    public static StackPanel EmptyState(string icon, string title, string line)
    {
        var mark = new EditorIcon(icon) { Width = 24, Height = 24 };
        TextElement.SetForeground(mark, EditorBrushes.Disabled);
        return new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(18, 26),
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                mark,
                new TextBlock { Text = title, FontSize = 12, Foreground = EditorBrushes.TextSoft, HorizontalAlignment = HorizontalAlignment.Center },
                new TextBlock { Text = line, FontSize = 11, Foreground = EditorBrushes.TextDim, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center },
            },
        };
    }

    // No border, so the box is the icon and its padding: 26 pixels square.
    static T Flat<T>(T button, string icon, string tip) where T : ContentControl
    {
        button.Content = new EditorIcon(icon);
        button.Padding = new Thickness(5);
        button.BorderThickness = new Thickness(0);
        button.MinWidth = 0;
        button.MinHeight = 0;
        ToolTip.SetTip(button, tip);
        return button;
    }

    // The banner sits on the card's top edge, so it rounds its own top corners in place of a clip that would cut the ring.
    public static Border Banner(string icon, int tone, double height)
    {
        var (glow, deep) = s_banners[tone % s_banners.Length];
        var top = new CornerRadius(9, 9, 0, 0);
        var mark = new EditorIcon(icon) { Width = 36, Height = 36, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        TextElement.SetForeground(mark, new ImmutableSolidColorBrush(Color.Parse("#E6FFFFFF")));
        return new Border
        {
            Height = height,
            CornerRadius = top,
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(deep, 0), new GradientStop(EditorColors.Panel, 1) },
            },
            Child = new Border
            {
                CornerRadius = top,
                Background = new RadialGradientBrush
                {
                    Center = new RelativePoint(0.2, 0, RelativeUnit.Relative),
                    GradientOrigin = new RelativePoint(0.2, 0, RelativeUnit.Relative),
                    RadiusX = new RelativeScalar(1.2, RelativeUnit.Relative),
                    RadiusY = new RelativeScalar(0.9, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(glow, 0), new GradientStop(Colors.Transparent, 0.6) },
                },
                Child = mark,
            },
        };
    }

    /// <summary>The logo's round mark, in its own two colors. Nothing else in the editor draws them.</summary>
    public static Ellipse LogoMark(double size) => new()
    {
        Width = size,
        Height = size,
        VerticalAlignment = VerticalAlignment.Center,
        Fill = new LinearGradientBrush
        {
            StartPoint = RelativePoint.TopLeft,
            EndPoint = RelativePoint.BottomRight,
            GradientStops = { new GradientStop(Color.Parse("#FFC21A"), 0), new GradientStop(Color.Parse("#FF6A00"), 1) },
        },
    };

    /// <summary>An icon and a word, for the content of a text button.</summary>
    public static StackPanel Worded(string icon, string text) => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 6,
        Children = { new EditorIcon(icon) { Width = 14, Height = 14, VerticalAlignment = VerticalAlignment.Center }, new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center } },
    };
}

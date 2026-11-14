using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The bar under the viewport. Panel tabs on the left. On the right the status line in the slot the old editor
/// gives its toasts, a rule, the version and the pin. Only the Output tab opens a panel, the others answer that
/// theirs is not built.
/// </summary>
internal sealed class BottomBar : Grid
{
    // shortcut: copied from the engine's version file, read it through the bridge when it gains a version call.
    internal const string Version = "1.0.stable";

    const double TabHeight = 26;
    const double SmallIcon = 13;

    // Name and icon file.
    static readonly (string Name, string Icon)[] Panels =
    [
        ("Output", "Output"),
        ("Debugger", "Debug"),
        ("Audio", "AudioStreamPlayer"),
        ("Animation", "Animation"),
        ("Shader Editor", "ShaderDock"),
    ];

    readonly Button[] _tabs;
    // Neutral, because the line also carries failures and a tick would call them done.
    readonly EditorIcon _statusIcon = new("Info") { Width = SmallIcon, Height = SmallIcon, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
    readonly Border _separator = Parts.Rule();
    readonly Button _version = Unbuilt.Mark(
        new Button
        {
            Padding = new Thickness(4, 0),
            BorderThickness = new Thickness(0),
            MinWidth = 0,
            MinHeight = 0,
            FontSize = 11,
            Foreground = EditorBrushes.TextDim,
            VerticalAlignment = VerticalAlignment.Center,
            Content = new TextBlock { Text = Version },
        },
        "Copying the version is not built yet.");
    // The pin keeps the game run from opening a panel on its own. FixedToggle never flips itself, so a click does it here.
    readonly FixedToggle _pin = new("Pin", "Pin the bottom panel", false) { Margin = new Thickness(2, 0, 0, 0) };
    bool _outputOpen;

    public BottomBar(TextBlock status)
    {
        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto");
        _tabs = [.. Panels.Select((panel, index) => index == 0 ? Tab(panel, EditorBrushes.TextDim) : Unbuilt.Mark(Tab(panel, EditorBrushes.Unbuilt), $"The {panel.Name} panel is not built yet."))];
        OutputTab.Click += (_, _) => OutputOpen = !OutputOpen;
        _pin.Click += (_, _) => _pin.IsChecked = _pin.IsChecked != true;
        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        left.Children.AddRange(_tabs);
        var right = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
        status.VerticalAlignment = VerticalAlignment.Center;
        status.FontSize = 12;
        status.Foreground = EditorBrushes.TextSoft;
        TextElement.SetForeground(_statusIcon, EditorBrushes.TextDim);
        _separator.Margin = new Thickness(8, 0);
        right.Children.AddRange([_statusIcon, status, _separator, _version, _pin]);
        SetColumn(right, 2);
        Children.Add(left);
        Children.Add(right);
    }

    /// <summary>Raised with the new state when the Output panel opens or shuts.</summary>
    public event Action<bool>? OutputOpenChanged;

    internal IReadOnlyList<Button> Tabs => _tabs;

    internal Button OutputTab => _tabs[0];

    internal bool OutputOpen
    {
        get => _outputOpen;
        private set
        {
            _outputOpen = value;
            OutputTab.Foreground = value ? EditorBrushes.Text : EditorBrushes.TextDim;
            OutputTab.Background = value ? EditorBrushes.Hover : Brushes.Transparent;
            OutputOpenChanged?.Invoke(value);
        }
    }

    /// <summary>Opens the Output panel for the editor's own reasons, such as a game starting. The pin stops it.</summary>
    internal void OpenOutputUnlessPinned()
    {
        if (_pin.IsChecked != true)
        {
            OutputOpen = true;
        }
    }

    internal Border Separator => _separator;

    internal Button VersionButton => _version;

    internal FixedToggle PinButton => _pin;

    static Button Tab((string Name, string Icon) panel, IBrush foreground) => new()
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
        Foreground = foreground,
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

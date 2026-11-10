using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The bar under the viewport. Panel buttons on the left. On the right a separator, the status line in the
/// slot the old editor gives its toasts, the version and the pin. Only the Output button opens a panel, the
/// others answer that theirs is not built.
/// </summary>
internal sealed class BottomBar : Grid
{
    // shortcut: copied from the engine's version file, read it through the bridge when it gains a version call.
    internal const string Version = "1.0.stable";

    static readonly string[] Panels = ["Output", "Debugger", "Audio", "Animation", "Shader Editor"];

    readonly Button[] _tabs;
    readonly Border _separator = new() { Width = 2, Height = 25, Margin = new Thickness(0, 0, 8, 0), Background = EditorBrushes.BarSeparator };
    readonly Button _version = Unbuilt.Mark(
        new Button
        {
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            MinWidth = 0,
            MinHeight = 0,
            Margin = new Thickness(5, 0, 0, 0),
            // The old bar draws the version dim so it reads as a label.
            Opacity = 0.65,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 11,
                Children = { new EditorIcon("OrblitMonochrome") { VerticalAlignment = VerticalAlignment.Center }, new TextBlock { Text = Version, VerticalAlignment = VerticalAlignment.Center } },
            },
        },
        "Copying the version is not built yet.");
    // The pin keeps the game run from opening a panel on its own. FixedToggle never flips itself, so a click does it here.
    readonly FixedToggle _pin = new("Pin", "Pin the bottom panel", false) { Margin = new Thickness(12, 0, 0, 0) };
    bool _outputOpen;

    public BottomBar(TextBlock status)
    {
        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto");
        _tabs = Panels.Select((name, index) => index == 0 ? Tab(name) : Unbuilt.Mark(Tab(name), $"The {name} panel is not built yet.")).ToArray();
        OutputTab.Click += (_, _) => OutputOpen = !OutputOpen;
        _pin.Click += (_, _) => _pin.IsChecked = _pin.IsChecked != true;
        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        left.Children.AddRange(_tabs);
        var right = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
        status.VerticalAlignment = VerticalAlignment.Center;
        right.Children.AddRange([_separator, status, _version, _pin]);
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
            OutputTab.Foreground = value ? EditorBrushes.Text : EditorBrushes.TabInactive;
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

    static Button Tab(string name)
    {
        Control label = new TextBlock { Text = name };
        if (name == "Output")
        {
            var dot = new Ellipse { Width = 8, Height = 8, Margin = new Thickness(0, 0, 6, 0), Fill = EditorBrushes.OutputDot, VerticalAlignment = VerticalAlignment.Center };
            label = new StackPanel { Orientation = Orientation.Horizontal, Children = { dot, label } };
        }

        return new Button
        {
            Content = label,
            Padding = new Thickness(10, 4),
            BorderThickness = new Thickness(0),
            MinWidth = 0,
            MinHeight = 0,
            Foreground = EditorBrushes.TabInactive,
        };
    }
}

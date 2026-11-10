using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// One name in the workspace switcher. <c>IsChecked</c> turns the name and icon to the accent colour, and a click
/// never changes that. The reference draws no box behind it.
/// </summary>
internal sealed class WorkspaceTab : ToggleButton
{
    public WorkspaceTab(string icon, string name)
    {
        Label = name;
        Resources["ToggleButtonBackgroundChecked"] = Brushes.Transparent;
        Resources["ToggleButtonBackgroundCheckedPointerOver"] = Brushes.Transparent;
        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Children =
            {
                new EditorIcon(icon),
                new TextBlock { Text = name, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center },
            },
        };
    }

    public string Label { get; }

    protected override Type StyleKeyOverride => typeof(ToggleButton);

    protected override void Toggle()
    {
    }
}

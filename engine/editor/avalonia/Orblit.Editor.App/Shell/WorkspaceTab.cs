using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// One name in the workspace switcher. <c>IsChecked</c> lifts the tab, brightens the name and turns the icon to the
/// accent colour, and a click never changes that.
/// </summary>
internal sealed class WorkspaceTab : ToggleButton
{
    const double TabHeight = 26;

    public WorkspaceTab(string icon, string name)
    {
        Label = name;
        Height = TabHeight;
        MinHeight = 0;
        Padding = new Thickness(10, 0);
        Resources["ToggleButtonForeground"] = EditorBrushes.TextDim;
        Resources["ToggleButtonBackgroundChecked"] = EditorBrushes.Lifted;
        Resources["ToggleButtonBackgroundCheckedPointerOver"] = EditorBrushes.Lifted;
        Resources["ToggleButtonForegroundChecked"] = EditorBrushes.Text;
        Resources["ToggleButtonForegroundCheckedPointerOver"] = EditorBrushes.Text;
        Styles.Add(new Style(x => x.OfType<ToggleButton>().Class(":checked").Descendant().OfType<EditorIcon>())
        {
            Setters = { new Setter(TextElement.ForegroundProperty, EditorBrushes.Accent) },
        });
        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                new EditorIcon(icon),
                new TextBlock { Text = name, FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center },
            },
        };
    }

    public string Label { get; }

    protected override Type StyleKeyOverride => typeof(ToggleButton);

    protected override void Toggle()
    {
    }
}

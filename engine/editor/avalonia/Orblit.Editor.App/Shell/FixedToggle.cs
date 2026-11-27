using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>A toolbar icon that shows a mode. <c>IsChecked</c> lights it, and a click never changes that.</summary>
internal sealed class FixedToggle : ToggleButton
{
    // 26 square with the 16 pixel icon, the size of every icon button.
    public FixedToggle(string icon, string tip, bool lit)
    {
        Content = new EditorIcon(icon);
        IsChecked = lit;
        Padding = new Thickness(Parts.IconInset);
        BorderThickness = new Thickness(0);
        MinWidth = 0;
        MinHeight = 0;
        ToolTip.SetTip(this, tip);
    }

    protected override Type StyleKeyOverride => typeof(ToggleButton);

    protected override void Toggle()
    {
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>The small controls that more than one area of the shell uses.</summary>
internal static class Parts
{
    public static Button IconButton(string icon, string tip) => Flat(new Button(), icon, tip);

    public static ToggleButton IconToggle(string icon, string tip) => Flat(new ToggleButton(), icon, tip);

    /// <summary>The magnifier at the right of a filter box. Centred, because a stretched icon draws squashed.</summary>
    public static EditorIcon SearchIcon() => new("Search") { Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };

    /// <summary>
    /// The icon of a tree row: pale, 7 pixels clear of the name, and a tint where the row has one. It is
    /// centred, because a row with an eye button is taller than the icon and a stretched icon draws squashed.
    /// </summary>
    public static EditorIcon RowIcon(string name, IBrush? tint = null)
    {
        var icon = new EditorIcon(name) { Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
        TextElement.SetForeground(icon, tint ?? EditorBrushes.TreeIcon);
        return icon;
    }

    /// <summary>A button 24 pixels square: the icon and 4 pixels of padding, no border.</summary>
    public static Button Tight(Button button)
    {
        button.Padding = new Thickness(4);
        button.BorderThickness = new Thickness(0);
        return button;
    }

    // The theme gives a button no border, so the box is the icon and its padding.
    static T Flat<T>(T button, string icon, string tip) where T : ContentControl
    {
        button.Content = new EditorIcon(icon);
        button.Padding = new Thickness(6);
        button.MinWidth = 0;
        button.MinHeight = 0;
        ToolTip.SetTip(button, tip);
        return button;
    }
}

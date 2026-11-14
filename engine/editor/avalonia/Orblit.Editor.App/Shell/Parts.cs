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
}

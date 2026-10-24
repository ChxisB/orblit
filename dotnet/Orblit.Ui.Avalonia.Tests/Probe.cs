using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Orblit.Ui.Avalonia.Tests;

/// <summary>Finds and presses what a surface built.</summary>
internal static class Probe
{
    internal static IEnumerable<T> All<T>(Visual root)
        where T : Visual => root.GetVisualDescendants().OfType<T>();

    internal static TextBlock Words(Visual root, string words) => All<TextBlock>(root).Single(block => block.Text == words);

    internal static Window Show(UiNode description, UiEvent? onEvent = null) =>
        Headless.Show(new UiSurface { Description = description, OnEvent = onEvent });

    /// <summary>Presses and lets go in the middle of <paramref name="target"/>, as a mouse would.</summary>
    internal static void Click(Window window, Control target)
    {
        var middle = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
        window.MouseDown(middle, MouseButton.Left);
        window.MouseUp(middle, MouseButton.Left);
    }
}

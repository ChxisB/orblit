using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>Quick Open Scene: the project's scene files by name, each with its path and when it was last opened.</summary>
internal static class QuickOpen
{
    internal static Picker Build(KeyModifiers command)
    {
        var search = new TextBox { PlaceholderText = "Search scenes", InnerLeftContent = Parts.FieldIcon("Lens", EditorBrushes.TextDim), Classes = { EditorTheme.Field } };
        var title = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = "Quick Open Scene", FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center },
                Parts.Kbd(Parts.KeyText(new KeyGesture(Key.O, command | KeyModifiers.Shift))),
            },
        };
        var top = new StackPanel { Children = { new Border { Padding = new Thickness(14, 12, 14, 0), Child = title }, new Border { Padding = new Thickness(12, 10, 12, 4), Child = search } } };
        return new Picker("Quick Open Scene", search, top);
    }

    /// <summary>A row for each scene in this order. <paramref name="whenOf"/> gives the words at its right.</summary>
    internal static IReadOnlyList<PickerRow> Rows(IEnumerable<string> scenes, Func<string, string> whenOf, Action<string> open) =>
        [.. scenes.Select(path => new PickerRow("", "SceneFile", Path.GetFileName(path), () => open(path)) { Tint = EditorBrushes.SceneIcon, Note = path, Key = whenOf(path) })];
}

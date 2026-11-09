using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Tests;

public sealed class EditorThemeTests
{
    // The values in REFERENCE.md, written out so a changed colour fails here.
    [Theory]
    [InlineData("Window", "#141414")]
    [InlineData("Panel", "#2a2a2a")]
    [InlineData("Field", "#1c1c1c")]
    [InlineData("InspectorField", "#212121")]
    [InlineData("ActiveTool", "#424242")]
    [InlineData("SelectedRow", "#494949")]
    [InlineData("Text", "#c5c5c5")]
    [InlineData("Accent", "#559eff")]
    [InlineData("TabInactive", "#959595")]
    public void Colours_match_the_reference(string name, string hex)
    {
        var field = typeof(EditorColors).GetField(name)!;

        Assert.Equal(Color.Parse(hex), (Color)field.GetValue(null)!);
    }

    static Color BrushOf(Window window, string key)
    {
        Assert.True(window.TryFindResource(key, ThemeVariant.Dark, out var value), key);
        return ((ISolidColorBrush)value!).Color;
    }

    [Fact]
    public void The_theme_beats_the_base_theme_for_every_control_the_shell_uses()
    {
        UiThread.Run(() =>
        {
            var window = new Window();

            Assert.Equal(Colors.Transparent, BrushOf(window, "ButtonBackground"));
            Assert.Equal(EditorColors.Hover, BrushOf(window, "ButtonBackgroundPointerOver"));
            Assert.Equal(EditorColors.ActiveTool, BrushOf(window, "ToggleButtonBackgroundChecked"));
            Assert.Equal(EditorColors.Accent, BrushOf(window, "ToggleButtonForegroundChecked"));
            Assert.Equal(EditorColors.Field, BrushOf(window, "TextControlBackground"));
            Assert.Equal(EditorColors.Accent, BrushOf(window, "TextControlBorderBrushFocused"));
            Assert.Equal(EditorColors.Field, BrushOf(window, "ComboBoxBackground"));
            // A row highlight draws the block, so the line itself stays clear.
            Assert.Equal(Colors.Transparent, BrushOf(window, "TreeViewItemBackgroundSelected"));
            Assert.Equal(EditorColors.Panel, BrushOf(window, "MenuFlyoutPresenterBackground"));
            Assert.True(window.TryFindResource("ContentControlThemeFontFamily", ThemeVariant.Dark, out var font));
            Assert.True(window.TryFindResource("ControlContentThemeFontSize", ThemeVariant.Dark, out var size));
            Assert.True(window.TryFindResource("ControlCornerRadius", ThemeVariant.Dark, out var corners));
            Assert.Equal(EditorTheme.Font, font);
            Assert.Equal(14.0, size);
            Assert.Equal(new CornerRadius(4), corners);
        });
    }

    [Fact]
    public void The_theme_is_dark_and_keeps_the_base_theme()
    {
        UiThread.Run(() =>
        {
            var window = new Window();

            Assert.Equal(ThemeVariant.Dark, window.ActualThemeVariant);
            Assert.Single(new EditorTheme().OfType<Avalonia.Themes.Fluent.FluentTheme>());
        });
    }

    [Fact]
    public void A_window_takes_the_editor_colours_font_and_size()
    {
        UiThread.Run(() =>
        {
            var window = new Window();
            window.Show();

            Assert.Equal(EditorColors.Window, ((ISolidColorBrush)window.Background!).Color);
            Assert.Equal(EditorColors.Text, ((ISolidColorBrush)window.Foreground!).Color);
            Assert.Equal(EditorTheme.Font, window.FontFamily);
            Assert.Equal(EditorTheme.FontSize, window.FontSize);
        });
    }

    [Fact]
    public void Tree_rows_are_the_reference_height_and_indent()
    {
        UiThread.Run(() =>
        {
            var window = new Window();

            Assert.True(window.TryFindResource("TreeViewItemMinHeight", ThemeVariant.Dark, out var height));
            Assert.True(window.TryFindResource("TreeViewItemIndent", ThemeVariant.Dark, out var indent));
            Assert.Equal(23.0, height);
            Assert.Equal(12.0, indent);
        });
    }
}

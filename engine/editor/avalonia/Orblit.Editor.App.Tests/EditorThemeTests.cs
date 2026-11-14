using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Tests;

public sealed class EditorThemeTests
{
    // The values on the design canvas, written out so a changed colour fails here.
    [Theory]
    [InlineData("Window", "#0A0C10")]
    [InlineData("Panel", "#12151B")]
    [InlineData("Field", "#0D1015")]
    [InlineData("Popover", "#161A21")]
    [InlineData("Raised", "#1A1E26")]
    [InlineData("Lifted", "#242A35")]
    [InlineData("Line", "#1E232C")]
    [InlineData("LineStrong", "#262C37")]
    [InlineData("Text", "#E8EBF0")]
    [InlineData("TextSoft", "#AAB2BF")]
    [InlineData("TextDim", "#858F9D")]
    [InlineData("Accent", "#E5893F")]
    [InlineData("AccentSoft", "#29E5893F")]
    [InlineData("AxisX", "#FF6B78")]
    [InlineData("AxisY", "#8BD450")]
    [InlineData("AxisZ", "#5AA2FF")]
    public void Colours_match_the_design(string name, string hex)
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
            Assert.Equal(EditorColors.AccentSoft, BrushOf(window, "ToggleButtonBackgroundChecked"));
            Assert.Equal(EditorColors.Accent, BrushOf(window, "ToggleButtonForegroundChecked"));
            Assert.Equal(EditorColors.Field, BrushOf(window, "TextControlBackground"));
            Assert.Equal(EditorColors.Accent, BrushOf(window, "TextControlBorderBrushFocused"));
            Assert.Equal(EditorColors.Field, BrushOf(window, "ComboBoxBackground"));
            // A row highlight draws the block, so the line itself stays clear.
            Assert.Equal(Colors.Transparent, BrushOf(window, "TreeViewItemBackgroundSelected"));
            Assert.Equal(EditorColors.Popover, BrushOf(window, "MenuFlyoutPresenterBackground"));
            Assert.Equal(EditorColors.LineStrong, BrushOf(window, "MenuFlyoutPresenterBorderBrush"));
            Assert.True(window.TryFindResource("ContentControlThemeFontFamily", ThemeVariant.Dark, out var font));
            Assert.True(window.TryFindResource("ControlContentThemeFontSize", ThemeVariant.Dark, out var size));
            Assert.True(window.TryFindResource("ControlCornerRadius", ThemeVariant.Dark, out var corners));
            Assert.Equal(EditorTheme.Font, font);
            Assert.Equal(13.0, size);
            Assert.Equal(new CornerRadius(6), corners);
        });
    }

    [Fact]
    public void The_base_theme_takes_the_editor_accent()
    {
        UiThread.Run(() =>
        {
            var window = new Window();
            window.Show();

            Assert.True(window.TryFindResource("SystemAccentColor", ThemeVariant.Dark, out var accent));
            Assert.Equal(EditorColors.Accent, accent);
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

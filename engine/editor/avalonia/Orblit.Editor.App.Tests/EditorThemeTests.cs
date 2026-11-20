using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.VisualTree;
using Avalonia.Styling;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using static Orblit.Editor.App.Tests.ReferenceWindow;

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

    [Theory]
    [InlineData("Geist-Regular.ttf")]
    [InlineData("Geist-Medium.ttf")]
    [InlineData("Geist-SemiBold.ttf")]
    [InlineData("Geist-Bold.ttf")]
    [InlineData("GeistMono-Regular.ttf")]
    public void The_editor_ships_the_canvas_fonts(string file)
    {
        UiThread.Run(() => Assert.True(AssetLoader.Exists(new Uri(EditorTheme.FontFolder + file))));
    }

    [Fact]
    public void Words_are_set_in_Geist_and_values_in_Geist_Mono()
    {
        Assert.Equal("Geist", EditorTheme.Font.Name);
        Assert.Equal("Geist Mono", EditorTheme.Mono.Name);
        Assert.Equal(new Uri(EditorTheme.FontFolder), EditorTheme.Font.Key!.Source);
        Assert.Equal(new Uri(EditorTheme.FontFolder), EditorTheme.Mono.Key!.Source);
    }

    [Fact]
    public void Number_fields_script_text_and_output_lines_are_set_in_Geist_Mono()
    {
        UiThread.Run(() =>
        {
            var script = new ScriptView();

            Assert.All(new VectorRow("Position").Axes, box => Assert.Equal(EditorTheme.Mono, box.FontFamily));
            Assert.Equal(EditorTheme.Mono, script.Editor.FontFamily);
            Assert.Equal(EditorTheme.Mono, script.Gutter.FontFamily);
            Assert.Equal(EditorTheme.Mono, new OutputPanel().List.FontFamily);
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
            Assert.Equal(26.0, height);
            Assert.Equal(12.0, indent);
        });
    }

    [Fact]
    public void A_menu_is_a_popover_with_a_5_pixel_inset_round_28_pixel_rows()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var menu = window.ScenePane.Outline.ContextMenu!;
            menu.Open(window.ScenePane.Outline);
            var item = menu.Items.OfType<MenuItem>().First(item => item.IsVisible);

            Assert.Equal((EditorTheme.PopoverCorners, new Thickness(5)), (menu.CornerRadius, menu.Padding));
            Assert.Equal((28.0, EditorTheme.Corners), (item.Bounds.Height, item.CornerRadius));
            Assert.Equal(new Point(6, 6), item.TranslatePoint(new Point(0, 0), menu));
            Assert.Equal(EditorColors.Popover, ((ISolidColorBrush)menu.Background!).Color);
        });
    }

    [Fact]
    public void A_flyout_takes_the_popover_fill_edge_and_corners()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            ClickOn(window.ScenePane.AddButton, window);
            var presenter = ((Control)window.ScenePane.Flyout.Content!).FindAncestorOfType<FlyoutPresenter>()!;

            Assert.Equal(EditorColors.Popover, ((ISolidColorBrush)presenter.Background!).Color);
            Assert.Equal(EditorColors.LineStrong, ((ISolidColorBrush)presenter.BorderBrush!).Color);
            Assert.Equal(EditorTheme.PopoverCorners, presenter.CornerRadius);
        });
    }
}

using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class BottomBarTests
{
    static BottomBar BarOf(EditorWindow window) => Assert.IsType<BottomBar>(window.Center.BottomBar.Child);

    static string[] Labels(Button tab) => tab.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text!).ToArray();

    [Fact]
    public void The_bar_holds_the_five_panel_buttons_in_order()
    {
        UiThread.Run(() =>
        {
            var tabs = BarOf(OpenAtReferenceSize()).Tabs;

            Assert.Equal(["Output", "Debugger", "Audio", "Animation", "Shader Editor"], tabs.Select(tab => Labels(tab).Single()));
        });
    }

    [Fact]
    public void The_first_tab_starts_six_pixels_in_with_ten_before_its_icon_and_six_after_it()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var bar = BarOf(window);
            var icon = bar.Tabs[0].GetVisualDescendants().OfType<EditorIcon>().Single();
            var text = bar.Tabs[0].GetVisualDescendants().OfType<TextBlock>().Single();

            AssertBox(293 + 6, 1241, BoxOf(bar.Tabs[0], window).Width, 26, BoxOf(bar.Tabs[0], window));
            // A 13 pixel icon in a 26 pixel tab is half a pixel off centre. Geist's line box rounds it up.
            AssertBox(309, 1247, 13, 13, BoxOf(icon, window));
            Assert.Equal(309 + 13 + 6, BoxOf(text, window).X);
        });
    }

    [Fact]
    public void Each_tab_shows_its_panel_icon_13_square_and_no_dot()
    {
        UiThread.Run(() =>
        {
            var bar = BarOf(OpenAtReferenceSize());
            var icons = bar.Tabs.Select(tab => tab.GetVisualDescendants().OfType<EditorIcon>().Single()).ToArray();

            Assert.Equal(["Output", "Debug", "AudioStreamPlayer", "Animation", "ShaderDock"], icons.Select(icon => icon.IconName));
            Assert.All(icons, icon => Assert.Equal((13d, 13d), (icon.Bounds.Width, icon.Bounds.Height)));
            Assert.Empty(bar.Tabs.SelectMany(tab => tab.GetVisualDescendants().OfType<Ellipse>()));
        });
    }

    [Fact]
    public void Every_label_sits_on_the_centre_line_of_the_bar()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var texts = BarOf(window).Tabs.SelectMany(tab => tab.GetVisualDescendants().OfType<TextBlock>()).ToArray();

            Assert.Equal(5, texts.Length);
            Assert.All(texts, text => Assert.InRange(BoxOf(text, window).Y + text.Bounds.Height / 2, 1253.5, 1254.5));
        });
    }

    [Fact]
    public void The_buttons_follow_each_other_two_pixels_apart()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var tabs = BarOf(window).Tabs;

            for (var i = 1; i < tabs.Count; i++)
            {
                Assert.Equal(2, BoxOf(tabs[i], window).X - BoxOf(tabs[i - 1], window).Right);
            }
        });
    }

    [Fact]
    public void The_output_tab_is_dim_and_the_unbuilt_tabs_dimmer_all_rounded_with_no_border()
    {
        UiThread.Run(() =>
        {
            var tabs = BarOf(OpenAtReferenceSize()).Tabs;

            Assert.Equal(EditorColors.TextDim, ((ISolidColorBrush)tabs[0].Foreground!).Color);
            Assert.All(tabs.Skip(1), tab => Assert.Equal(Color.Parse("#5C6573"), ((ISolidColorBrush)tab.Foreground!).Color));
            Assert.All(tabs, tab => Assert.Equal((0d, EditorTheme.Corners), (tab.BorderThickness.Left, tab.CornerRadius)));
        });
    }

    [Fact]
    public void The_rule_is_one_by_eighteen_and_eight_pixels_clear_of_the_status_and_the_version()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.Fail("Saved.");
            window.UpdateLayout();
            var bar = BarOf(window);
            var status = bar.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == "Saved.");
            var separator = BoxOf(bar.Separator, window);

            AssertBox(BoxOf(status, window).Right + 8, 1245, 1, 18, separator);
            Assert.Equal(separator.Right + 8, BoxOf(bar.VersionButton, window).X);
            Assert.Equal(Color.Parse("#262C37"), ((ISolidColorBrush)bar.Separator.Background!).Color);
        });
    }

    [Fact]
    public void The_version_is_small_dim_text_with_no_logo()
    {
        UiThread.Run(() =>
        {
            var version = BarOf(OpenAtReferenceSize()).VersionButton;
            var text = version.GetVisualDescendants().OfType<TextBlock>().Single();

            Assert.Equal("1.0.stable", text.Text);
            Assert.Equal((11d, EditorColors.TextDim, 1d), (text.FontSize, ((ISolidColorBrush)version.Foreground!).Color, version.Opacity));
            Assert.Empty(version.GetVisualDescendants().OfType<EditorIcon>());
        });
    }

    [Fact]
    public void A_click_on_the_version_says_copying_is_not_built()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            ClickOn(BarOf(window).VersionButton, window);

            Assert.Equal("Copying the version is not built yet.", window.Status);
        });
    }

    [Fact]
    public void The_pin_icon_ends_eleven_pixels_from_the_bar_edge_and_follows_the_version_by_eleven()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var bar = BarOf(window);
            var icon = BoxOf(bar.PinButton.GetVisualDescendants().OfType<EditorIcon>().Single(), window);
            var text = BoxOf(bar.VersionButton.GetVisualDescendants().OfType<TextBlock>().Single(), window);

            Assert.Equal(293 + 2818 - 11, icon.Right);
            Assert.Equal("Pin", bar.PinButton.GetVisualDescendants().OfType<EditorIcon>().Single().IconName);
            Assert.Equal(16, icon.Width);
            Assert.InRange(icon.X - text.Right, 10.5, 11.5);
            AssertBox(3079, 1241, 26, 26, BoxOf(bar.PinButton, window));
        });
    }

    [Fact]
    public void The_status_follows_a_dim_info_icon_in_soft_12_pixel_text()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var bar = BarOf(window);
            window.Fail("Saved.");
            window.UpdateLayout();
            var status = bar.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == "Saved.");
            var icon = bar.GetVisualDescendants().OfType<EditorIcon>().Single(icon => icon.IconName == "Info");

            Assert.Equal(BoxOf(icon, window).Right + 6, BoxOf(status, window).X);
            Assert.Equal((13d, EditorColors.TextDim), (icon.Bounds.Width, ((ISolidColorBrush)TextElement.GetForeground(icon)!).Color));
            Assert.Equal((12d, EditorColors.TextSoft), (status.FontSize, ((ISolidColorBrush)status.Foreground!).Color));
            Assert.InRange(BoxOf(status, window).Y + status.Bounds.Height / 2, 1253.5, 1254.5);
            Assert.Equal("Saved.", window.Status);
        });
    }

    [Fact]
    public void A_long_status_grows_leftward_and_leaves_the_pin_where_it_is()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var bar = BarOf(window);
            var before = BoxOf(bar.PinButton, window).X;

            window.Fail("A long answer that takes much more room than the short one before it.");
            window.UpdateLayout();

            Assert.Equal(before, BoxOf(bar.PinButton, window).X);
        });
    }

    [Fact]
    public void The_bar_is_the_child_of_the_bottom_strip()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.IsType<BottomBar>(window.Center.BottomBar.Child);
            AssertBox(293, 1236, 2818, 36, BoxOf(BarOf(window), window));
        });
    }
}

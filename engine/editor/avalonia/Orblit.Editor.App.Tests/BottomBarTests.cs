using Avalonia.Controls;
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

    static EditorWindow OpenQuiet()
    {
        var window = OpenAtReferenceSize();
        window.Fail("");
        window.UpdateLayout();
        return window;
    }

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
    public void The_first_button_starts_six_pixels_in_with_ten_before_its_content()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var bar = BarOf(window);
            var dot = bar.Tabs[0].GetVisualDescendants().OfType<Ellipse>().Single();
            var text = bar.Tabs[0].GetVisualDescendants().OfType<TextBlock>().Single();

            Assert.Equal(287 + 6, BoxOf(bar.Tabs[0], window).X);
            AssertBox(303, 1250, 8, 8, BoxOf(dot, window));
            Assert.Equal(317, BoxOf(text, window).X);
        });
    }

    [Fact]
    public void Only_the_output_button_has_a_dot_and_it_is_the_measured_colour()
    {
        UiThread.Run(() =>
        {
            var bar = BarOf(OpenAtReferenceSize());
            var dots = bar.Tabs.SelectMany(tab => tab.GetVisualDescendants().OfType<Ellipse>()).ToArray();

            var dot = Assert.Single(dots);
            Assert.Equal(Color.Parse("#9d8d5c"), ((ISolidColorBrush)dot.Fill!).Color);
            Assert.Contains(dot, bar.Tabs[0].GetVisualDescendants());
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
            Assert.All(texts, text => Assert.InRange(BoxOf(text, window).Y + text.Bounds.Height / 2, 1254, 1255));
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
    public void The_buttons_are_dim_and_have_no_border()
    {
        UiThread.Run(() =>
        {
            var tabs = BarOf(OpenAtReferenceSize()).Tabs;

            Assert.All(tabs, tab => Assert.Equal(EditorColors.TabInactive, ((ISolidColorBrush)tab.Foreground!).Color));
            Assert.All(tabs, tab => Assert.Equal(0, tab.BorderThickness.Left));
        });
    }

    [Fact]
    public void The_separator_is_two_by_twenty_five_and_centred_in_the_bar()
    {
        UiThread.Run(() =>
        {
            var window = OpenQuiet();
            var separator = BoxOf(BarOf(window).Separator, window);

            Assert.Equal(2, separator.Width);
            Assert.Equal(25, separator.Height);
            Assert.Equal(1234 + (41 - 25) / 2, separator.Y);
            Assert.Equal(Color.Parse("#191919"), ((ISolidColorBrush)BarOf(window).Separator.Background!).Color);
        });
    }

    [Fact]
    public void With_no_status_the_logo_starts_thirteen_pixels_after_the_separator()
    {
        UiThread.Run(() =>
        {
            var window = OpenQuiet();
            var bar = BarOf(window);
            var logo = bar.VersionButton.GetVisualDescendants().OfType<EditorIcon>().Single();

            AssertBox(BoxOf(bar.Separator, window).Right + 13, 1246, 16, 16, BoxOf(logo, window));
        });
    }

    [Fact]
    public void The_version_text_follows_the_logo_after_eleven_pixels()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var bar = BarOf(window);
            var logo = bar.VersionButton.GetVisualDescendants().OfType<EditorIcon>().Single();
            var text = bar.VersionButton.GetVisualDescendants().OfType<TextBlock>().Single();

            Assert.Equal("1.0.stable", text.Text);
            Assert.Equal("OrblitMonochrome", logo.IconName);
            Assert.Equal(11, BoxOf(text, window).X - BoxOf(logo, window).Right);
        });
    }

    [Fact]
    public void The_version_is_drawn_dim()
    {
        UiThread.Run(() =>
        {
            Assert.Equal(0.65, BarOf(OpenAtReferenceSize()).VersionButton.Opacity);
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
    public void The_pin_icon_ends_twelve_pixels_from_the_bar_edge_and_follows_the_version_by_eighteen()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var bar = BarOf(window);
            var icon = BoxOf(bar.PinButton.GetVisualDescendants().OfType<EditorIcon>().Single(), window);
            var text = BoxOf(bar.VersionButton.GetVisualDescendants().OfType<TextBlock>().Single(), window);

            Assert.Equal(287 + 2866 - 12, icon.Right);
            Assert.Equal("Pin", bar.PinButton.GetVisualDescendants().OfType<EditorIcon>().Single().IconName);
            Assert.Equal(16, icon.Width);
            Assert.InRange(icon.X - text.Right, 17.5, 18.5);
            AssertBox(3119, 1242, 28, 24, BoxOf(bar.PinButton, window));
        });
    }

    [Fact]
    public void The_status_line_sits_between_the_separator_and_the_version_and_follows_the_text()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var bar = BarOf(window);
            window.Fail("Saved.");
            window.UpdateLayout();
            var status = bar.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == "Saved.");

            Assert.Equal(BoxOf(bar.Separator, window).Right + 8, BoxOf(status, window).X);
            Assert.Equal(BoxOf(bar.VersionButton, window).X, BoxOf(status, window).Right + 5);
            Assert.InRange(BoxOf(status, window).Y + status.Bounds.Height / 2, 1254, 1255);
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
            AssertBox(287, 1234, 2866, 41, BoxOf(BarOf(window), window));
        });
    }
}

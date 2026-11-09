using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class InspectorStructureTests
{
    // The bars and rows sit 6 pixels inside the dock, which starts at 3156.
    const double BarX = 3162;
    const double BarWidth = 268;

    static readonly string[] s_entityGroups = ["Process", "Physics Interpolation", "Auto Translate", "Editor Description"];

    static EditorWindow Selected(OutlineNode node)
    {
        var window = OpenAtReferenceSize();
        window.Outline.SelectedItem = node;
        window.UpdateLayout();
        return window;
    }

    static ClassBar[] Bars(EditorWindow window) =>
        [.. window.Inspector.GetVisualDescendants().OfType<ClassBar>().Where(bar => bar.IsEffectivelyVisible)];

    static PropertyGroup[] Groups(EditorWindow window) =>
        [.. window.Inspector.GetVisualDescendants().OfType<PropertyGroup>().Where(group => group.IsEffectivelyVisible)];

    static PropertyGroup Group(EditorWindow window, string title) =>
        window.Inspector.GetVisualDescendants().OfType<PropertyGroup>().Single(group => group.Title == title);

    static string ArrowOf(PropertyGroup group) =>
        group.Header.GetVisualDescendants().OfType<EditorIcon>().Single(icon => icon.IsEffectivelyVisible).IconName;

    [Fact]
    public void An_Entity3D_shows_its_class_bar_then_the_Entity_bar_with_every_group()
    {
        UiThread.Run(() =>
        {
            var window = Selected(FakeScene.Lamp);

            Assert.Equal(["Entity3D", "Entity"], Bars(window).Select(bar => bar.Title));
            Assert.Equal(["Transform", "Visibility", .. s_entityGroups], Groups(window).Select(group => group.Title));
        });
    }

    [Fact]
    public void A_node_with_no_transform_has_its_own_bar_then_Entity_and_no_Transform()
    {
        UiThread.Run(() =>
        {
            var window = Selected(FakeScene.Sound);

            Assert.Equal(["Audio", "Entity"], Bars(window).Select(bar => bar.Title));
            Assert.Equal(s_entityGroups, Groups(window).Select(group => group.Title));
        });
    }

    [Fact]
    public void A_bar_is_a_30_high_rounded_block_inside_a_6_pixel_gutter()
    {
        UiThread.Run(() =>
        {
            var window = Selected(FakeScene.Lamp);
            var bars = Bars(window);

            AssertBox(BarX, 149, BarWidth, 30, BoxOf(bars[0], window));
            Assert.Equal(Color.Parse("#393939"), ((ISolidColorBrush)bars[0].Background!).Color);
            Assert.Equal(EditorTheme.Corners, bars[0].CornerRadius);
            Assert.All(bars, bar => Assert.Equal((BarX, BarWidth, 30d), (BoxOf(bar, window).X, BoxOf(bar, window).Width, BoxOf(bar, window).Height)));
        });
    }

    [Fact]
    public void A_bar_draws_the_class_icon_and_falls_back_to_the_Entity_icon()
    {
        UiThread.Run(() =>
        {
            Assert.Equal(["Entity3D", "Entity"], Bars(Selected(FakeScene.Lamp)).Select(bar => bar.IconName));
            Assert.Equal(["Entity", "Entity"], Bars(Selected(FakeScene.Sound)).Select(bar => bar.IconName));
        });
    }

    [Fact]
    public void A_group_header_sits_under_its_bar_with_the_arrow_at_the_edge_and_the_title_16_further()
    {
        UiThread.Run(() =>
        {
            var window = Selected(FakeScene.Lamp);
            var bar = BoxOf(Bars(window)[0], window);
            var header = BoxOf(Group(window, "Transform").Header, window);
            var arrow = Group(window, "Transform").Header.GetVisualDescendants().OfType<EditorIcon>().Single(icon => icon.IsEffectivelyVisible);
            var title = Group(window, "Transform").Header.GetVisualDescendants().OfType<TextBlock>().Single();

            Assert.Equal((BarX, BarWidth), (header.X, header.Width));
            Near(bar.Bottom + 2, header.Y);
            Near(24.5, header.Height);
            Assert.Equal(BarX, BoxOf(arrow, window).X);
            Near(BarX + 18, BoxOf(title, window).X);
            Assert.Equal(FontWeight.SemiBold, title.FontWeight);
        });
    }

    [Fact]
    public void The_Transform_rows_sit_12_pixels_in_and_the_next_group_follows_them()
    {
        UiThread.Run(() =>
        {
            var window = Selected(FakeScene.Lamp);
            var header = BoxOf(Group(window, "Transform").Header, window);
            var position = BoxOf(window.Inspector.Position, window);
            var scale = BoxOf(window.Inspector.Scale, window);

            Assert.Equal((BarX + 12, BarWidth - 12), (position.X, position.Width));
            Near(header.Bottom - 2, position.Y);
            Near(position.Y + 118, scale.Y);
            Near(scale.Bottom, BoxOf(Group(window, "Visibility").Header, window).Y);
        });
    }

    [Fact]
    public void The_Entity_bar_follows_Visibility_and_its_groups_follow_the_bar()
    {
        UiThread.Run(() =>
        {
            var window = Selected(FakeScene.Lamp);
            var visibility = BoxOf(Group(window, "Visibility").Header, window);
            var bar = BoxOf(Bars(window)[1], window);
            var process = BoxOf(Group(window, "Process").Header, window);
            var last = BoxOf(Group(window, "Editor Description").Header, window);

            Near(visibility.Bottom + 4, bar.Y);
            Near(bar.Bottom + 2, process.Y);
            // A whole-pixel layout rounds 24.5 up, so the pitch is the height it drew.
            Near(process.Y + 3 * process.Height, last.Y);
        });
    }

    [Fact]
    public void The_Transform_group_starts_open_and_a_click_folds_and_unfolds_it()
    {
        UiThread.Run(() =>
        {
            var window = Selected(FakeScene.Lamp);
            var transform = Group(window, "Transform");

            Assert.True(transform.IsOpen);
            Assert.Equal("GuiTreeArrowDown", ArrowOf(transform));
            Assert.True(window.Inspector.Position.IsEffectivelyVisible);

            ClickOn(transform.Header, window);
            Assert.False(transform.IsOpen);
            Assert.Equal("GuiTreeArrowRight", ArrowOf(transform));
            Assert.False(window.Inspector.Position.IsEffectivelyVisible);

            window.UpdateLayout();
            Near(BoxOf(transform.Header, window).Bottom, BoxOf(Group(window, "Visibility").Header, window).Y);

            ClickOn(transform.Header, window);
            Assert.True(transform.IsOpen);
            Assert.True(window.Inspector.Position.IsEffectivelyVisible);
        });
    }

    [Theory]
    [InlineData("Visibility")]
    [InlineData("Process")]
    [InlineData("Physics Interpolation")]
    [InlineData("Auto Translate")]
    [InlineData("Editor Description")]
    public void A_group_with_no_rows_stays_closed_and_says_it_is_not_built(string title)
    {
        UiThread.Run(() =>
        {
            var window = Selected(FakeScene.Lamp);
            var group = Group(window, title);

            ClickOn(group.Header, window);

            Assert.False(group.IsOpen);
            Assert.Equal("GuiTreeArrowRight", ArrowOf(group));
            Assert.Equal($"{title} is not built yet.", window.Status);
        });
    }

    [Fact]
    public void The_script_row_is_a_label_and_a_field_half_the_bar_wide_under_the_last_group()
    {
        UiThread.Run(() =>
        {
            var window = Selected(FakeScene.Lamp);
            var last = BoxOf(Group(window, "Editor Description").Header, window);
            var field = BoxOf(window.Inspector.ScriptField, window);
            var label = window.Inspector.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == "Script");

            Assert.Equal((3296d, 134d, 28d), (field.X, field.Width, field.Height));
            Near(last.Bottom + 4, field.Y);
            Near(BarX + 10, BoxOf(label, window).X);
            Assert.Contains(window.Inspector.ScriptField.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "<empty>");
            Assert.Equal(["Load", "GuiDropdown"], window.Inspector.ScriptField.GetVisualDescendants().OfType<EditorIcon>().Select(icon => icon.IconName));
        });
    }

    [Fact]
    public void Add_Metadata_is_a_centred_130_by_30_button_under_the_script_row()
    {
        UiThread.Run(() =>
        {
            var window = Selected(FakeScene.Lamp);
            var field = BoxOf(window.Inspector.ScriptField, window);
            var button = BoxOf(window.Inspector.MetadataButton, window);

            Assert.Equal((3231d, 130d, 30d), (button.X, button.Width, button.Height));
            Near(field.Bottom + 13, button.Y);
            Assert.Equal(Color.Parse("#424242"), ((ISolidColorBrush)window.Inspector.MetadataButton.Background!).Color);
            Assert.Equal("Add", window.Inspector.MetadataButton.GetVisualDescendants().OfType<EditorIcon>().Single().IconName);
            Assert.Contains(window.Inspector.MetadataButton.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "Add Metadata");
        });
    }

    [Fact]
    public void The_script_field_and_Add_Metadata_open_their_flyouts()
    {
        UiThread.Run(() =>
        {
            var window = Selected(FakeScene.Lamp);

            ClickOn(window.Inspector.ScriptField, window);
            Assert.True(window.Inspector.ScriptMenu.IsOpen);
            window.Inspector.ScriptMenu.Hide();

            ClickOn(window.Inspector.MetadataButton, window);
            Assert.True(window.Inspector.MetadataFlyout.IsOpen);
        });
    }

    [Fact]
    public void A_filter_keeps_only_the_bars_and_groups_that_hold_a_matching_row()
    {
        UiThread.Run(() =>
        {
            var window = Selected(FakeScene.Lamp);

            window.InspectorPane.FilterBox.Text = "pos";

            Assert.Equal(["Entity3D"], Bars(window).Select(bar => bar.Title));
            Assert.Equal(["Transform"], Groups(window).Select(group => group.Title));
            Assert.False(window.Inspector.ScriptField.IsEffectivelyVisible);
            Assert.False(window.Inspector.MetadataButton.IsEffectivelyVisible);

            window.InspectorPane.FilterBox.Text = "zzz";
            Assert.Empty(Bars(window));
            Assert.Empty(Groups(window));

            window.InspectorPane.FilterBox.Text = "";
            Assert.Equal(["Entity3D", "Entity"], Bars(window).Select(bar => bar.Title));
            Assert.True(window.Inspector.MetadataButton.IsEffectivelyVisible);
        });
    }

    [Fact]
    public void With_nothing_selected_no_bar_group_or_script_row_shows()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.Empty(Bars(window));
            Assert.Empty(Groups(window));
            Assert.False(window.Inspector.ScriptField.IsEffectivelyVisible);
            Assert.False(window.Inspector.MetadataButton.IsEffectivelyVisible);
        });
    }

    [Fact]
    public void The_history_row_names_the_inspected_node_with_its_class_icon()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            Assert.False(window.InspectorPane.InspectedName.IsEffectivelyVisible);

            window.Outline.SelectedItem = FakeScene.Lamp;
            window.UpdateLayout();
            var name = window.InspectorPane.InspectedName;
            var icon = window.InspectorPane.InspectedIcon;

            Assert.Equal("Lamp", name.Text);
            Assert.Equal("Entity3D", icon.IconName);
            Assert.True(name.IsEffectivelyVisible);
            Assert.True(BoxOf(icon, window).Right <= BoxOf(name, window).X);
            Assert.True(BoxOf(icon, window).X > BoxOf(window.InspectorPane.ForwardButton, window).Right);
            Assert.True(BoxOf(name, window).Bottom <= BoxOf(window.InspectorPane.FilterBox, window).Y);

            window.Outline.SelectedItem = FakeScene.Sound;
            Assert.Equal("Sound", window.InspectorPane.InspectedName.Text);
            Assert.Equal("Entity", window.InspectorPane.InspectedIcon.IconName);
        });
    }

    [Fact]
    public void The_history_row_has_a_dropdown_button_that_is_not_built()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var pane = window.InspectorPane;

            Assert.Equal("GuiDropdown", pane.DropdownButton.GetVisualDescendants().OfType<EditorIcon>().Single().IconName);
            Assert.True(BoxOf(pane.DropdownButton, window).Right < BoxOf(pane.HistoryButton, window).X);

            ClickOn(pane.DropdownButton, window);
            Assert.Equal("The list of inspected objects is not built yet.", window.Status);
        });
    }
}

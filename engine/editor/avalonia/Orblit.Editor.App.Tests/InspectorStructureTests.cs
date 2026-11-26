using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
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
    const double BarX = 3125;
    const double BarWidth = 298;

    static readonly string[] s_entityGroups = ["Run", "Physics Interpolation", "Auto Translate", "Editor Description"];

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

            Assert.Equal(["Empty 3D point", "From any thing"], Bars(window).Select(bar => bar.Title));
            Assert.Equal(["Where it is", "Visibility", .. s_entityGroups], Groups(window).Select(group => group.Title));
        });
    }

    [Fact]
    public void A_node_with_no_transform_has_its_own_bar_then_Entity_and_no_Transform()
    {
        UiThread.Run(() =>
        {
            var window = Selected(FakeScene.Sound);

            Assert.Equal(["Audio", "From any thing"], Bars(window).Select(bar => bar.Title));
            Assert.Equal(s_entityGroups, Groups(window).Select(group => group.Title));
        });
    }

    [Fact]
    public void A_bar_is_a_34_high_strip_across_the_gutter_with_a_line_over_it()
    {
        UiThread.Run(() =>
        {
            var window = Selected(FakeScene.Lamp);
            var bars = Bars(window);

            AssertBox(BarX - 8, 163, BarWidth + 16, 34, BoxOf(bars[0], window));
            Assert.Equal(
                (EditorBrushes.ClassBar, EditorBrushes.Line, new Thickness(0, 1, 0, 0), default(CornerRadius)),
                (bars[0].Background, bars[0].BorderBrush, bars[0].BorderThickness, bars[0].CornerRadius));
            Assert.All(bars, bar => Assert.Equal((BarX - 8, BarWidth + 16, 34d), (BoxOf(bar, window).X, BoxOf(bar, window).Width, BoxOf(bar, window).Height)));
        });
    }

    [Fact]
    public void A_bar_starts_its_icon_12_pixels_in_and_titles_it_in_semibold_12_and_a_half()
    {
        UiThread.Run(() =>
        {
            var window = Selected(FakeScene.Lamp);
            var bar = Bars(window)[0];
            // The title comes before the engine's name in dim mono.
            var title = bar.GetVisualDescendants().OfType<TextBlock>().First();

            Assert.Equal(BarX - 8 + 12, BoxOf(bar.GetVisualDescendants().OfType<EditorIcon>().Single(), window).X);
            Assert.Equal((FontWeight.SemiBold, 12.5), (title.FontWeight, title.FontSize));
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
            var header = BoxOf(Group(window, "Where it is").Header, window);
            var arrow = Group(window, "Where it is").Header.GetVisualDescendants().OfType<EditorIcon>().Single(icon => icon.IsEffectivelyVisible);
            var title = Group(window, "Where it is").Header.GetVisualDescendants().OfType<TextBlock>().Single();

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
            var header = BoxOf(Group(window, "Where it is").Header, window);
            var position = BoxOf(window.Inspector.Position, window);
            var scale = BoxOf(window.Inspector.Scale, window);

            Assert.Equal((BarX + 12, BarWidth - 12), (position.X, position.Width));
            Near(header.Bottom, position.Y);
            Near(position.Y + 60, scale.Y);
            Near(scale.Bottom, BoxOf(window.Inspector.Rows.Single(row => row.Label == "Top Level"), window).Y);
            Near(BoxOf(Group(window, "Where it is").Body, window).Bottom, BoxOf(Group(window, "Visibility").Header, window).Y);
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
            var process = BoxOf(Group(window, "Run").Header, window);
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
            var transform = Group(window, "Where it is");

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
    [InlineData("Visibility", "Visible")]
    [InlineData("Run", "Mode")]
    [InlineData("Physics Interpolation", "Mode")]
    [InlineData("Auto Translate", "Mode")]
    [InlineData("Editor Description", "Editor Description")]
    public void A_group_of_the_engine_rows_starts_closed_and_a_click_opens_it(string title, string first)
    {
        UiThread.Run(() =>
        {
            var window = Selected(FakeScene.Lamp);
            var group = Group(window, title);
            Assert.False(group.IsOpen);
            Assert.Equal("GuiTreeArrowRight", ArrowOf(group));

            ClickOn(group.Header, window);

            Assert.True(group.IsOpen);
            Assert.Equal("GuiTreeArrowDown", ArrowOf(group));
            Assert.Equal(first, ((PropertyRow)((Panel)group.Body).Children[0]).Label);
            Assert.True(group.Body.IsEffectivelyVisible);
        });
    }

    [Fact]
    public void A_group_keeps_its_fold_from_one_entity_to_the_next()
    {
        UiThread.Run(() =>
        {
            var window = Selected(FakeScene.Lamp);
            ClickOn(Group(window, "Run").Header, window);

            window.Outline.SelectedItem = FakeScene.Sound;
            window.UpdateLayout();

            Assert.True(Group(window, "Run").IsOpen);
            Assert.False(Group(window, "Auto Translate").IsOpen);
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

            Assert.Equal((BarX + BarWidth / 2, BarWidth / 2, 28d), (field.X, field.Width, field.Height));
            Near(last.Bottom + 4, field.Y);
            Near(BarX + 8, BoxOf(label, window).X);
            Assert.Contains(window.Inspector.ScriptField.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "None");
            Assert.Equal(["Load", "GuiDropdown"], window.Inspector.ScriptField.GetVisualDescendants().OfType<EditorIcon>().Select(icon => icon.IconName));
        });
    }

    [Fact]
    public void Add_Metadata_is_a_ghost_button_on_a_dashed_edge_across_the_gutter_under_the_script_row()
    {
        UiThread.Run(() =>
        {
            var window = Selected(FakeScene.Lamp);
            var attach = BoxOf(window.Inspector.AttachButton, window);
            var button = BoxOf(window.Inspector.MetadataButton, window);
            var edge = window.Inspector.MetadataButton.GetVisualDescendants().OfType<Rectangle>().Single();

            Assert.Equal((BarX, BarWidth, 30d), (button.X, button.Width, button.Height));
            Near(attach.Bottom + 13, button.Y);
            Assert.Equal(Colors.Transparent, ((ISolidColorBrush)window.Inspector.MetadataButton.Background!).Color);
            Assert.Equal((EditorBrushes.LineStrong, button.Size), (edge.Stroke, BoxOf(edge, window).Size));
            Assert.NotEmpty(edge.StrokeDashArray!);
            Assert.Equal("Add", window.Inspector.MetadataButton.GetVisualDescendants().OfType<EditorIcon>().Single().IconName);
            Assert.Contains(window.Inspector.MetadataButton.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "Add a custom value");
        });
    }

    [Fact]
    public void Attach_a_script_is_a_quieter_dashed_button_12_under_the_script_row()
    {
        UiThread.Run(() =>
        {
            var window = Selected(FakeScene.Lamp);
            var field = BoxOf(window.Inspector.ScriptField, window);
            var button = BoxOf(window.Inspector.AttachButton, window);

            Assert.Equal((BarX, BarWidth, 32d), (button.X, button.Width, button.Height));
            Near(field.Bottom + 12, button.Y);
            Assert.Equal(EditorBrushes.TextDim, window.Inspector.AttachButton.Foreground);
            Assert.NotEmpty(window.Inspector.AttachButton.GetVisualDescendants().OfType<Rectangle>().Single().StrokeDashArray!);
            Assert.Equal("ScriptCreate", window.Inspector.AttachButton.GetVisualDescendants().OfType<EditorIcon>().Single().IconName);
            Assert.Contains(window.Inspector.AttachButton.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "Add a script");
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

            Assert.Equal(["Empty 3D point"], Bars(window).Select(bar => bar.Title));
            Assert.Equal(["Where it is"], Groups(window).Select(group => group.Title));
            Assert.False(window.Inspector.ScriptField.IsEffectivelyVisible);
            Assert.False(window.Inspector.MetadataButton.IsEffectivelyVisible);

            window.InspectorPane.FilterBox.Text = "zzz";
            Assert.Empty(Bars(window));
            Assert.Empty(Groups(window));

            window.InspectorPane.FilterBox.Text = "";
            Assert.Equal(["Empty 3D point", "From any thing"], Bars(window).Select(bar => bar.Title));
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
            Assert.False(window.Inspector.AttachButton.IsEffectivelyVisible);
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
}

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class InspectorPaneTests
{
    static bool[] Shown(EditorWindow window) =>
        new[] { window.Inspector.Position, window.Inspector.Rotation, window.Inspector.Scale }.Select(row => row.IsVisible).ToArray();

    static string IconOf(Control control) => control.GetVisualDescendants().OfType<EditorIcon>().First().IconName;

    static string[] Names(MenuFlyout menu) =>
        [.. menu.Items.OfType<MenuItem>().Select(item => ((StackPanel)item.Header!).Children.OfType<TextBlock>().First().Text!)];

    static void Choose(MenuFlyout menu, Func<MenuItem, bool> which) =>
        menu.Items.OfType<MenuItem>().Single(which).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    static void Choose(MenuFlyout menu, string header) => Choose(menu, item => (string?)item.Header == header);

    static PropertyGroup Transform(EditorWindow window) =>
        window.Inspector.GetVisualDescendants().OfType<PropertyGroup>().Single(group => group.Title == "Transform");

    [Fact]
    public void The_history_row_holds_back_forward_and_the_object_chip()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var pane = window.InspectorPane;

            AssertBox(3121, 92, 24, 24, BoxOf(pane.BackButton, window));
            AssertBox(3149, 92, 24, 24, BoxOf(pane.ForwardButton, window));
            AssertBox(3181, 88, 246, 32, BoxOf(pane.Chip, window));
            Assert.Equal("Inspected objects", ToolTip.GetTip(pane.Chip));
        });
    }

    [Fact]
    public void The_filter_row_holds_the_box_and_the_tools_button()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var pane = window.InspectorPane;

            AssertBox(3123, 122, 274, 28, BoxOf(pane.FilterBox, window));
            AssertBox(3403, 124, 24, 24, BoxOf(pane.ToolsButton, window));
            Assert.Equal("Filter properties", pane.FilterBox.PlaceholderText);
        });
    }

    [Fact]
    public void The_property_list_starts_under_the_filter_row_and_fills_the_dock()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            AssertBox(3119, 151, 314, 1121, BoxOf(window.InspectorPane.Viewer, window));
            Assert.Same(window.Inspector, window.InspectorPane.Viewer.Content);
        });
    }

    [Fact]
    public void Each_button_draws_the_icon_of_the_old_editor()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var pane = window.InspectorPane;

            Assert.Equal(["Back", "Forward", "Tools", "Search"],
                [IconOf(pane.BackButton), IconOf(pane.ForwardButton), IconOf(pane.ToolsButton), IconOf(pane.FilterBox)]);
        });
    }

    [Fact]
    public void Back_and_forward_are_off_until_a_second_object_is_shown()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.Outline.SelectedItem = FakeScene.Lamp;

            Assert.False(window.InspectorPane.BackButton.IsEnabled);
            Assert.False(window.InspectorPane.ForwardButton.IsEnabled);
        });
    }

    [Fact]
    public void Back_returns_to_the_last_object_and_forward_comes_back()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var pane = window.InspectorPane;
            window.Outline.SelectedItem = FakeScene.Lamp;
            window.Outline.SelectedItem = FakeScene.Sound;

            ClickOn(pane.BackButton, window);
            Assert.Equal(FakeScene.Lamp.Handle, window.ScenePane.Selected!.Handle);
            Assert.Equal("Lamp", pane.InspectedName.Text);
            Assert.False(pane.BackButton.IsEnabled);
            Assert.True(pane.ForwardButton.IsEnabled);

            ClickOn(pane.ForwardButton, window);
            Assert.Equal(FakeScene.Sound.Handle, window.ScenePane.Selected!.Handle);
            Assert.False(pane.ForwardButton.IsEnabled);
        });
    }

    [Fact]
    public void A_new_object_after_a_step_back_drops_the_steps_forward()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var pane = window.InspectorPane;
            window.Outline.SelectedItem = FakeScene.Lamp;
            window.Outline.SelectedItem = FakeScene.Sound;
            ClickOn(pane.BackButton, window);

            window.ScenePane.Select(FakeScene.Field.Handle);

            Assert.False(pane.ForwardButton.IsEnabled);
            ClickOn(pane.BackButton, window);
            Assert.Equal(FakeScene.Lamp.Handle, window.ScenePane.Selected!.Handle);
        });
    }

    [Fact]
    public void The_chip_names_the_class_and_lists_recent_objects_newest_first_once_each()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var pane = window.InspectorPane;
            window.Outline.SelectedItem = FakeScene.Lamp;
            window.Outline.SelectedItem = FakeScene.Sound;
            window.Outline.SelectedItem = FakeScene.Lamp;
            Assert.Equal("Entity3D › Entity", pane.InspectedClass.Text);

            ClickOn(pane.Chip, window);

            Assert.Equal(["Lamp", "Sound"], Names(pane.Recent));
            Choose(pane.Recent, item => item == pane.Recent.Items[1]);
            Assert.Equal(FakeScene.Sound.Handle, window.ScenePane.Selected!.Handle);
        });
    }

    [Fact]
    public void Collapse_All_and_Expand_All_fold_the_groups_with_rows()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var tools = (MenuFlyout)window.InspectorPane.ToolsButton.Flyout!;
            window.Outline.SelectedItem = FakeScene.Lamp;

            Choose(tools, "Collapse All");
            Assert.False(Transform(window).IsOpen);

            Choose(tools, "Expand All");
            Assert.True(Transform(window).IsOpen);
        });
    }

    [Fact]
    public void A_tool_past_the_folds_says_it_is_not_built()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var tools = (MenuFlyout)window.InspectorPane.ToolsButton.Flyout!;

            Choose(tools, "Copy Properties");

            Assert.Equal("Copy Properties is not built yet.", window.Status);
        });
    }

    [Fact]
    public void With_nothing_selected_the_list_is_empty()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.Equal([false, false, false], Shown(window));
            Assert.DoesNotContain(window.Inspector.GetVisualDescendants().OfType<TextBlock>(), text => text.IsEffectivelyVisible && text.Text is { Length: > 0 });
        });
    }

    [Fact]
    public void A_selected_entity_shows_all_three_rows()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            window.Outline.SelectedItem = FakeScene.Lamp;

            Assert.Equal([true, true, true], Shown(window));
        });
    }

    [Theory]
    [InlineData("pos", new[] { true, false, false })]
    [InlineData("ROT", new[] { false, true, false })]
    [InlineData("  scale ", new[] { false, false, true })]
    [InlineData("o", new[] { true, true, false })]
    [InlineData("zzz", new[] { false, false, false })]
    [InlineData("", new[] { true, true, true })]
    public void The_filter_keeps_the_rows_whose_label_holds_the_text(string text, bool[] expected)
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.Outline.SelectedItem = FakeScene.Lamp;

            window.InspectorPane.FilterBox.Text = text;

            Assert.Equal(expected, Shown(window));
        });
    }

    [Theory]
    [InlineData("pos", "1 of 10 properties match.")]
    [InlineData("o", "8 of 10 properties match.")]
    [InlineData("zzz", "No properties match.")]
    public void A_filter_counts_the_rows_that_match(string text, string expected)
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.Outline.SelectedItem = FakeScene.Lamp;

            window.InspectorPane.FilterBox.Text = text;

            Assert.True(window.Inspector.FilterCount.IsEffectivelyVisible);
            Assert.Equal(expected, window.Inspector.FilterCount.Text);
        });
    }

    [Fact]
    public void With_no_filter_there_is_no_count()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.Outline.SelectedItem = FakeScene.Lamp;

            Assert.False(window.Inspector.FilterCount.IsVisible);
        });
    }

    [Fact]
    public void Clearing_the_filter_brings_the_rows_back()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.Outline.SelectedItem = FakeScene.Lamp;

            window.InspectorPane.FilterBox.Text = "zzz";
            window.InspectorPane.FilterBox.Text = "";

            Assert.Equal([true, true, true], Shown(window));
        });
    }

    [Fact]
    public void A_filter_still_applies_when_the_selection_changes()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.InspectorPane.FilterBox.Text = "pos";

            window.Outline.SelectedItem = FakeScene.Lamp;

            Assert.Equal([true, false, false], Shown(window));
        });
    }

    [Fact]
    public void Clearing_the_selection_hides_the_rows_again()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.Outline.SelectedItem = FakeScene.Lamp;

            window.Outline.SelectedItem = null;

            Assert.Equal([false, false, false], Shown(window));
        });
    }
}

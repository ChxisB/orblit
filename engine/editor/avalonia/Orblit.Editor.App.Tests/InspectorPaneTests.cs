using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
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

    // The heading of a menu has no name, so only the items that do something are named.
    static string[] Names(MenuFlyout menu) =>
        [.. menu.Items.OfType<MenuItem>().Select(AutomationProperties.GetName).OfType<string>()];

    static MenuItem Item(MenuFlyout menu, string name) => menu.Items.OfType<MenuItem>().Single(item => AutomationProperties.GetName(item) == name);

    static void Choose(MenuFlyout menu, Func<MenuItem, bool> which) =>
        menu.Items.OfType<MenuItem>().Single(which).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    static void Choose(MenuFlyout menu, string name) => Item(menu, name).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    static string[] Words(MenuItem item) => [.. ((Control)item.Header!).GetLogicalDescendants().OfType<TextBlock>().Select(text => text.Text!)];

    static HoverCard Card(Control control) => (HoverCard)ToolTip.GetTip(control)!;

    static PropertyGroup Transform(EditorWindow window) =>
        window.Inspector.GetVisualDescendants().OfType<PropertyGroup>().Single(group => group.Title == "Where it is");

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
            Assert.Equal(("Recently inspected", "Pick one to look at it again."), (Card(pane.Chip).Title, Card(pane.Chip).Line));
        });
    }

    [Fact]
    public void The_search_row_holds_the_box_and_the_more_icon()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var pane = window.InspectorPane;

            AssertBox(3123, 122, 272, 28, BoxOf(pane.FilterBox, window));
            AssertBox(3401, 123, 26, 26, BoxOf(pane.MoreButton, window));
            Assert.Equal("Find a setting", pane.FilterBox.PlaceholderText);
            Assert.Equal("More", Card(pane.MoreButton).Title);
            Assert.Equal("More", AutomationProperties.GetName(pane.MoreButton));
            Assert.Empty(pane.MoreButton.GetVisualDescendants().OfType<TextBlock>());
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
    public void Each_button_draws_its_icon()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var pane = window.InspectorPane;

            Assert.Equal(["Back", "Forward", "GuiTabMenuHl", "Search"],
                [IconOf(pane.BackButton), IconOf(pane.ForwardButton), IconOf(pane.MoreButton), IconOf(pane.FilterBox)]);
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
    public void Back_and_forward_say_what_they_do_and_why_they_are_off()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var pane = window.InspectorPane;
            window.Outline.SelectedItem = FakeScene.Lamp;

            Assert.Equal(("Back", "Nothing was inspected before this."), (Card(pane.BackButton).Title, Card(pane.BackButton).Line));
            Assert.Equal(("Forward", "You are at the newest. Go back first."), (Card(pane.ForwardButton).Title, Card(pane.ForwardButton).Line));

            window.Outline.SelectedItem = FakeScene.Sound;

            Assert.Equal("Look at the thing you inspected before. Hold for the list.", Card(pane.BackButton).Line);
        });
    }

    [Fact]
    public void Holding_back_lists_the_recent_things_and_does_not_step()
    {
        UiThread.Run(async () =>
        {
            var window = OpenAtReferenceSize();
            var pane = window.InspectorPane;
            window.Outline.SelectedItem = FakeScene.Lamp;
            window.Outline.SelectedItem = FakeScene.Sound;
            var center = pane.BackButton.TranslatePoint(new Point(12, 12), window)!.Value;

            window.MouseDown(center, MouseButton.Left);
            await Task.Delay(700);
            window.MouseUp(center, MouseButton.Left);

            Assert.True(pane.Recent.IsOpen);
            Assert.Equal(["Sound", "Lamp"], Names(pane.Recent));
            Assert.Equal(FakeScene.Sound.Handle, window.ScenePane.Selected!.Handle);
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
    public void The_chip_names_the_kind_and_class_and_lists_recent_things_newest_first_once_each()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var pane = window.InspectorPane;
            window.Outline.SelectedItem = FakeScene.Lamp;
            window.Outline.SelectedItem = FakeScene.Sound;
            window.Outline.SelectedItem = FakeScene.Lamp;
            Assert.Equal("Empty 3D point", pane.InspectedKind.Text);
            Assert.Equal("Entity3D", pane.InspectedClass.Text);

            ClickOn(pane.Chip, window);

            Assert.Equal("RECENTLY INSPECTED", ((TextBlock)((MenuItem)pane.Recent.Items[0]!).Header!).Text);
            Assert.Equal(["Lamp", "Sound"], Names(pane.Recent));
            Assert.Equal(["Sound", "Audio"], Words((MenuItem)pane.Recent.Items[2]!));
            Choose(pane.Recent, item => item == pane.Recent.Items[2]);
            Assert.Equal(FakeScene.Sound.Handle, window.ScenePane.Selected!.Handle);
        });
    }

    [Fact]
    public void Close_and_open_all_sections_fold_the_groups_with_rows()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var tools = (MenuFlyout)window.InspectorPane.MoreButton.Flyout!;
            window.Outline.SelectedItem = FakeScene.Lamp;

            Choose(tools, "Close all sections");
            Assert.False(Transform(window).IsOpen);

            Choose(tools, "Open all sections");
            Assert.True(Transform(window).IsOpen);
        });
    }

    [Fact]
    public void More_notes_each_item_and_paste_says_why_it_is_off()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var more = (MenuFlyout)window.InspectorPane.MoreButton.Flyout!;
            window.Outline.SelectedItem = FakeScene.Lamp;

            ClickOn(window.InspectorPane.MoreButton, window);

            Assert.Equal(["Open all sections", "Close all sections", "Copy all settings", "Paste settings", "Stop sharing parts"], Names(more));
            Assert.Equal(["Stop sharing parts", "Make Sub-Resources Unique", "So changing this thing leaves others alone"], Words(Item(more, "Stop sharing parts")));
            Assert.False(Item(more, "Paste settings").IsEnabled);
            Assert.Equal(["Paste settings", "Copy settings first."], Words(Item(more, "Paste settings")));

            Choose(more, "Copy all settings");
            more.Hide();
            ClickOn(window.InspectorPane.MoreButton, window);

            Assert.True(Item(more, "Paste settings").IsEnabled);
            Assert.Equal(["Paste settings", "Onto this thing"], Words(Item(more, "Paste settings")));
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
    [InlineData("pos", "1 of 10 settings match.")]
    [InlineData("o", "8 of 10 settings match.")]
    [InlineData("zzz", "No settings match.")]
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

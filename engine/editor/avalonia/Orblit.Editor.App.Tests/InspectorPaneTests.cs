using Avalonia.Controls;
using Avalonia.VisualTree;
using Orblit.Editor.App.Theme;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class InspectorPaneTests
{
    static bool[] Shown(EditorWindow window) =>
        new[] { window.Inspector.Position, window.Inspector.Rotation, window.Inspector.Scale }.Select(row => row.IsVisible).ToArray();

    static string IconOf(Control control) => control.GetVisualDescendants().OfType<EditorIcon>().First().IconName;

    [Fact]
    public void The_history_row_holds_back_forward_and_the_menu()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var pane = window.InspectorPane;

            AssertBox(3158, 80, 24, 24, BoxOf(pane.BackButton, window));
            AssertBox(3186, 80, 24, 24, BoxOf(pane.ForwardButton, window));
            AssertBox(3406, 80, 24, 24, BoxOf(pane.HistoryButton, window));
        });
    }

    [Fact]
    public void The_filter_row_holds_the_box_and_the_tools_button()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var pane = window.InspectorPane;

            AssertBox(3160, 110, 240, 28, BoxOf(pane.FilterBox, window));
            AssertBox(3406, 112, 24, 24, BoxOf(pane.ToolsButton, window));
            Assert.Equal("Filter Properties", pane.FilterBox.PlaceholderText);
        });
    }

    [Fact]
    public void The_property_list_starts_under_the_filter_row_and_fills_the_dock()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            AssertBox(3156, 139, 280, 1136, BoxOf(window.InspectorPane.Viewer, window));
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

            Assert.Equal(["Back", "Forward", "GuiTabMenuHl", "Tools", "Search"],
                [IconOf(pane.BackButton), IconOf(pane.ForwardButton), IconOf(pane.HistoryButton), IconOf(pane.ToolsButton), IconOf(pane.FilterBox)]);
        });
    }

    [Fact]
    public void Back_and_forward_are_off_because_no_history_is_kept()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.False(window.InspectorPane.BackButton.IsEnabled);
            Assert.False(window.InspectorPane.ForwardButton.IsEnabled);
        });
    }

    [Fact]
    public void The_history_menu_says_it_is_not_built()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            ClickOn(window.InspectorPane.HistoryButton, window);

            Assert.Equal("The history of inspected objects is not built yet.", window.Status);
        });
    }

    [Fact]
    public void The_tools_button_says_it_is_not_built()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            ClickOn(window.InspectorPane.ToolsButton, window);

            Assert.Equal("The inspector tools are not built yet.", window.Status);
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

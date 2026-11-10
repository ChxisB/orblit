using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using Orblit.Editor.Bridge;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class TreeGuidesTests
{
    static readonly Color Vertical = Color.Parse("#343434");
    static readonly Color Stub = Color.Parse("#3f3f3f");
    static readonly Color Lit = Color.Parse("#545454");

    static TreeViewItem[] Items(TreeView tree) => [.. tree.GetVisualDescendants().OfType<TreeViewItem>()];

    static Rect RowBox(EditorWindow window, TreeViewItem item) =>
        BoxOf(item.GetVisualDescendants().OfType<Border>().First(border => border.Name == "PART_LayoutRoot"), window);

    // The lines in window coordinates, with the colour each is drawn in.
    static (Rect Area, Color Colour)[] Drawn(TreeGuides guides, EditorWindow window)
    {
        var origin = BoxOf(guides, window).Position;
        return [.. guides.Lines().Select(line => (new Rect(line.Area.Position + origin, line.Area.Size), ((ISolidColorBrush)line.Fill).Color))];
    }

    static void Select(EditorWindow window, OutlineNode node)
    {
        window.Outline.SelectedItem = node;
        window.UpdateLayout();
    }

    [Fact]
    public void Nothing_is_drawn_while_nothing_is_selected()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.Empty(window.ScenePane.Guides.Lines());
            Assert.False(window.ScenePane.Guides.IsHitTestVisible);
        });
    }

    [Fact]
    public void The_children_of_the_selected_row_get_a_faint_vertical_and_a_stub_each()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            Select(window, FakeScene.Field);
            var items = Items(window.Outline);
            var lamp = RowBox(window, items[1]);
            var sound = RowBox(window, items[2]);

            var drawn = Drawn(window.ScenePane.Guides, window);

            Assert.Equal(3, drawn.Length);
            AssertBox(15, lamp.Y + 1, 2, sound.Y + 12 - lamp.Y - 1, drawn[0].Area);
            AssertBox(16, lamp.Y + 11, 18, 1, drawn[1].Area);
            AssertBox(16, sound.Y + 11, 18, 1, drawn[2].Area);
            Assert.Equal([Vertical, Stub, Stub], drawn.Select(line => line.Colour));
        });
    }

    [Fact]
    public void A_selected_child_lights_the_vertical_down_to_it_and_wraps_its_stub_in_a_halo()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            Select(window, FakeScene.Lamp);
            var lamp = RowBox(window, Items(window.Outline)[1]);

            var drawn = Drawn(window.ScenePane.Guides, window);

            Assert.Equal(4, drawn.Length);
            AssertBox(15, lamp.Y, 2, 12, drawn[0].Area);
            AssertBox(17, lamp.Y + 10, 17, 1, drawn[1].Area);
            AssertBox(15, lamp.Y + 11, 19, 1, drawn[2].Area);
            AssertBox(15, lamp.Y + 12, 19, 1, drawn[3].Area);
            Assert.Equal([Lit, Stub, Lit, Stub], drawn.Select(line => line.Colour));
        });
    }

    [Fact]
    public void A_later_selected_child_lights_more_of_the_vertical_and_its_siblings_have_no_stub()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            Select(window, FakeScene.Sound);
            var items = Items(window.Outline);
            var lamp = RowBox(window, items[1]);
            var sound = RowBox(window, items[2]);

            var drawn = Drawn(window.ScenePane.Guides, window);

            Assert.Equal(4, drawn.Length);
            AssertBox(15, lamp.Y, 2, sound.Y + 12 - lamp.Y, drawn[0].Area);
            AssertBox(15, sound.Y + 11, 19, 1, drawn[2].Area);
        });
    }

    [Fact]
    public void A_shut_parent_draws_nothing()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            Select(window, FakeScene.Field);
            Items(window.Outline)[0].IsExpanded = false;
            window.UpdateLayout();

            Assert.Empty(window.ScenePane.Guides.Lines());
        });
    }

    [Fact]
    public void A_nested_parent_draws_12_pixels_further_right()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var folder = Items(window.Scenes.Tree).Single(item => item.DataContext is FileNode { Name: "levels" });
            folder.IsExpanded = true;
            window.Scenes.Tree.SelectedItem = folder.DataContext;
            window.UpdateLayout();
            var scene = RowBox(window, Items(window.Scenes.Tree).Single(item => item.DataContext is FileNode { Name: "second.oscn" }));

            var drawn = Drawn(window.Scenes.Guides, window);

            Assert.Contains(drawn, line => line.Area == new Rect(27, scene.Y + 1, 2, 11) && line.Colour == Vertical);
            Assert.Contains(drawn, line => line.Area == new Rect(28, scene.Y + 11, 18, 1) && line.Colour == Stub);
        });
    }
}

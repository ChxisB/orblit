using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Orblit.Editor.App.Shell;
using Orblit.Editor.Bridge;
using static Orblit.Editor.App.Tests.PlanarViewportTests;
using static Orblit.Editor.App.Tests.ReferenceWindow;
using Vector2 = System.Numerics.Vector2;

namespace Orblit.Editor.App.Tests;

public sealed class PlanarToolsTests
{
    static FixedToggle ToolOf(EditorWindow window, string tip) =>
        window.Center.Planar.Tools.GetLogicalDescendants().OfType<FixedToggle>().Single(tool => (string?)ToolTip.GetTip(tool) == tip);

    static double ZeroAt(Ruler ruler) => ruler.Marks().Single(mark => mark.Label == "0").At;

    static string? ZoomShown(EditorWindow window) => (string?)window.Center.Planar.ZoomButton.Content;

    [Fact]
    public void The_zoom_pill_steps_three_notches_and_the_percent_goes_back_to_100()
    {
        UiThread.Run(() =>
        {
            var (window, scene, _) = OpenIn2D();
            var planar = window.Center.Planar;
            Assert.Equal("100%", ZoomShown(window));

            ClickOn(planar.ZoomInButton, window);
            Assert.Equal("133%", ZoomShown(window));
            Assert.Equal(1.331f, scene.PlanarView.Zoom, 3);

            ClickOn(planar.ZoomButton, window);
            Assert.Equal("100%", ZoomShown(window));

            ClickOn(planar.ZoomOutButton, window);
            Assert.Equal("75%", ZoomShown(window));
        });
    }

    [Fact]
    public void Center_takes_the_view_back_to_the_origin_and_the_top_ruler_follows()
    {
        UiThread.Run(() =>
        {
            var (window, scene, _) = OpenIn2D(scene => scene.PlanarView = new PlanarView(new Vector2(-50, 0), 1));
            var ruler = window.Center.Planar.TopRuler;
            var before = ZeroAt(ruler);

            ClickOn(window.Center.Planar.CenterButton, window);

            Assert.Equal(PlanarView.Start, scene.PlanarView);
            // The view is in frame pixels, and a ruler pixel holds the scaling's worth of them.
            Assert.Equal(50 / TopLevel.GetTopLevel(window)!.RenderScaling, before - ZeroAt(ruler), 3);
        });
    }

    [Fact]
    public void Show_Rulers_hides_the_rulers_and_shows_them_again()
    {
        UiThread.Run(() =>
        {
            var (window, _, _) = OpenIn2D();
            var planar = window.Center.Planar;

            planar.RulersItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.False(planar.Rulers.IsVisible);
            Assert.False(planar.RulersItem.IsChecked);

            planar.RulersItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.True(planar.Rulers.IsVisible);
            Assert.True(planar.RulersItem.IsChecked);
        });
    }

    [Fact]
    public void In_Move_mode_a_drag_away_from_the_mark_moves_the_selection()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = OpenIn2D();
            var away = InWindow(viewport, window, new Vector2(80, 40));

            ClickOn(window.Center.Planar.MoveButton, window);
            window.MouseDown(away, MouseButton.Left);
            window.MouseMove(away + new Point(10, 0));
            window.MouseUp(away + new Point(10, 0), MouseButton.Left);

            Assert.True(window.Center.Planar.MoveButton.IsChecked);
            Assert.False(ToolOf(window, "Select mode").IsChecked);
            Assert.Equal(FakeScene.Lamp.Handle, Assert.Single(scene.PlanarMoved).Entity);
        });
    }

    [Fact]
    public void In_Select_mode_a_drag_away_from_the_mark_moves_nothing()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = OpenIn2D();
            var away = InWindow(viewport, window, new Vector2(80, 40));

            window.MouseDown(away, MouseButton.Left);
            window.MouseMove(away + new Point(10, 0));
            window.MouseUp(away + new Point(10, 0), MouseButton.Left);

            Assert.Empty(scene.PlanarMoved);
        });
    }

    [Fact]
    public void Lock_in_the_2D_pill_locks_the_selection_and_lights_with_it()
    {
        UiThread.Run(() =>
        {
            var (window, _, _) = OpenIn2D();
            var locks = ToolOf(window, "Lock the selection");
            Assert.True(locks.IsEnabled);
            Assert.False(locks.IsChecked);

            ClickOn(locks, window);

            Assert.True(window.ScenePane.Selected!.Locked);
            Assert.True(locks.IsChecked);
        });
    }

    [Fact]
    public void A_tool_that_is_not_built_says_so()
    {
        UiThread.Run(() =>
        {
            var (window, _, _) = OpenIn2D();

            ClickOn(ToolOf(window, "Rotate mode"), window);

            Assert.Equal("Rotate mode is not built yet.", window.Status);
        });
    }

    [Fact]
    public void A_scene_with_no_2D_entity_gets_the_note_and_Switch_to_3D_leaves_the_2D_view()
    {
        UiThread.Run(() =>
        {
            var (window, scene, _) = OpenIn2D(scene => scene.Pivots.Clear());
            var planar = window.Center.Planar;
            Assert.True(planar.Note.IsVisible);
            Assert.Equal("Field is a 3D scene", planar.NoteTitle.Text);

            ClickOn(planar.SwitchButton, window);

            Assert.False(scene.Planar[^1]);
            Assert.True(window.Center.Toolbar.IsVisible);
            Assert.False(planar.IsVisible);
        });
    }

    [Fact]
    public void A_scene_with_a_2D_entity_gets_no_note()
    {
        UiThread.Run(() =>
        {
            var (window, _, _) = OpenIn2D();

            Assert.False(window.Center.Planar.Note.IsVisible);
        });
    }
}

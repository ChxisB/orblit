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
    // A tool is found by the plain name its card shows, as a person reads it.
    static FixedToggle ToolOf(EditorWindow window, string name) =>
        window.Center.Planar.Tools.GetLogicalDescendants().OfType<FixedToggle>().Single(tool => ToolTip.GetTip(tool) is HoverCard card && card.Title == name);

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
            Assert.False(ToolOf(window, "Pick").IsChecked);
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
            var locks = ToolOf(window, "Lock");
            Assert.True(locks.IsEnabled);
            Assert.False(locks.IsChecked);

            ClickOn(locks, window);

            Assert.True(window.ScenePane.Selected!.Locked);
            Assert.True(locks.IsChecked);
        });
    }

    static void DragFrom(EditorWindow window, Point from, RawInputModifiers keys = RawInputModifiers.None)
    {
        window.MouseDown(from, MouseButton.Left, keys);
        window.MouseMove(from + new Point(10, 0), keys);
        window.MouseUp(from + new Point(10, 0), MouseButton.Left, keys);
    }

    [Theory]
    [InlineData("Turn", "Rotate", "Rotated Lamp. Save keeps the change.")]
    [InlineData("Resize", "Scale", "Scaled Lamp. Save keeps the change.")]
    public void Rotate_and_Scale_modes_take_a_drag_anywhere(string tip, string verb, string status)
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = OpenIn2D();

            ClickOn(ToolOf(window, tip), window);
            DragFrom(window, InWindow(viewport, window, new Vector2(80, 40)));

            Assert.Equal(verb, Assert.Single(scene.PlanarDrags).Verb);
            Assert.Equal(FakeScene.Lamp.Handle, scene.PlanarDrags[0].Entity);
            Assert.Equal(status, window.Status);
            Assert.False(ToolOf(window, "Pick").IsChecked);
        });
    }

    [Fact]
    public void In_Pan_mode_a_drag_on_the_mark_pans_and_moves_nothing()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = OpenIn2D();
            var pans = 0;
            viewport.PlanarPanned += _ => pans++;

            ClickOn(window.Center.Planar.PanButton, window);
            DragFrom(window, InWindow(viewport, window, Pivot));

            Assert.Empty(scene.PlanarMoved);
            Assert.Equal(1, pans);
        });
    }

    [Fact]
    public void A_2D_drag_is_one_step_to_undo()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = OpenIn2D();
            var onPivot = InWindow(viewport, window, Pivot);

            window.MouseDown(onPivot, MouseButton.Left);
            window.MouseMove(onPivot + new Point(5, 0));
            window.MouseMove(onPivot + new Point(10, 0));
            window.MouseUp(onPivot + new Point(10, 0), MouseButton.Left);
            Assert.Equal(2, scene.PlanarMoved.Count);
            Assert.Equal(1, scene.DragsEnded);

            window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Z, KeyModifiers = window.CommandKey });

            Assert.Equal(Pivot, scene.Pivots[FakeScene.Lamp.Handle]);
            Assert.Equal("Undid Moved Lamp. Save keeps the change.", window.Status);
        });
    }

    [Theory]
    [InlineData(false, RawInputModifiers.None, false)]
    [InlineData(true, RawInputModifiers.None, true)]
    [InlineData(false, RawInputModifiers.Meta, true)]
    [InlineData(true, RawInputModifiers.Control, false)]
    public void Snap_in_the_2D_pill_lights_with_the_toolbar_and_a_snapped_move_keeps_to_the_grid(bool lit, RawInputModifiers keys, bool snaps)
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = OpenIn2D();
            var snap = ToolOf(window, "Snap");
            if (lit)
            {
                ClickOn(snap, window);
            }

            DragFrom(window, InWindow(viewport, window, Pivot), keys);

            Assert.Equal(lit, window.Toolbar.SnapButton.IsChecked);
            Assert.Equal(lit, snap.IsChecked);
            var step = (float)window.Center.Planar.GridStep;
            Assert.Equal(snaps ? new GizmoSnap(step, 15, 0.1f) : null, Assert.Single(scene.SnapsSeen));
        });
    }

    [Fact]
    public void The_grid_button_and_Show_Grid_hide_the_grid_and_stay_in_step()
    {
        UiThread.Run(() =>
        {
            var (window, _, _) = OpenIn2D();
            var planar = window.Center.Planar;
            Assert.True(planar.Grid.IsVisible);
            Assert.True(planar.GridButton.IsChecked);

            ClickOn(planar.GridButton, window);
            Assert.False(planar.Grid.IsVisible);
            Assert.False(planar.GridItem.IsChecked);

            planar.GridItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.True(planar.Grid.IsVisible);
            Assert.True(planar.GridButton.IsChecked);
        });
    }

    [Fact]
    public void Every_grid_line_falls_on_a_ruler_tick_and_the_axes_cross_at_the_origin()
    {
        UiThread.Run(() =>
        {
            var grid = new PlanarGrid();
            grid.Measure(new Size(400, 300));
            grid.Arrange(new Rect(0, 0, 400, 300));
            grid.Show(new Vector2(30, -10), 0.5);

            // Half a world unit a pixel gives a minor tick of 5 units, so a line every 10 units, which is 20 pixels.
            Assert.Equal(10, grid.Step);
            var across = grid.Across();
            Assert.All(across.Zip(across.Skip(1)), pair => Assert.Equal(20, pair.Second - pair.First, 6));
            Assert.Equal(new Point(200 - 60, 150 + 20), grid.Origin);
            Assert.Contains(grid.Origin.X, across);
            Assert.Contains(grid.Origin.Y, grid.Down());
        });
    }

    static KeyEventArgs Press(EditorWindow window, Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        var press = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers };
        window.RaiseEvent(press);
        return press;
    }

    [Fact]
    public void Every_2D_tool_shows_its_word_and_its_key()
    {
        UiThread.Run(() =>
        {
            var (window, _, _) = OpenIn2D();

            // Headless runs take Control as the command key.
            Assert.Equal(
                ["Pick Q", "Move W", "Turn E", "Resize R", "Pan", "Snap Y", "Grid", "Lock ⌃L", "Group ⌃G"],
                window.Center.Planar.Tools.GetLogicalDescendants().OfType<FixedToggle>().Select(tool => string.Join(" ", Texts(tool))));
        });
    }

    [Fact]
    public void In_the_2D_view_Q_W_E_and_R_pick_its_tools_and_the_command_key_zooms()
    {
        UiThread.Run(() =>
        {
            var (window, _, _) = OpenIn2D();
            var planar = window.Center.Planar;

            Assert.True(Press(window, Key.R).Handled);
            Assert.Equal(PlanarTool.Scale, planar.Tool);
            Assert.False(window.Toolbar.ScaleButton.IsChecked);
            Press(window, Key.E);
            Assert.Equal(PlanarTool.Rotate, planar.Tool);
            Press(window, Key.W);
            Assert.Equal(PlanarTool.Move, planar.Tool);
            Press(window, Key.Q);
            Assert.Equal(PlanarTool.Select, planar.Tool);

            Assert.True(Press(window, Key.OemPlus, window.CommandKey).Handled);
            Assert.Equal("133%", ZoomShown(window));
            Press(window, Key.D0, window.CommandKey);
            Assert.Equal("100%", ZoomShown(window));
            Press(window, Key.OemMinus, window.CommandKey);
            Assert.Equal("75%", ZoomShown(window));
        });
    }

    [Fact]
    public void The_view_menu_notes_each_item_and_actual_size_shows_its_key()
    {
        UiThread.Run(() =>
        {
            var (window, _, _) = OpenIn2D();
            var items = window.Center.Planar.ViewMenu.Items;

            Assert.Equal(
                ["Rulers The pixel rulers at the edges", "Grid Lines to line things up with", "-", "Center the view Puts the point 0, 0 in the middle", "Actual size One pixel on screen is one in the game"],
                items.Select(item => item is MenuItem menu ? string.Join(" ", Texts(menu)) : "-"));
            Assert.Equal("⌃0", Parts.KeyText(((MenuItem)items[^1]!).InputGesture!));
        });
    }

    [Fact]
    public void The_note_says_why_the_view_is_empty_and_offers_three_ways_on_with_their_keys()
    {
        UiThread.Run(() =>
        {
            var (window, _, _) = OpenIn2D(scene => scene.Pivots.Clear());
            var planar = window.Center.Planar;

            Assert.Equal("This view shows 2D scenes and menus. Field has no 2D things yet.", planar.NoteLine.Text);
            Assert.Equal(
                ["Go to 3D ⌃2", "Open a 2D scene… ⌃⇧O", "Add a 2D thing ⌃A"],
                new[] { planar.SwitchButton, planar.OpenButton, planar.AddButton }.Select(button => string.Join(" ", Texts(button))));

            ClickOn(planar.AddButton, window);

            Assert.True(window.Tools.AddDialog.IsVisible);
        });
    }

    [Fact]
    public void A_scene_with_no_2D_entity_gets_the_note_and_Go_to_3D_leaves_the_2D_view()
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

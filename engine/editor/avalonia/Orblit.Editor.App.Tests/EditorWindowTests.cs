using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Orblit.Editor.Bridge;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class EditorWindowTests
{
    static EditorWindow Open(IEditorScene scene)
    {
        var window = new EditorWindow();
        window.Show();
        window.Load(scene);
        return window;
    }

    static OutlineNode Root(EditorWindow window) => window.Outline.ItemsSource!.Cast<OutlineNode>().Single();

    static KeyEventArgs Press(InputElement target, Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        var press = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers };
        target.RaiseEvent(press);
        return press;
    }

    static void Commit(TextBox box) => box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

    [Fact]
    public void Starts_with_nothing_to_save_or_play()
    {
        UiThread.Run(() =>
        {
            var window = new EditorWindow();

            Assert.False(window.SaveButton.IsEnabled);
            Assert.False(window.PlayButton.IsEnabled);
            Assert.Equal("Starting the engine.", window.Status);
            Assert.Equal("Unnamed Project", window.Title);
        });
    }

    [Fact]
    public void The_window_wears_the_planet_icon()
    {
        UiThread.Run(() =>
        {
            var window = new EditorWindow();

            Assert.NotNull(window.Icon);
        });
    }

    [Fact]
    public void Loading_a_scene_shows_its_root()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());

            Assert.Equal([FakeScene.Field], window.Outline.ItemsSource!.Cast<OutlineNode>());
            Assert.True(window.SaveButton.IsEnabled);
            Assert.Equal("Opened Field.", window.Status);
        });
    }

    [Fact]
    public void Selecting_an_entity_shows_its_position()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());

            window.Outline.SelectedItem = FakeScene.Lamp;

            Assert.Equal(["1", "2", "3"], window.Inspector.Position.Axes.Select(box => box.Text));
            Assert.All(window.Inspector.Position.Axes, box => Assert.True(box.IsEnabled));
        });
    }

    [Fact]
    public void Selecting_an_entity_shows_its_rotation_and_scale()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());

            window.Outline.SelectedItem = FakeScene.Lamp;

            Assert.Equal(["10", "20", "30"], window.Inspector.Rotation.Axes.Select(box => box.Text));
            Assert.Equal(["4", "5", "6"], window.Inspector.Scale.Axes.Select(box => box.Text));
        });
    }

    [Fact]
    public void An_object_that_is_not_an_entity_has_no_transform_to_edit()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());

            window.Outline.SelectedItem = FakeScene.Sound;

            var boxes = new[] { window.Inspector.Position, window.Inspector.Rotation, window.Inspector.Scale }.SelectMany(row => row.Axes);
            Assert.All(boxes, box => Assert.False(box.IsEnabled));
            Assert.All(boxes, box => Assert.Equal("", box.Text));
        });
    }

    [Fact]
    public void A_typed_position_moves_the_entity()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = Open(scene);
            window.Outline.SelectedItem = FakeScene.Lamp;

            window.Inspector.Position.Axes[0].Text = "4.5";
            Commit(window.Inspector.Position.Axes[0]);

            Assert.Equal(new Vector3(4.5f, 2, 3), scene.PositionOf(FakeScene.Lamp.Handle));
            Assert.Equal("Edited. Save keeps the change.", window.Status);
        });
    }

    [Fact]
    public void Text_that_is_not_a_number_is_put_back()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = Open(scene);
            window.Outline.SelectedItem = FakeScene.Lamp;

            window.Inspector.Position.Axes[1].Text = "high";
            Commit(window.Inspector.Position.Axes[1]);

            Assert.Equal(new Vector3(1, 2, 3), scene.PositionOf(FakeScene.Lamp.Handle));
            Assert.Equal("2", window.Inspector.Position.Axes[1].Text);
        });
    }

    [Fact]
    public void A_typed_rotation_turns_only_the_rotation()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = Open(scene);
            window.Outline.SelectedItem = FakeScene.Lamp;

            window.Inspector.Rotation.Axes[2].Text = "45";
            Commit(window.Inspector.Rotation.Axes[2]);

            Assert.Equal(new Vector3(10, 20, 45), scene.RotationOf(FakeScene.Lamp.Handle));
            Assert.Equal(new Vector3(1, 2, 3), scene.PositionOf(FakeScene.Lamp.Handle));
            Assert.Equal(new Vector3(4, 5, 6), scene.ScaleOf(FakeScene.Lamp.Handle));
            Assert.Equal("Edited. Save keeps the change.", window.Status);
        });
    }

    [Fact]
    public void A_typed_scale_resizes_only_the_scale()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = Open(scene);
            window.Outline.SelectedItem = FakeScene.Lamp;

            window.Inspector.Scale.Axes[1].Text = "2";
            Commit(window.Inspector.Scale.Axes[1]);

            Assert.Equal(new Vector3(4, 2, 6), scene.ScaleOf(FakeScene.Lamp.Handle));
            Assert.Equal(new Vector3(1, 2, 3), scene.PositionOf(FakeScene.Lamp.Handle));
            Assert.Equal(new Vector3(10, 20, 30), scene.RotationOf(FakeScene.Lamp.Handle));
            Assert.Equal("Edited. Save keeps the change.", window.Status);
        });
    }

    [Fact]
    public void A_rotation_is_shown_as_the_scene_answers()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());
            window.Outline.SelectedItem = FakeScene.Lamp;

            window.Inspector.Rotation.Axes[2].Text = "190";
            Commit(window.Inspector.Rotation.Axes[2]);

            Assert.Equal("-170", window.Inspector.Rotation.Axes[2].Text);
        });
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void A_value_that_is_not_finite_is_put_back(string typed)
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = Open(scene);
            window.Outline.SelectedItem = FakeScene.Lamp;

            window.Inspector.Scale.Axes[0].Text = typed;
            Commit(window.Inspector.Scale.Axes[0]);

            Assert.Equal(new Vector3(4, 5, 6), scene.ScaleOf(FakeScene.Lamp.Handle));
            Assert.Equal("4", window.Inspector.Scale.Axes[0].Text);
            Assert.Equal("Opened Field.", window.Status);
        });
    }

    [Fact]
    public void Text_with_the_value_already_shown_changes_nothing()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());
            window.Outline.SelectedItem = FakeScene.Lamp;

            window.Inspector.Position.Axes[0].Text = "1";
            Commit(window.Inspector.Position.Axes[0]);

            Assert.Equal("Opened Field.", window.Status);
        });
    }

    [Fact]
    public void Save_writes_the_scene()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = Open(scene);

            window.SaveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal(1, scene.Saves);
            Assert.Equal("Saved.", window.Status);
        });
    }

    [Fact]
    public void Play_saves_the_edits_first_and_then_runs_the_game()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = Open(scene);
            MakeAnEdit(window);

            window.PlayButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal(1, scene.Saves);
            Assert.Equal(1, scene.Plays);
        });
    }

    [Fact]
    public void Play_starts_the_game_with_the_breakpoints_of_every_script()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = Open(scene);
            window.Graph.Breakpoints.Toggle("res://a.ograph#main", 2);
            window.Graph.Breakpoints.Toggle("res://b.ograph#_start", 5);

            window.PlayButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal([("res://a.ograph#main", 2), ("res://b.ograph#_start", 5)], scene.PlayedWith.Order());
        });
    }

    [Fact]
    public void A_game_the_editor_cannot_link_to_is_not_started_and_the_reason_shows()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { PlayError = new IOException("The debug port could not be opened.") };
            var window = Open(scene);

            window.PlayButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal("The debug port could not be opened.", window.Status);
            Assert.False(scene.IsPlaying);
        });
    }

    [Fact]
    public void Each_poll_takes_in_what_the_game_said()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = Open(scene);
            var before = scene.Channel.Polls;

            window.Poll();
            window.Poll();

            Assert.Equal(before + 2, scene.Channel.Polls);
        });
    }

    [Fact]
    public void A_failed_save_blocks_play_and_says_why()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { SaveFails = true };
            var window = Open(scene);
            MakeAnEdit(window);

            window.PlayButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal(0, scene.Plays);
            Assert.Contains("Error code 7", window.Status);
        });
    }

    [Fact]
    public void Add_makes_an_entity_under_the_selected_one_and_selects_it()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());
            window.Outline.SelectedItem = FakeScene.Lamp;
            window.Tools.Class.SelectedItem = "Marker3D";
            window.Tools.NameBox.Text = " Spawn ";

            Click(window.Tools.AddButton);

            var lamp = Root(window).Find(FakeScene.Lamp.Handle)!;
            var made = Assert.Single(lamp.Children);
            Assert.Equal(("Spawn", "Marker3D"), (made.Name, made.ClassName));
            Assert.Equal(made, window.Outline.SelectedItem);
            Assert.Equal("Added Spawn.", window.Status);
        });
    }

    [Fact]
    public void Add_with_no_selection_and_no_name_uses_the_root_and_the_class_name()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());
            window.Tools.Class.SelectedItem = "Camera3D";

            Click(window.Tools.AddButton);

            var made = Root(window).Children.Last();
            Assert.Equal(("Camera3D", "Camera3D"), (made.Name, made.ClassName));
        });
    }

    [Fact]
    public void Selecting_an_entity_draws_the_box_round_it_and_clearing_draws_none()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = Open(scene);
            scene.Highlighted.Clear();

            window.Outline.SelectedItem = FakeScene.Lamp;
            window.Outline.SelectedItem = null;

            Assert.Equal([FakeScene.Lamp.Handle, 0], scene.Highlighted);
        });
    }

    [Fact]
    public void A_typed_position_redraws_the_box()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = Open(scene);
            window.Outline.SelectedItem = FakeScene.Lamp;
            scene.Highlighted.Clear();

            window.Inspector.Position.Axes[0].Text = "4.5";
            Commit(window.Inspector.Position.Axes[0]);

            Assert.Equal([FakeScene.Lamp.Handle], scene.Highlighted);
        });
    }

    [Fact]
    public void Frame_points_the_viewport_at_the_selected_entity()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = Open(scene);
            window.Outline.SelectedItem = FakeScene.Lamp;

            Click(window.FrameButton);

            Assert.Equal([FakeScene.Lamp.Handle], scene.Framed);
            Assert.Equal("Framed Lamp.", window.Status);
        });
    }

    [Fact]
    public void Frame_is_on_only_for_an_entity_with_a_position()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());

            Assert.False(window.FrameButton.IsEnabled);

            window.Outline.SelectedItem = FakeScene.Sound;
            Assert.False(window.FrameButton.IsEnabled);

            window.Outline.SelectedItem = FakeScene.Lamp;
            Assert.True(window.FrameButton.IsEnabled);

            window.Outline.SelectedItem = null;
            Assert.False(window.FrameButton.IsEnabled);
        });
    }

    [Fact]
    public void Duplicate_puts_a_copy_after_the_entity_and_selects_it()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());
            window.Outline.SelectedItem = FakeScene.Lamp;

            Click(window.Tools.DuplicateButton);

            Assert.Equal(["Lamp", "Lamp2", "Sound"], Root(window).Children.Select(c => c.Name));
            var copy = (OutlineNode)window.Outline.SelectedItem!;
            Assert.Equal("Lamp2", copy.Name);
            Assert.NotEqual(FakeScene.Lamp.Handle, copy.Handle);
            Assert.Equal(["1", "2", "3"], window.Inspector.Position.Axes.Select(box => box.Text));
            Assert.Equal("Duplicated Lamp.", window.Status);
        });
    }

    [Fact]
    public void Duplicate_takes_the_children_along()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());
            window.Outline.SelectedItem = FakeScene.Lamp;
            window.Tools.Class.SelectedItem = "Marker3D";
            window.Tools.NameBox.Text = "Bulb";
            Click(window.Tools.AddButton);
            window.Outline.SelectedItem = Root(window).Find(FakeScene.Lamp.Handle);

            Click(window.Tools.DuplicateButton);

            var original = Root(window).Find(FakeScene.Lamp.Handle)!;
            var copy = (OutlineNode)window.Outline.SelectedItem!;
            var bulb = Assert.Single(copy.Children);
            Assert.Equal(("Bulb", "Marker3D"), (bulb.Name, bulb.ClassName));
            Assert.NotEqual(Assert.Single(original.Children).Handle, bulb.Handle);
        });
    }

    [Fact]
    public void Rename_changes_the_selected_name_and_keeps_it_selected()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());
            window.Outline.SelectedItem = FakeScene.Lamp;
            window.Tools.NameBox.Text = "Sun";

            Click(window.Tools.RenameButton);

            Assert.Equal("Sun", Root(window).Find(FakeScene.Lamp.Handle)!.Name);
            Assert.Equal("Sun", ((OutlineNode)window.Outline.SelectedItem!).Name);
            Assert.Equal("Renamed Lamp.", window.Status);
        });
    }

    [Fact]
    public void Remove_takes_the_entity_out_and_clears_the_selection()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());
            window.Outline.SelectedItem = FakeScene.Lamp;

            Click(window.Tools.RemoveButton);

            Assert.Equal(["Sound"], Root(window).Children.Select(c => c.Name));
            Assert.Null(window.Outline.SelectedItem);
            Assert.Equal("Removed Lamp.", window.Status);
        });
    }

    [Fact]
    public void Tools_are_off_when_they_have_nothing_to_act_on()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());

            Assert.True(window.Tools.AddButton.IsEnabled);
            Assert.False(window.Tools.DuplicateButton.IsEnabled);
            Assert.False(window.Tools.RenameButton.IsEnabled);
            Assert.False(window.Tools.RemoveButton.IsEnabled);

            window.Outline.SelectedItem = Root(window);

            Assert.False(window.Tools.DuplicateButton.IsEnabled);
            Assert.True(window.Tools.RenameButton.IsEnabled);
            Assert.False(window.Tools.RemoveButton.IsEnabled);

            window.Outline.SelectedItem = FakeScene.Lamp;

            Assert.True(window.Tools.DuplicateButton.IsEnabled);
            Assert.False(new EditorWindow().Tools.AddButton.IsEnabled);
        });
    }

    [Fact]
    public void A_refused_change_says_why_and_changes_nothing()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());
            window.Outline.SelectedItem = FakeScene.Lamp;
            window.Tools.NameBox.Text = "  ";

            Click(window.Tools.RenameButton);

            Assert.Equal("An entity needs a name.", window.Status);
            Assert.Equal("Lamp", Root(window).Find(FakeScene.Lamp.Handle)!.Name);
        });
    }

    static bool NoFrame(out int width, out int height, ref byte[] rgba)
    {
        width = 0;
        height = 0;
        return false;
    }

    [Fact]
    public void Without_a_viewport_the_middle_says_why()
    {
        UiThread.Run(() =>
        {
            var window = new EditorWindow();

            var note = Assert.IsType<TextBlock>(window.Viewport.Content);
            Assert.Contains("--headless", note.Text);
        });
    }

    [Fact]
    public void A_viewport_takes_the_middle_area()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());
            var viewport = new ViewportControl(() => false, NoFrame);

            window.ShowViewport(viewport);

            Assert.Same(viewport, window.Viewport.Content);
            // The viewport shares a cell with its overlay, and the cell is the third row of the middle area.
            var cell = Assert.IsType<Grid>(window.Viewport.Parent);
            Assert.Same(window.Center, cell.Parent);
            Assert.Equal(2, Grid.GetRow(cell));
        });
    }

    [Fact]
    public void A_viewport_whose_engine_quit_says_so_in_the_status()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());
            var viewport = new ViewportControl(() => true, NoFrame);
            window.ShowViewport(viewport);

            viewport.Tick();

            Assert.Equal("The engine window was closed. Restart the editor to see the scene.", window.Status);
        });
    }

    [Fact]
    public void Closing_the_window_stops_the_viewport_before_a_later_close_handler_frees_the_engine()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());
            var viewport = new ViewportControl(() => false, NoFrame);
            window.ShowViewport(viewport);
            Assert.True(viewport.IsRunning);
            // The app frees the engine in a Closed handler. Avalonia detaches the control before it raises Closed.
            var runningWhenEngineGoes = true;
            window.Closed += (_, _) => runningWhenEngineGoes = viewport.IsRunning;

            window.Close();

            Assert.False(runningWhenEngineGoes);
            Assert.False(viewport.IsRunning);
        });
    }

    static bool SmallFrame(out int width, out int height, ref byte[] rgba)
    {
        width = 100;
        height = 50;
        rgba = new byte[width * height * 4];
        return true;
    }

    // A viewport with a frame in the middle column, and the point at its center in window coordinates.
    static (EditorWindow Window, Point Center) OpenWithViewport(FakeScene scene)
    {
        var window = Open(scene);
        var viewport = new ViewportControl(() => false, SmallFrame);
        window.ShowViewport(viewport);
        viewport.Tick();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var middle = new Point(viewport.Bounds.Width / 2, viewport.Bounds.Height / 2);
        return (window, viewport.TranslatePoint(middle, window)!.Value);
    }

    static void ClickAt(EditorWindow window, Point point)
    {
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    [Fact]
    public void Clicking_the_viewport_selects_the_entity_the_scene_picks()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { PickResult = FakeScene.Lamp.Handle };
            var (window, center) = OpenWithViewport(scene);

            ClickAt(window, center);

            Assert.Same(FakeScene.Lamp, window.Outline.SelectedItem);
            Assert.Equal(["1", "2", "3"], window.Inspector.Position.Axes.Select(box => box.Text));
            Assert.Equal(50, scene.PickedAt!.Value.X, 1f);
            Assert.Equal(25, scene.PickedAt!.Value.Y, 1f);
        });
    }

    [Fact]
    public void Clicking_empty_space_in_the_viewport_clears_the_selection()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var (window, center) = OpenWithViewport(scene);
            window.Outline.SelectedItem = FakeScene.Lamp;

            ClickAt(window, center);

            Assert.Null(window.Outline.SelectedItem);
            Assert.Equal(["", "", ""], window.Inspector.Position.Axes.Select(box => box.Text));
        });
    }

    [Fact]
    public void Loading_a_scene_lists_the_scenes_of_the_project()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());

            Assert.Equal(["res://levels/second.oscn", "res://field.oscn"], FileNodes(window).Where(node => node.Kind == FileKind.Scene).Select(node => node.Path));
            Assert.Equal(FileTree.Root, ((FileNode)window.Scenes.Tree.SelectedItem!).Path);
        });
    }

    [Fact]
    public void Choosing_a_scene_saves_the_edits_and_shows_the_new_scene()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = Open(scene);
            MakeAnEdit(window);

            OpenScene(window, "res://levels/second.oscn");

            Assert.Equal(1, scene.Saves);
            Assert.Equal(["res://levels/second.oscn"], scene.Opened);
            Assert.Equal("second", Root(window).Name);
            Assert.Equal("Opened second.", window.Status);
        });
    }

    [Fact]
    public void A_scene_that_cannot_save_stays_open()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { SaveFails = true };
            var window = Open(scene);
            MakeAnEdit(window);
            OpenScene(window, "res://field.oscn");

            Assert.Empty(scene.Opened);
            Assert.Equal("Field", Root(window).Name);
            Assert.Equal("The engine could not save the scene. Error code 7.", window.Status);
        });
    }

    [Fact]
    public void A_scene_that_cannot_open_says_why_and_keeps_the_open_one()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene { OpenFails = true });
            OpenScene(window, "res://field.oscn");

            Assert.Equal("The engine could not open res://field.oscn. Error code 7.", window.Status);
            Assert.Equal("Field", Root(window).Name);
        });
    }

    [Fact]
    public void With_no_scene_open_a_chosen_scene_opens_without_a_save()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { StartsEmpty = true };
            var window = Open(scene);
            Assert.Equal("No scene is open.", window.Status);
            OpenScene(window, "res://field.oscn");

            Assert.Equal(0, scene.Saves);
            Assert.Equal("field", Root(window).Name);
        });
    }

    [Fact]
    public void Move_buttons_follow_the_place_among_siblings()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());
            Assert.False(window.Move.UpButton.IsEnabled);
            Assert.False(window.Move.DownButton.IsEnabled);

            window.Outline.SelectedItem = FakeScene.Lamp;
            Assert.False(window.Move.UpButton.IsEnabled);
            Assert.True(window.Move.DownButton.IsEnabled);

            window.Outline.SelectedItem = FakeScene.Sound;
            Assert.True(window.Move.UpButton.IsEnabled);
            Assert.False(window.Move.DownButton.IsEnabled);

            window.Outline.SelectedItem = Root(window);
            Assert.False(window.Move.UpButton.IsEnabled);
            Assert.False(window.Move.DownButton.IsEnabled);
            Assert.Empty(window.Move.ParentBox.ItemsSource!.Cast<string>());
        });
    }

    [Fact]
    public void Down_moves_the_entity_after_its_sibling_and_keeps_it_selected()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());
            window.Outline.SelectedItem = FakeScene.Lamp;

            Click(window.Move.DownButton);

            Assert.Equal(["Sound", "Lamp"], Root(window).Children.Select(c => c.Name));
            Assert.Equal("Lamp", ((OutlineNode)window.Outline.SelectedItem!).Name);
            Assert.Equal("Moved Lamp down.", window.Status);
            Assert.True(window.Move.UpButton.IsEnabled);
            Assert.False(window.Move.DownButton.IsEnabled);
        });
    }

    [Fact]
    public void Up_moves_the_entity_before_its_sibling()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());
            window.Outline.SelectedItem = FakeScene.Sound;

            Click(window.Move.UpButton);

            Assert.Equal(["Sound", "Lamp"], Root(window).Children.Select(c => c.Name));
            Assert.Equal("Moved Sound up.", window.Status);
        });
    }

    [Fact]
    public void Parent_choices_leave_out_the_entity_its_children_and_its_parent()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());
            window.Outline.SelectedItem = FakeScene.Sound;
            window.Tools.NameBox.Text = "Deep";
            Click(window.Tools.AddButton);
            Assert.Equal("Deep", ((OutlineNode)window.Outline.SelectedItem!).Name);

            window.Outline.SelectedItem = Root(window).Find(FakeScene.Sound.Handle);
            Assert.Equal(["Field/Lamp"], window.Move.ParentBox.ItemsSource!.Cast<string>());

            window.Outline.SelectedItem = Root(window).Find(FakeScene.Lamp.Handle);
            Assert.Equal(["Field/Sound", "Field/Sound/Deep"], window.Move.ParentBox.ItemsSource!.Cast<string>());
        });
    }

    [Fact]
    public void Move_under_waits_for_a_chosen_parent()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());
            window.Outline.SelectedItem = FakeScene.Lamp;
            Assert.False(window.Move.MoveUnderButton.IsEnabled);

            window.Move.ParentBox.SelectedIndex = 0;
            Assert.True(window.Move.MoveUnderButton.IsEnabled);

            window.Move.ParentBox.SelectedIndex = -1;
            Assert.False(window.Move.MoveUnderButton.IsEnabled);
        });
    }

    [Fact]
    public void Move_under_puts_the_entity_last_under_the_chosen_parent()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());
            window.Outline.SelectedItem = FakeScene.Lamp;
            window.Move.ParentBox.SelectedIndex = 0;

            Click(window.Move.MoveUnderButton);

            Assert.Equal(["Sound"], Root(window).Children.Select(c => c.Name));
            Assert.Equal(["Lamp"], Root(window).Children.Single().Children.Select(c => c.Name));
            Assert.Equal("Lamp", ((OutlineNode)window.Outline.SelectedItem!).Name);
            Assert.Equal("Moved Lamp under Field/Sound.", window.Status);
        });
    }

    [Fact]
    public void Move_under_uses_the_parent_that_was_chosen()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());
            window.Outline.SelectedItem = FakeScene.Sound;
            window.Tools.NameBox.Text = "Deep";
            Click(window.Tools.AddButton);
            window.Outline.SelectedItem = Root(window).Find(FakeScene.Lamp.Handle);
            window.Move.ParentBox.SelectedIndex = 1;

            Click(window.Move.MoveUnderButton);

            var deep = Root(window).Find(FakeScene.Sound.Handle)!.Children.Single();
            Assert.Equal(["Lamp"], deep.Children.Select(c => c.Name));
            Assert.Equal("Moved Lamp under Field/Sound/Deep.", window.Status);
        });
    }

    [Fact]
    public void A_parent_with_a_child_of_that_name_is_refused_and_nothing_moves()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());
            window.Outline.SelectedItem = FakeScene.Sound;
            window.Tools.NameBox.Text = "Lamp";
            Click(window.Tools.AddButton);
            window.Outline.SelectedItem = Root(window).Find(FakeScene.Lamp.Handle);
            window.Move.ParentBox.SelectedIndex = 0;

            Click(window.Move.MoveUnderButton);

            Assert.Equal("Sound already has a child named Lamp.", window.Status);
            Assert.Equal(["Lamp", "Sound"], Root(window).Children.Select(c => c.Name));
        });
    }

    [Fact]
    public void The_command_key_with_S_saves_and_the_key_is_handled()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = Open(scene);

            var press = Press(window, Key.S, window.CommandKey);

            Assert.Equal(1, scene.Saves);
            Assert.True(press.Handled);
            Assert.Equal("Saved.", window.Status);
        });
    }

    [Fact]
    public void S_saves_only_with_the_command_key_and_nothing_else()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = Open(scene);

            Assert.False(Press(window, Key.S).Handled);
            Assert.False(Press(window, Key.S, window.CommandKey | KeyModifiers.Shift).Handled);

            Assert.Equal(0, scene.Saves);
        });
    }

    [Fact]
    public void The_save_shortcut_waits_for_an_open_scene()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { StartsEmpty = true };
            var window = Open(scene);

            var press = Press(window, Key.S, window.CommandKey);

            Assert.Equal(0, scene.Saves);
            Assert.False(press.Handled);
        });
    }

    [Fact]
    public void The_command_key_with_D_duplicates_the_selected_entity()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());
            window.Outline.SelectedItem = FakeScene.Lamp;

            Assert.True(Press(window, Key.D, window.CommandKey).Handled);

            Assert.Equal(["Lamp", "Lamp2", "Sound"], Root(window).Children.Select(c => c.Name));
            Assert.Equal("Duplicated Lamp.", window.Status);
        });
    }

    [Fact]
    public void D_duplicates_only_with_the_command_key_and_nothing_else()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());
            window.Outline.SelectedItem = FakeScene.Lamp;

            Assert.False(Press(window, Key.D).Handled);
            Assert.False(Press(window, Key.D, window.CommandKey | KeyModifiers.Shift).Handled);

            Assert.Equal(["Lamp", "Sound"], Root(window).Children.Select(c => c.Name));
        });
    }

    [Fact]
    public void A_key_that_a_box_already_handled_is_left_alone()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = Open(scene);
            var press = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.S, KeyModifiers = window.CommandKey, Handled = true };

            window.RaiseEvent(press);

            Assert.Equal(0, scene.Saves);
        });
    }

    [Fact]
    public void The_duplicate_shortcut_leaves_the_scene_root_alone()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());
            window.Outline.SelectedItem = Root(window);
            var status = window.Status;

            Assert.False(Press(window, Key.D, window.CommandKey).Handled);

            Assert.Equal(["Lamp", "Sound"], Root(window).Children.Select(c => c.Name));
            Assert.Equal(status, window.Status);
        });
    }

    [Fact]
    public void F_frames_the_selected_entity()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = Open(scene);
            window.Outline.SelectedItem = FakeScene.Lamp;

            Assert.True(Press(window, Key.F).Handled);

            Assert.Equal([FakeScene.Lamp.Handle], scene.Framed);
            Assert.Equal("Framed Lamp.", window.Status);
        });
    }

    [Fact]
    public void F_frames_nothing_with_no_selection_or_with_a_modifier()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = Open(scene);

            Assert.False(Press(window, Key.F).Handled);
            window.Outline.SelectedItem = FakeScene.Lamp;
            Assert.False(Press(window, Key.F, window.CommandKey).Handled);
            Assert.False(Press(window, Key.F, KeyModifiers.Shift).Handled);

            Assert.Empty(scene.Framed);
        });
    }

    [Fact]
    public void F_typed_into_a_box_stays_with_the_box()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = Open(scene);
            window.Outline.SelectedItem = FakeScene.Lamp;

            // The Scene filter is in the window. The name and class boxes sit in a closed flyout, so a key raised on them never reaches the window.
            Assert.False(Press(window.ScenePane.FilterBox, Key.F).Handled);

            Assert.Empty(scene.Framed);
        });
    }

    [Fact]
    public void Q_lights_select_mode_W_move_mode_E_rotate_mode_and_R_scale_mode()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());

            Assert.True(Press(window, Key.Q).Handled);
            Assert.True(window.Toolbar.SelectButton.IsChecked);
            Assert.False(window.Toolbar.TransformButton.IsChecked);

            Assert.True(Press(window, Key.W).Handled);
            Assert.True(window.Toolbar.MoveButton.IsChecked);
            Assert.False(window.Toolbar.SelectButton.IsChecked);

            Assert.True(Press(window, Key.E).Handled);
            Assert.True(window.Toolbar.RotateButton.IsChecked);
            Assert.False(window.Toolbar.MoveButton.IsChecked);

            Assert.True(Press(window, Key.R).Handled);
            Assert.True(window.Toolbar.ScaleButton.IsChecked);
            Assert.False(window.Toolbar.RotateButton.IsChecked);
        });
    }

    [Fact]
    public void Q_W_E_and_R_with_a_modifier_or_typed_into_a_box_change_no_mode()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());
            window.Outline.SelectedItem = FakeScene.Lamp;

            Assert.False(Press(window, Key.Q, window.CommandKey).Handled);
            Assert.False(Press(window, Key.W, KeyModifiers.Shift).Handled);
            Assert.False(Press(window.ScenePane.FilterBox, Key.Q).Handled);
            Assert.False(Press(window.ScenePane.FilterBox, Key.W).Handled);
            Assert.False(Press(window, Key.E, window.CommandKey).Handled);
            Assert.False(Press(window.ScenePane.FilterBox, Key.E).Handled);
            Assert.False(Press(window, Key.R, KeyModifiers.Shift).Handled);
            Assert.False(Press(window.ScenePane.FilterBox, Key.R).Handled);

            Assert.True(window.Toolbar.TransformButton.IsChecked);
            Assert.False(window.Toolbar.SelectButton.IsChecked);
            Assert.False(window.Toolbar.MoveButton.IsChecked);
            Assert.False(window.Toolbar.RotateButton.IsChecked);
            Assert.False(window.Toolbar.ScaleButton.IsChecked);
        });
    }

    [Fact]
    public void T_flips_local_coordinates_unless_a_box_or_a_modifier_has_it()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());

            Assert.False(Press(window, Key.T, window.CommandKey).Handled);
            Assert.False(Press(window.ScenePane.FilterBox, Key.T).Handled);
            Assert.False(window.Toolbar.LocalButton.IsChecked);

            Assert.True(Press(window, Key.T).Handled);
            Assert.True(window.Toolbar.LocalButton.IsChecked);
            Assert.True(Press(window, Key.T).Handled);
            Assert.False(window.Toolbar.LocalButton.IsChecked);
        });
    }

    [Fact]
    public void The_title_is_the_scene_file_then_the_project()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene { SceneFile = "res://levels/field.oscn", ProjectName = "Demo" });
            Assert.Equal("field.oscn - Demo", window.Title);
            Assert.Equal("field", window.SceneTabs.Label.Text);

            MakeAnEdit(window);
            Assert.Equal("(*) field.oscn - Demo", window.Title);
        });
    }

    [Fact]
    public void A_scene_with_no_file_leaves_the_project_name_alone_in_the_title()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene { ProjectName = "Demo" });

            Assert.Equal("Demo", window.Title);
        });
    }

    [Fact]
    public void Opening_a_scene_puts_its_file_in_the_title_and_on_the_tab()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene { ProjectName = "Demo" });

            OpenScene(window, "res://levels/second.oscn");

            Assert.Equal("second.oscn - Demo", window.Title);
            Assert.Equal("second", window.SceneTabs.Label.Text);
        });
    }

    [Fact]
    public void An_edit_puts_a_star_in_the_title_and_a_save_takes_it_out()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());
            Assert.Equal("Unnamed Project", window.Title);

            MakeAnEdit(window);
            Assert.Equal("(*) Unnamed Project", window.Title);

            Click(window.SaveButton);
            Assert.Equal("Unnamed Project", window.Title);
        });
    }

    [Fact]
    public void An_inspector_edit_puts_a_star_in_the_title()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());
            window.Outline.SelectedItem = FakeScene.Lamp;
            window.Inspector.Position.Axes[0].Text = "7";

            Commit(window.Inspector.Position.Axes[0]);

            Assert.Equal("(*) Unnamed Project", window.Title);
        });
    }

    [Fact]
    public void A_refused_edit_leaves_the_title_alone()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());
            window.Outline.SelectedItem = FakeScene.Lamp;
            window.Tools.NameBox.Text = " ";

            Click(window.Tools.RenameButton);

            Assert.Equal("Unnamed Project", window.Title);
        });
    }

    [Fact]
    public void A_failed_save_keeps_the_star()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene { SaveFails = true });
            MakeAnEdit(window);

            Click(window.SaveButton);

            Assert.Equal("(*) Unnamed Project", window.Title);
        });
    }

    [Fact]
    public void Play_and_choosing_a_scene_leave_a_scene_with_no_edits_unsaved()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = Open(scene);

            Click(window.PlayButton);
            OpenScene(window, "res://levels/second.oscn");

            Assert.Equal(1, scene.Plays);
            Assert.Equal(0, scene.Saves);
            Assert.Equal(["res://levels/second.oscn"], scene.Opened);
        });
    }

    [Fact]
    public void A_play_saves_the_edits_and_clears_the_star()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());
            MakeAnEdit(window);

            Click(window.PlayButton);

            Assert.Equal("Unnamed Project", window.Title);
        });
    }

    [Fact]
    public void Opening_another_scene_starts_without_a_star()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());
            MakeAnEdit(window);
            OpenScene(window, "res://levels/second.oscn");

            Assert.Equal("second.oscn - Unnamed Project", window.Title);
        });
    }

    [Fact]
    public void A_scene_that_cannot_open_keeps_the_saved_state_and_no_star()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { OpenFails = true };
            var window = Open(scene);
            MakeAnEdit(window);
            OpenScene(window, "res://levels/second.oscn");

            Assert.Equal(1, scene.Saves);
            Assert.Equal("Unnamed Project", window.Title);
        });
    }

    [Fact]
    public void Closing_with_edits_keeps_the_window_open_and_asks()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());
            MakeAnEdit(window);

            window.Close();

            Assert.True(window.IsVisible);
            Assert.True(window.CloseBar.IsVisible);
        });
    }

    [Fact]
    public void Closing_with_no_edits_just_closes()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = Open(scene);

            window.Close();

            Assert.False(window.IsVisible);
            Assert.False(window.CloseBar.IsVisible);
            Assert.Equal(0, scene.Saves);
        });
    }

    [Fact]
    public void Save_and_close_saves_the_edits_and_closes()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = Open(scene);
            MakeAnEdit(window);
            window.Close();

            Click(window.CloseBar.SaveAndCloseButton);

            Assert.Equal(1, scene.Saves);
            Assert.False(window.IsVisible);
        });
    }

    [Fact]
    public void A_failed_save_keeps_the_window_open_and_says_why()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene { SaveFails = true });
            MakeAnEdit(window);
            window.Close();

            Click(window.CloseBar.SaveAndCloseButton);

            Assert.True(window.IsVisible);
            Assert.Contains("Error code 7", window.Status);
        });
    }

    [Fact]
    public void Close_without_saving_closes_and_writes_nothing()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = Open(scene);
            MakeAnEdit(window);
            window.Close();

            Click(window.CloseBar.CloseWithoutButton);

            Assert.Equal(0, scene.Saves);
            Assert.False(window.IsVisible);
        });
    }

    [Fact]
    public void Keep_editing_hides_the_bar_and_keeps_the_edits()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());
            MakeAnEdit(window);
            window.Close();

            Click(window.CloseBar.KeepEditingButton);

            Assert.True(window.IsVisible);
            Assert.False(window.CloseBar.IsVisible);
            Assert.Equal("(*) Unnamed Project", window.Title);
        });
    }

    [Fact]
    public void A_save_while_the_bar_is_up_hides_it()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());
            MakeAnEdit(window);
            window.Close();

            Click(window.SaveButton);

            Assert.True(window.IsVisible);
            Assert.False(window.CloseBar.IsVisible);
        });
    }

    [Fact]
    public void The_bar_stays_hidden_until_a_close_is_asked_for()
    {
        UiThread.Run(() =>
        {
            var window = Open(new FakeScene());

            MakeAnEdit(window);

            Assert.False(window.CloseBar.IsVisible);
        });
    }
}

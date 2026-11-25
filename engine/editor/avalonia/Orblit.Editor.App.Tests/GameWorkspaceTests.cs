using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using Orblit.Editor.App.Tests.Graph;
using Orblit.Editor.Bridge;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class GameWorkspaceTests
{
    const string Idle = "Play settings on the left apply the next time you press Play.";
    const string Running = "Changes to the scene now count from the next Play.";

    static readonly DebugMonitors s_frame = new(Fps: 60, Process: 0.004, Physics: 0.001, DrawCalls: 12, Objects: 340, VideoMemory: 50 * 1024 * 1024);

    static readonly LiveEntity[] s_tree = [new("Level", "Entity3D", 0), new("Props", "Entity3D", 1), new("Crate", "MeshInstance3D", 2), new("Sun", "DirectionalLight3D", 1)];

    static string[] Words(Control control) => [.. control.GetLogicalDescendants().OfType<TextBlock>().Select(text => text.Text!)];

    static void ShowGame(EditorWindow window) => ClickOn(window.Bar.Tabs.Single(tab => tab.Label == "Game"), window);

    // A game runs and has sent one frame of monitors and its tree.
    static (EditorWindow Window, FakeScene Scene) RunningWithLiveState()
    {
        var scene = new FakeScene();
        var window = OpenAtReferenceSize(scene);
        ShowGame(window);
        Click(window.Game.RunButton);
        scene.Channel.Next = FakeDebugChannel.Running() with { Monitors = s_frame, Scene = s_tree };
        window.Poll();
        window.UpdateLayout();
        return (window, scene);
    }

    [Fact]
    public void The_Game_tab_swaps_the_toolbar_and_viewport_for_the_game_view()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var before = window.Status;

            ShowGame(window);
            window.UpdateLayout();

            Assert.Equal(["Game"], window.Bar.Tabs.Where(tab => tab.IsChecked == true).Select(tab => tab.Label));
            Assert.True(window.Game.IsVisible);
            Assert.False(window.Center.Toolbar.IsVisible);
            Assert.False(window.Viewport.IsEffectivelyVisible);
            AssertBox(56, 46, 3328, 1183, BoxOf(window.Game, window));
            Assert.Equal(before, window.Status);
        });
    }

    [Fact]
    public void The_running_card_names_the_project_and_counts_the_time_on_the_clock()
    {
        UiThread.Run(() =>
        {
            var time = new ManualTime();
            var window = OpenAtReferenceSize(new FakeScene { ProjectName = "view_demo" }, time: time);
            ShowGame(window);
            Assert.Equal(("Stopped", ""), (window.Game.Status.Text, window.Game.Time.Text));
            Assert.True(window.Game.RunButton.IsVisible);

            Click(window.Game.RunButton);
            time.Advance(TimeSpan.FromSeconds(42));
            window.Poll();

            Assert.Equal(("Playing", "view_demo", "00:42"), (window.Game.Status.Text, window.Game.Project.Text, window.Game.Time.Text));
            Assert.False(window.Game.RunButton.IsVisible);
            Assert.True(window.Game.StopButton.IsVisible);

            time.Advance(TimeSpan.FromHours(1));
            window.Poll();

            Assert.Equal("1:00:42", window.Game.Time.Text);

            Click(window.Game.StopButton);

            Assert.Equal(("Stopped", ""), (window.Game.Status.Text, window.Game.Time.Text));
        });
    }

    [Fact]
    public void The_play_settings_show_each_flag_by_its_plain_name_and_each_takes_input()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            ShowGame(window);
            window.UpdateLayout();

            Assert.All(GameView.FlagNames, name => Assert.True(window.Game.SwitchOf(name).IsEffectivelyEnabled));
            Assert.Equal(GameView.FlagNames.Count(), window.Game.GetVisualDescendants().OfType<CheckBox>().Count());
            var collisions = window.Game.SwitchOf("Visible Collision Shapes");
            Assert.Equal("Collision shapes", Words(collisions)[0]);
            Assert.Equal("Visible Collision Shapes", Assert.IsType<HoverCard>(ToolTip.GetTip(collisions)).Engine);
        });
    }

    [Fact]
    public void Before_the_game_sends_a_frame_the_monitors_show_dashes_and_the_scene_is_empty()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(new FakeScene());
            ShowGame(window);
            Click(window.Game.RunButton);

            Assert.All(window.Game.MonitorValues, value => Assert.Equal("—", value));
            Assert.Empty(window.Game.LiveRows);
            Assert.True(window.Game.NoScene.IsVisible);
        });
    }

    [Fact]
    public void A_frame_from_the_game_fills_each_monitor_in_its_unit()
    {
        UiThread.Run(() =>
        {
            var (window, _) = RunningWithLiveState();

            Assert.Equal(["Frames a second", "Time per frame", "Game code", "Physics", "Draw calls", "Things", "Video memory"], GameView.MonitorNames);
            Assert.Equal(["60", "16.7 ms", "4.0 ms", "1.0 ms", "12", "340", "50 MB"], window.Game.MonitorValues);
            Assert.False(window.Game.SpeedHint.IsVisible);
        });
    }

    [Fact]
    public void A_game_at_no_frames_a_second_shows_no_frame_time()
    {
        UiThread.Run(() =>
        {
            var (window, scene) = RunningWithLiveState();
            scene.Channel.Next = scene.Channel.State with { Monitors = s_frame with { Fps = 0 } };

            window.Poll();

            Assert.Equal("—", window.Game.MonitorValues.ElementAt(1));
        });
    }

    [Fact]
    public void The_live_scene_lists_each_entity_of_the_game_indented_by_its_depth()
    {
        UiThread.Run(() =>
        {
            var (window, _) = RunningWithLiveState();

            var rows = window.Game.LiveRows.ToList();
            Assert.Equal(["Level", "Props", "Crate", "Sun"], rows.Select(row => Words(row)[0]));
            Assert.Equal([6.0, 22, 38, 22], rows.Select(row => row.Margin.Left));
            var card = Assert.IsType<HoverCard>(ToolTip.GetTip(rows[2]));
            Assert.Equal((PlainNames.NameOf("MeshInstance3D"), "MeshInstance3D"), (Words(rows[2])[1], card.Engine));
            Assert.False(window.Game.NoScene.IsVisible);
            Assert.True(window.Game.LiveMark.IsVisible);
        });
    }

    [Fact]
    public void A_poll_that_brings_the_same_tree_keeps_the_rows()
    {
        UiThread.Run(() =>
        {
            var (window, scene) = RunningWithLiveState();
            var first = window.Game.LiveRows.First();
            scene.Channel.Next = scene.Channel.State with { Monitors = s_frame with { Fps = 59 }, Scene = [.. s_tree] };

            window.Poll();

            Assert.Equal("59", window.Game.MonitorValues.First());
            Assert.Same(first, window.Game.LiveRows.First());
        });
    }

    [Fact]
    public void A_game_that_stops_takes_its_monitors_and_its_scene_with_it()
    {
        UiThread.Run(() =>
        {
            var (window, _) = RunningWithLiveState();

            Click(window.Game.StopButton);

            Assert.Equal("—", window.Game.MonitorValues.First());
            Assert.Empty(window.Game.LiveRows);
            Assert.True(window.Game.NoScene.IsVisible);
            Assert.False(window.Game.LiveMark.IsVisible);
            Assert.True(window.Game.SpeedHint.IsVisible);
        });
    }

    [Fact]
    public void The_Show_while_playing_boxes_that_are_on_go_to_the_game_as_engine_flags()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            ShowGame(window);

            window.Game.SwitchOf("Visible Navigation").IsChecked = true;
            window.Game.SwitchOf("Visible Collision Shapes").IsChecked = true;
            Click(window.Game.RunButton);

            Assert.Equal(["--debug-collisions", "--debug-navigation"], scene.PlayedFlags);
        });
    }

    [Fact]
    public void An_open_project_offers_Play_and_no_Stop()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.True(window.Game.RunButton.IsEnabled);
            Assert.False(window.Game.StopButton.IsEnabled);
            Assert.False(window.StopButton.IsEnabled);
            Assert.Equal(Idle, window.Game.State.Text);
        });
    }

    [Fact]
    public void Before_a_project_is_loaded_there_is_nothing_to_run()
    {
        UiThread.Run(() =>
        {
            var window = new EditorWindow();

            Assert.False(window.Game.RunButton.IsEnabled);
            Assert.False(window.Game.StopButton.IsEnabled);
            Assert.False(window.StopButton.IsEnabled);
            Assert.Equal(Idle, window.Game.State.Text);
        });
    }

    [Fact]
    public void Play_in_the_game_view_starts_the_game_and_turns_both_Stops_on()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            ShowGame(window);

            Click(window.Game.RunButton);

            Assert.Equal(1, scene.Plays);
            Assert.Equal(Running, window.Game.State.Text);
            Assert.True(window.Game.StopButton.IsEnabled);
            Assert.True(window.StopButton.IsEnabled);
            Assert.Equal("Playing in a second window.", window.Status);
        });
    }

    [Fact]
    public void The_Play_button_shows_in_the_game_view_too()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);

            Click(window.PlayButton);

            Assert.Equal(Running, window.Game.State.Text);
            Assert.True(window.Game.StopButton.IsEnabled);
        });
    }

    [Fact]
    public void Play_in_the_game_view_saves_the_edits_first()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            MakeAnEdit(window);

            Click(window.Game.RunButton);

            Assert.Equal(1, scene.Saves);
            Assert.Equal(1, scene.Plays);
        });
    }

    [Fact]
    public void Stop_in_the_game_view_ends_the_game()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            Click(window.Game.RunButton);

            Click(window.Game.StopButton);

            Assert.Equal(1, scene.Stops);
            Assert.Equal(Idle, window.Game.State.Text);
            Assert.False(window.Game.StopButton.IsEnabled);
            Assert.False(window.StopButton.IsEnabled);
            Assert.Equal("Stopped the game.", window.Status);
        });
    }

    [Fact]
    public void The_title_bar_Stop_ends_the_game()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            Click(window.PlayButton);

            Click(window.StopButton);

            Assert.Equal(1, scene.Stops);
            Assert.Equal(Idle, window.Game.State.Text);
            Assert.False(window.StopButton.IsEnabled);
        });
    }

    [Fact]
    public void A_game_that_ends_on_its_own_shows_as_stopped_at_the_next_poll()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            Click(window.PlayButton);

            scene.IsPlaying = false;
            window.Poll();

            Assert.Equal(Idle, window.Game.State.Text);
            Assert.False(window.Game.StopButton.IsEnabled);
            Assert.False(window.StopButton.IsEnabled);
        });
    }

    [Fact]
    public void A_poll_while_the_game_runs_leaves_the_view_alone()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            Click(window.PlayButton);

            window.Poll();

            Assert.Equal(Running, window.Game.State.Text);
            Assert.True(window.StopButton.IsEnabled);
        });
    }

    [Fact]
    public void A_failed_save_keeps_Play_from_starting_a_game()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { SaveFails = true };
            var window = OpenAtReferenceSize(scene);
            MakeAnEdit(window);

            Click(window.Game.RunButton);

            Assert.Equal(0, scene.Plays);
            Assert.Equal(Idle, window.Game.State.Text);
        });
    }

    [Fact]
    public void What_Play_runs_set_to_This_scene_plays_the_open_scene()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { SceneFile = "res://levels/one.oscn" };
            var window = OpenAtReferenceSize(scene);
            ShowGame(window);

            window.Game.ThisSceneChoice.IsChecked = true;
            Click(window.Game.RunButton);

            Assert.False(window.Game.MainChoice.IsChecked);
            Assert.Equal(["--scene", "res://levels/one.oscn"], scene.PlayedFlags.TakeLast(2));
        });
    }

    [Fact]
    public void The_title_bar_Play_follows_What_Play_runs()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { SceneFile = "res://levels/one.oscn" };
            var window = OpenAtReferenceSize(scene);
            window.Game.ThisSceneChoice.IsChecked = true;

            Click(window.PlayButton);

            Assert.Equal(["--scene", "res://levels/one.oscn"], scene.PlayedFlags.TakeLast(2));
        });
    }

    [Fact]
    public void Main_scene_plays_with_no_scene_argument()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { SceneFile = "res://levels/one.oscn" };
            var window = OpenAtReferenceSize(scene);

            Click(window.Game.RunButton);

            Assert.True(window.Game.MainChoice.IsChecked);
            Assert.DoesNotContain("--scene", scene.PlayedFlags);
        });
    }

    [Fact]
    public void Without_a_saved_scene_This_scene_is_off_and_says_why()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(new FakeScene());

            Assert.False(window.Game.ThisSceneChoice.IsEnabled);
            Assert.False(window.Game.PlaySceneButton.IsEnabled);
            Assert.Equal("Open a saved scene first.", Assert.IsType<HoverCard>(ToolTip.GetTip(window.Game.ThisSceneChoice)).Line);
        });
    }

    [Fact]
    public void Play_this_scene_in_the_stopped_view_plays_the_open_scene()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { SceneFile = "res://levels/one.oscn" };
            var window = OpenAtReferenceSize(scene);
            ShowGame(window);

            Click(window.Game.PlaySceneButton);

            Assert.Equal(["--scene", "res://levels/one.oscn"], scene.PlayedFlags.TakeLast(2));
        });
    }

    [Fact]
    public void While_the_game_plays_the_settings_wait_and_say_so()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            ShowGame(window);
            Assert.True(window.Game.Settings.IsEnabled);
            Assert.False(window.Game.WaitsNote.IsVisible);
            Assert.True(window.Game.Stopped.IsVisible);

            Click(window.Game.RunButton);

            Assert.False(window.Game.Settings.IsEnabled);
            Assert.True(window.Game.WaitsNote.IsVisible);
            Assert.Equal((false, true), (window.Game.Stopped.IsVisible, window.Game.Playing.IsVisible));
        });
    }

    [Fact]
    public void A_slow_game_turns_its_readings_amber_or_red_and_says_where_to_look()
    {
        UiThread.Run(() =>
        {
            var (window, scene) = RunningWithLiveState();
            scene.Channel.Next = scene.Channel.State with { Monitors = s_frame with { Fps = 22, Process = 0.0418 } };

            window.Poll();

            var rate = window.Game.ReadingOf("Frames a second");
            var frame = window.Game.ReadingOf("Time per frame");
            var code = window.Game.ReadingOf("Game code");
            Assert.Equal((EditorBrushes.FaultText, "Under 30 feels slow. Time per frame shows why."), (rate.Value.Foreground, rate.Line.Text));
            Assert.Equal((EditorBrushes.Warn, "Over 16.7 ms. Game code takes most of it."), (frame.Value.Foreground, frame.Line.Text));
            Assert.Equal((EditorBrushes.Warn, "Look at what runs Every frame."), (code.Value.Foreground, code.Line.Text));
            Assert.Equal("Moving and bumping things.", window.Game.ReadingOf("Physics").Line.Text);
        });
    }

    [Fact]
    public void A_slow_frame_names_physics_when_physics_takes_most_of_it()
    {
        UiThread.Run(() =>
        {
            var (window, scene) = RunningWithLiveState();
            scene.Channel.Next = scene.Channel.State with { Monitors = s_frame with { Fps = 40, Physics = 0.02 } };

            window.Poll();

            Assert.Equal("Over 16.7 ms. Physics takes most of it.", window.Game.ReadingOf("Time per frame").Line.Text);
            Assert.Equal("What your scripts and graphs take each frame.", window.Game.ReadingOf("Game code").Line.Text);
            Assert.Equal(EditorBrushes.Warn, window.Game.ReadingOf("Physics").Value.Foreground);
            Assert.Equal("60 or more feels smooth.", window.Game.ReadingOf("Frames a second").Line.Text);
        });
    }

    [Fact]
    public void A_game_back_in_its_good_range_drops_the_tone_and_the_line()
    {
        UiThread.Run(() =>
        {
            var (window, scene) = RunningWithLiveState();
            var calm = window.Game.ReadingOf("Frames a second").Value.Foreground;
            scene.Channel.Next = scene.Channel.State with { Monitors = s_frame with { Fps = 22 } };
            window.Poll();

            scene.Channel.Next = scene.Channel.State with { Monitors = s_frame };
            window.Poll();

            var rate = window.Game.ReadingOf("Frames a second");
            Assert.Equal((calm, "60 or more feels smooth."), (rate.Value.Foreground, rate.Line.Text));
        });
    }

    [Fact]
    public void Copies_opens_that_many_games_and_one_is_the_default()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            Click(window.Game.RunButton);
            Assert.Equal(1, scene.PlayedCopies);
            Click(window.Game.StopButton);

            window.Game.CopiesChoice.SelectedIndex = 2;
            Click(window.Game.RunButton);

            Assert.Equal(3, scene.PlayedCopies);
            Assert.Equal("Run instances", Assert.IsType<HoverCard>(ToolTip.GetTip(window.Game.CopiesChoice)).Engine);
        });
    }
}

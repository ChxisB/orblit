using Avalonia.Controls;
using Avalonia.VisualTree;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class GameWorkspaceTests
{
    const string Idle = "No game is running. Run opens one in its own window.";
    const string Running = "The game is running in its own window.";

    static void ShowGame(EditorWindow window) => ClickOn(window.Bar.Tabs.Single(tab => tab.Label == "Game"), window);

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
            AssertBox(287, 74, 2866, 1157, BoxOf(window.Game, window));
            Assert.Equal(before, window.Status);
        });
    }

    [Fact]
    public void An_open_project_offers_Run_and_no_Stop()
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
    public void Run_in_the_game_view_starts_the_game_and_turns_both_Stops_on()
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
    public void Run_in_the_game_view_saves_the_edits_first()
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
    public void Pause_stays_off_while_a_game_runs()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(new FakeScene());
            Click(window.PlayButton);

            var pause = window.Bar.GetVisualDescendants().OfType<Button>().Single(button => ToolTip.GetTip(button) is "Pause the game");

            Assert.False(pause.IsEnabled);
        });
    }

    [Fact]
    public void A_failed_save_keeps_Run_from_starting_a_game()
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
}

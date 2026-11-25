using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Threading;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class ToastTests
{
    // The posted hide runs on the next pass of the UI thread, as it would after a real timer.
    static void Pass(ManualTime time, double seconds)
    {
        time.Advance(TimeSpan.FromSeconds(seconds));
        Dispatcher.UIThread.RunJobs();
    }

    const string First = "Saved first.oscn";
    const string Second = "Saved second.oscn";

    static void Say(EditorWindow window, string title) => window.Toasts.Show(ToastKind.Done, title);

    static Toast[] Cards(EditorWindow window) => [.. window.Toasts.Cards];

    static string[] Titles(EditorWindow window) => [.. window.Toasts.Cards.Select(card => card.Title)];

    [Fact]
    public void Closing_the_window_takes_the_toasts_down_and_drops_their_timers()
    {
        UiThread.Run(() =>
        {
            var time = new ManualTime();
            var window = OpenAtReferenceSize(new FakeScene(), time: time);
            Say(window, First);
            Say(window, Second);
            Assert.Equal(2, time.Armed);

            window.Close();

            Assert.Empty(Cards(window));
            Assert.Equal(0, time.Armed);
        });
    }

    [Fact]
    public void A_toast_goes_after_four_seconds()
    {
        UiThread.Run(() =>
        {
            var time = new ManualTime();
            var window = OpenAtReferenceSize(new FakeScene(), time: time);
            Say(window, First);

            Pass(time, 3.9);
            Assert.Single(Cards(window));
            Pass(time, 0.1);

            Assert.Empty(Cards(window));
        });
    }

    [Fact]
    public void Toasts_stack_newest_at_the_bottom_and_each_keeps_its_own_four_seconds()
    {
        UiThread.Run(() =>
        {
            var time = new ManualTime();
            var window = OpenAtReferenceSize(new FakeScene(), time: time);
            Say(window, First);
            Pass(time, 3);

            Say(window, Second);
            window.UpdateLayout();

            Assert.Equal([First, Second], Titles(window));
            var cards = Cards(window);
            Assert.Equal(BoxOf(cards[0], window).Bottom + 10, BoxOf(cards[1], window).Y);
            Pass(time, 1);
            Assert.Equal([Second], Titles(window));
            Pass(time, 3);
            Assert.Empty(Cards(window));
        });
    }

    [Fact]
    public void The_same_message_again_moves_its_toast_to_the_bottom_and_starts_its_four_seconds_again()
    {
        UiThread.Run(() =>
        {
            var time = new ManualTime();
            var window = OpenAtReferenceSize(new FakeScene(), time: time);
            Say(window, First);
            Say(window, Second);
            Pass(time, 3);

            Say(window, First);
            Pass(time, 3);

            Assert.Equal([First], Titles(window));
            Assert.Equal(1, time.Armed);
        });
    }

    [Fact]
    public void Dismiss_takes_the_toast_down_at_once_and_drops_its_timer()
    {
        UiThread.Run(() =>
        {
            var time = new ManualTime();
            var window = OpenAtReferenceSize(new FakeScene(), time: time);
            Say(window, First);
            var dismiss = Cards(window)[0].DismissButton;

            Click(dismiss);

            Assert.Equal("Dismiss", ToolTip.GetTip(dismiss));
            Assert.Empty(Cards(window));
            Assert.Equal(0, time.Armed);
        });
    }

    [Fact]
    public void A_toast_with_an_action_stays_until_the_action_or_dismiss()
    {
        UiThread.Run(() =>
        {
            var time = new ManualTime();
            var stack = new ToastStack(time);
            var runs = 0;
            stack.Show(ToastKind.Failed, "The game could not start", "No main scene is set.", new ToastAction("Use main.oscn", () => runs++));

            Pass(time, 60);
            var card = Assert.Single(stack.Cards);
            Assert.Equal("No main scene is set.", card.Line.Text);
            Assert.Equal("Use main.oscn", card.ActionButton!.Content);
            Click(card.ActionButton);

            Assert.Equal(1, runs);
            Assert.Empty(stack.Cards);
        });
    }

    [Fact]
    public void A_save_shows_a_green_tick_toast_that_names_the_scene_the_time_and_the_changes()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(new FakeScene { SceneFile = "res://main.oscn" }, time: new ManualTime());
            MakeAnEdit(window);

            Click(window.SaveButton);

            var card = Assert.Single(Cards(window));
            Assert.Equal("Saved main", card.Title);
            Assert.Equal("At 16:33. 1 change saved.", card.Line.Text);
            Assert.Equal("Tick", card.Icon.IconName);
            Assert.Same(EditorBrushes.Done, TextElement.GetForeground(card.Icon));
            Assert.Null(card.ActionButton);
        });
    }

    [Fact]
    public void A_failed_save_shows_why_in_red_and_Try_again_saves_once_more()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { SaveFails = true, SceneFile = "res://main.oscn" };
            var window = OpenAtReferenceSize(scene, time: new ManualTime());
            MakeAnEdit(window);
            Click(window.SaveButton);

            var card = Assert.Single(Cards(window));
            Assert.Equal("Couldn't save main", card.Title);
            Assert.Equal("The engine could not save the scene. Error code 7.", card.Line.Text);
            Assert.Equal("FaultCircle", card.Icon.IconName);
            Assert.Same(EditorBrushes.Fault, TextElement.GetForeground(card.Icon));
            scene.SaveFails = false;
            Click(card.ActionButton!);

            Assert.Equal(1, scene.Saves);
            Assert.Equal(["Saved main"], Titles(window));
        });
    }

    [Fact]
    public void Play_saves_an_edited_scene_and_says_so_in_a_toast()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { SceneFile = "res://main.oscn" };
            var window = OpenAtReferenceSize(scene, time: new ManualTime());
            MakeAnEdit(window);

            Click(window.PlayButton);

            Assert.Equal(1, scene.Saves);
            Assert.Equal(["Saved main"], Titles(window));
        });
    }

    [Fact]
    public void A_game_that_cannot_start_says_why_in_a_toast()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(new FakeScene { PlayError = new IOException("No main scene is set.") }, time: new ManualTime());

            Click(window.PlayButton);

            var card = Assert.Single(Cards(window));
            Assert.Equal("The game could not start", card.Title);
            Assert.Equal("No main scene is set.", card.Line.Text);
            Assert.Equal("No main scene is set.", window.Status);
        });
    }

    [Fact]
    public void The_stack_is_380_wide_16_in_from_the_right_and_10_clear_of_the_bottom_bar()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(new FakeScene(), time: new ManualTime());
            Say(window, First);
            window.UpdateLayout();

            var card = Cards(window)[0];
            var box = BoxOf(card, window);
            var bar = BoxOf(window.Center.BottomBar, window);
            Assert.Equal(3440 - 16, box.Right);
            Assert.Equal(380, box.Width);
            Assert.Equal(bar.Top - 10, box.Bottom);
            Assert.Same(EditorBrushes.Raised, card.Background);
            Assert.Same(EditorBrushes.LineStrong, card.BorderBrush);
            Assert.Equal(EditorTheme.IslandCorners, card.CornerRadius);
            Assert.Equal(new Thickness(12, 11), card.Padding);
            // The stack has no fill, so a click beside a card reaches the panel under it.
            Assert.Null(window.Toasts.Background);
        });
    }
}

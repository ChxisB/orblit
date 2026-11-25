using Orblit.Editor.App.Shell;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class NoSceneTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 20, 16, 33, 0, TimeSpan.Zero);

    static EditorWindow Closed(FakeScene scene, ManualTime time)
    {
        var window = OpenAtReferenceSize(scene, time);
        ClickOn(window.SceneTabs.CloseButton, window);
        return window;
    }

    [Fact]
    public void Closing_the_scene_shows_what_a_scene_is_and_the_empty_scene_and_inspector_panels_say_why()
    {
        UiThread.Run(() =>
        {
            var window = Closed(new FakeScene { SceneFile = "res://main.oscn" }, new ManualTime());

            Assert.True(window.Center.NoScene.IsVisible);
            Assert.Contains(NoScene.Title, Texts(window.Center.NoScene));
            Assert.Contains("A scene is a level, a menu or a part you reuse. Open one, or make a new one.", Texts(window.Center.NoScene));
            Assert.True(window.ScenePane.Empty.IsVisible);
            Assert.True(window.InspectorPane.Empty.IsVisible);
        });
    }

    [Fact]
    public void An_open_scene_hides_the_note_and_the_empty_panels()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(new FakeScene { SceneFile = "res://main.oscn" });

            Assert.False(window.Center.NoScene.IsVisible);
            Assert.False(window.ScenePane.Empty.IsVisible);
        });
    }

    [Fact]
    public void Open_again_lists_the_scenes_of_this_session_with_when_and_a_row_opens_its_scene()
    {
        UiThread.Run(() =>
        {
            var time = new ManualTime();
            var scene = new FakeScene { SceneFile = "res://field.oscn" };
            var window = OpenAtReferenceSize(scene, time);
            time.Advance(TimeSpan.FromMinutes(10));
            OpenScene(window, "res://levels/second.oscn");
            ClickOn(window.SceneTabs.CloseButton, window);

            var rows = window.Center.NoScene.Rows.ToArray();
            Assert.Equal(["opened just now", "second", "opened 10 minutes ago", "field"], rows.SelectMany(Texts));

            Click(rows[1]);

            Assert.Equal("res://field.oscn", scene.SceneFile);
            Assert.False(window.Center.NoScene.IsVisible);
        });
    }

    [Fact]
    public void New_scene_opens_the_new_scene_dialog()
    {
        UiThread.Run(() =>
        {
            var window = Closed(new FakeScene { SceneFile = "res://main.oscn" }, new ManualTime());

            Click(window.Center.NoScene.NewButton);

            Assert.True(window.NewScene.Dialog.IsVisible);
        });
    }

    [Fact]
    public void Find_a_scene_opens_search_on_the_scenes()
    {
        UiThread.Run(() =>
        {
            var window = Closed(new FakeScene { SceneFile = "res://main.oscn" }, new ManualTime());

            Click(window.Center.NoScene.FindButton);
            Assert.True(window.Search.Picker.IsVisible);
            Assert.True(window.Search.ScenesChip.IsVisible);
        });
    }

    [Theory]
    [InlineData(0.5, "opened just now")]
    [InlineData(1, "opened 1 minute ago")]
    [InlineData(59, "opened 59 minutes ago")]
    [InlineData(60, "opened 1 hour ago")]
    [InlineData(16 * 60, "opened 16 hours ago")]
    [InlineData(17 * 60, "opened yesterday")]
    [InlineData(3 * 24 * 60, "opened 17 November")]
    public void Opened_says_when_in_words(double minutesAgo, string words) =>
        Assert.Equal(words, NoScene.Opened(Now.AddMinutes(-minutesAgo), Now));
}

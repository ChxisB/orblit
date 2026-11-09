using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class ScriptWorkspaceTests
{
    const string Spin = "res://spin.cs";

    static EditorWindow Open(FakeScene scene)
    {
        var window = new EditorWindow();
        window.Show();
        window.Load(scene);
        window.UpdateLayout();
        return window;
    }

    static FakeScene SceneWithScripts()
    {
        var scene = new FakeScene();
        scene.ScriptText[Spin] = "class Spin { }";
        return scene;
    }

    static void ShowScript(EditorWindow window) => ClickOn(window.Bar.Tabs.Single(tab => tab.Label == "Script"), window);

    // Opens the Script tab and types into the first script, which is a change no file holds yet.
    static void EditAScript(EditorWindow window)
    {
        ShowScript(window);
        window.Script.List.SelectedItem = Spin;
        window.Script.Editor.Text = "class Spin { int turns; }";
    }

    [Fact]
    public void The_Script_tab_swaps_the_toolbar_and_viewport_for_the_script_view()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(SceneWithScripts());

            ShowScript(window);
            window.UpdateLayout();

            Assert.Equal(["Script"], window.Bar.Tabs.Where(tab => tab.IsChecked == true).Select(tab => tab.Label));
            Assert.True(window.Script.IsVisible);
            Assert.False(window.Center.Toolbar.IsVisible);
            Assert.False(window.Viewport.IsEffectivelyVisible);
            AssertBox(287, 74, 2866, 1157, BoxOf(window.Script, window));
            Assert.Equal(["res://spin.cs", "res://enemies/slime.cs"], (IEnumerable<string>)window.Script.List.ItemsSource!);
        });
    }

    [Fact]
    public void Save_writes_an_edited_script_with_the_scene()
    {
        UiThread.Run(() =>
        {
            var scene = SceneWithScripts();
            var window = Open(scene);
            EditAScript(window);

            Click(window.SaveButton);

            Assert.Equal([(Spin, "class Spin { int turns; }")], scene.ScriptsWritten);
            Assert.Equal(1, scene.Saves);
            Assert.False(window.Script.IsDirty);
        });
    }

    [Fact]
    public void Closing_with_an_edited_script_keeps_the_window_open_and_asks()
    {
        UiThread.Run(() =>
        {
            var window = Open(SceneWithScripts());
            EditAScript(window);

            window.Close();

            Assert.True(window.IsVisible);
            Assert.True(window.CloseBar.IsVisible);
        });
    }

    [Fact]
    public void Save_and_close_writes_only_the_script_when_the_scene_has_no_edits()
    {
        UiThread.Run(() =>
        {
            var scene = SceneWithScripts();
            var window = Open(scene);
            EditAScript(window);
            window.Close();

            Click(window.CloseBar.SaveAndCloseButton);

            Assert.Equal([(Spin, "class Spin { int turns; }")], scene.ScriptsWritten);
            Assert.Equal(0, scene.Saves);
            Assert.False(window.IsVisible);
        });
    }

    [Fact]
    public void Close_without_saving_drops_the_script_edit_and_closes()
    {
        UiThread.Run(() =>
        {
            var scene = SceneWithScripts();
            var window = Open(scene);
            EditAScript(window);
            window.Close();

            Click(window.CloseBar.CloseWithoutButton);

            Assert.Empty(scene.ScriptsWritten);
            Assert.False(window.IsVisible);
        });
    }

    [Fact]
    public void A_failed_script_write_on_save_and_close_keeps_the_window_open()
    {
        UiThread.Run(() =>
        {
            var scene = SceneWithScripts();
            scene.ScriptWriteFails = true;
            var window = Open(scene);
            EditAScript(window);
            window.Close();

            Click(window.CloseBar.SaveAndCloseButton);

            Assert.True(window.IsVisible);
            Assert.True(window.Script.IsDirty);
            Assert.Equal("The disk is full.", window.Script.Status.Text);
            Assert.Equal("The disk is full.", window.Status);
        });
    }

    [Fact]
    public void A_failed_script_write_on_Save_leaves_the_scene_file_alone()
    {
        UiThread.Run(() =>
        {
            var scene = SceneWithScripts();
            scene.ScriptWriteFails = true;
            var window = Open(scene);
            EditAScript(window);

            Click(window.SaveButton);

            Assert.Equal(0, scene.Saves);
            Assert.Equal("The disk is full.", window.Status);
        });
    }

    [Fact]
    public void A_play_writes_an_edited_script_first()
    {
        UiThread.Run(() =>
        {
            var scene = SceneWithScripts();
            var window = Open(scene);
            EditAScript(window);

            Click(window.PlayButton);

            Assert.Equal([(Spin, "class Spin { int turns; }")], scene.ScriptsWritten);
            Assert.Equal(1, scene.Plays);
        });
    }

    [Fact]
    public void A_play_does_not_start_when_the_script_cannot_be_written()
    {
        UiThread.Run(() =>
        {
            var scene = SceneWithScripts();
            scene.ScriptWriteFails = true;
            var window = Open(scene);
            EditAScript(window);

            Click(window.PlayButton);

            Assert.Equal(0, scene.Plays);
        });
    }

    [Fact]
    public void A_tool_letter_typed_in_the_script_box_is_left_for_the_box()
    {
        UiThread.Run(() =>
        {
            var window = Open(SceneWithScripts());
            ShowScript(window);
            var mode = window.Toolbar.Mode;
            var press = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.R };

            window.Script.Editor.RaiseEvent(press);

            Assert.False(press.Handled);
            Assert.Equal(mode, window.Toolbar.Mode);
        });
    }
}

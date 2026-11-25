using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Orblit.Editor.App.Shell;
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
            AssertBox(56, 46, 3328, 1183, BoxOf(window.Script, window));
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
            Assert.True(window.SaveChanges.IsVisible);
            // The scene has no edits, so the scene's name would point at the wrong file.
            Assert.Equal("Save spin.cs?", window.SaveChanges.Title);
            Assert.Equal("It has changes that are not saved.", window.SaveChanges.Why.Text);
            Assert.Equal(["spin.cs", "res://spin.cs · 1 line changed"], Texts(window.ChangesList));
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

            Click(window.SaveChanges.MainButton);

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

            Click(window.SaveChanges.LeadButton!);

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

            Click(window.SaveChanges.MainButton);

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
            var card = Assert.Single(window.Toasts.Cards);
            Assert.Equal("Couldn't save the script", card.Title);
            Assert.Equal("The disk is full.", card.Line.Text);
            scene.ScriptWriteFails = false;
            Click(card.ActionButtons[0]);
            Assert.Equal([(Spin, "class Spin { int turns; }")], scene.ScriptsWritten);
        });
    }

    [Fact]
    public void Shift_Command_B_checks_the_scripts_and_Find_a_script_opens_Search()
    {
        UiThread.Run(() =>
        {
            var window = Open(SceneWithScripts());
            ShowScript(window);
            var press = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.B, KeyModifiers = window.CommandKey | KeyModifiers.Shift };

            window.RaiseEvent(press);

            Assert.True(press.Handled);
            Assert.Equal("No problems", window.Script.ProblemsText);

            Click(window.Script.FindScriptButton);

            Assert.True(window.Search.Picker.IsVisible);
            Assert.False(window.Search.ScenesChip.IsVisible);
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

    [Fact]
    public void Script_folds_both_sides_to_rails_until_a_rail_button_opens_one()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(SceneWithScripts());

            ShowScript(window);
            window.UpdateLayout();

            Assert.False(window.Center.Stage.IsVisible);
            Assert.Equal(["Scene dock", "Files dock"], window.LeftSide.RailButtons.Select(button => ToolTip.GetTip(button) as string));
            Assert.Equal(["Inspector dock"], window.RightSide.RailButtons.Select(button => ToolTip.GetTip(button) as string));
            Assert.False(window.SceneDock.IsEffectivelyVisible);
            Assert.Equal(DockSide.RailWidth, window.LeftSide.Rail.Bounds.Width);

            Click(window.LeftSide.RailButtons.First());
            window.UpdateLayout();

            Assert.True(window.SceneDock.IsEffectivelyVisible);
            Assert.True(window.RightSide.IsFolded);

            ClickOn(window.Bar.Tabs.Single(tab => tab.Label == TitleBar.Spatial), window);
            window.UpdateLayout();

            Assert.False(window.RightSide.IsFolded);
            Assert.True(window.InspectorDock.IsEffectivelyVisible);
            Assert.True(window.Center.Stage.IsVisible);

            ShowScript(window);

            Assert.True(window.LeftSide.IsFolded);
        });
    }
}

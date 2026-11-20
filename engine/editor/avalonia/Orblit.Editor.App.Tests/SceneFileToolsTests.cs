using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Orblit.Editor.Bridge;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class SceneFileToolsTests
{
    static void CreateScene(EditorWindow window, string name)
    {
        ClickOn(window.SceneTabs.AddButton, window);
        window.NewScene.NameBox.Text = name;
        Click(window.NewScene.CreateButton);
    }

    static void OpenInstanceFlyout(EditorWindow window) => window.InstanceFlyout.ShowAt(window.ScenePane.InstanceButton);

    static IEnumerable<NativeMenuItem> Items(NativeMenu menu) =>
        menu.Items.OfType<NativeMenuItem>().SelectMany(item => item.Menu is { } sub ? Items(sub).Prepend(item) : [item]);

    [Fact]
    public void The_plus_the_key_and_the_menu_open_the_new_scene_dialog_over_the_window()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var menuItem = Items(NativeMenu.GetMenu(window)!).Single(item => item.Header == "New Scene");
            Action[] ways =
            [
                () => ClickOn(window.SceneTabs.AddButton, window),
                () => window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.N, KeyModifiers = window.CommandKey }),
                () => menuItem.Command!.Execute(menuItem.CommandParameter),
            ];

            foreach (var open in ways)
            {
                window.NewScene.Dialog.Close();
                open();
                Assert.True(window.NewScene.Dialog.IsVisible);
            }

            Assert.True(window.NewScene.Dialog.IsAttachedToVisualTree());
            Assert.True(window.Graph.CreateDialog.IsAttachedToVisualTree());
        });
    }

    [Fact]
    public void A_new_scene_is_made_at_the_project_root_opened_and_listed()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            ClickOn(window.SceneTabs.AddButton, window);
            window.NewScene.Roots.ElementAt(1).IsChecked = true;
            window.NewScene.NameBox.Text = " arena ";

            Click(window.NewScene.CreateButton);

            Assert.Equal([("res://arena.oscn", "Entity2D")], scene.Created);
            Assert.Equal("Opened arena.", window.Status);
            Assert.StartsWith("arena.oscn", window.Title);
            Assert.Contains(FileNodes(window), node => node.Path == "res://arena.oscn");
            Assert.False(window.NewScene.Dialog.IsVisible);
        });
    }

    [Fact]
    public void A_new_scene_saves_the_open_one_first()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            MakeAnEdit(window);

            CreateScene(window, "arena");

            Assert.Equal(1, scene.Saves);
            Assert.Single(scene.Created);
        });
    }

    [Fact]
    public void A_failed_save_stops_the_new_scene_and_the_dialog_says_why()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { SaveFails = true };
            var window = OpenAtReferenceSize(scene);
            MakeAnEdit(window);

            CreateScene(window, "arena");

            Assert.Empty(scene.Created);
            Assert.Equal("The engine could not save the scene. Error code 7.", window.Status);
            Assert.Equal("The engine could not save the scene. Error code 7.", window.NewScene.Fault);
            Assert.True(window.NewScene.Dialog.IsVisible);
        });
    }

    [Fact]
    public void A_name_that_is_a_scene_already_keeps_Create_off_and_changes_nothing()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            ClickOn(window.SceneTabs.AddButton, window);

            window.NewScene.NameBox.Text = "field";

            Assert.Equal("field.oscn already exists. Pick another name.", window.NewScene.Fault);
            Assert.False(window.NewScene.CreateButton.IsEnabled);
            Assert.Empty(scene.Opened);
            Assert.Empty(scene.Created);
        });
    }

    [Fact]
    public void An_engine_failure_on_a_new_scene_goes_to_the_status_and_the_dialog()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(new FakeScene { CreateFails = true });

            CreateScene(window, "arena");

            Assert.Equal("The engine could not save res://arena.oscn. Error code 7.", window.Status);
            Assert.Equal("The engine could not save res://arena.oscn. Error code 7.", window.NewScene.Fault);
            Assert.True(window.NewScene.Dialog.IsVisible);
        });
    }

    [Fact]
    public void Close_saves_edits_then_leaves_no_scene_open()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            MakeAnEdit(window);

            ClickOn(window.SceneTabs.CloseButton, window);

            Assert.Equal(1, scene.Saves);
            Assert.Equal(1, scene.Closed);
            Assert.False(window.SceneTabs.Tab.IsVisible);
            Assert.Equal("No scene is open.", window.Status);
            Assert.False(window.SaveButton.IsEnabled);
            Assert.False(window.PlayButton.IsEnabled);
            Assert.Empty(window.Outline.ItemsSource!.Cast<OutlineNode>());
            Assert.DoesNotContain("(*)", window.Title);
        });
    }

    [Fact]
    public void A_scene_with_no_edits_is_closed_without_a_save()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);

            ClickOn(window.SceneTabs.CloseButton, window);

            Assert.Equal(0, scene.Saves);
            Assert.Equal(1, scene.Closed);
        });
    }

    [Fact]
    public void A_failed_save_keeps_the_scene_open()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { SaveFails = true };
            var window = OpenAtReferenceSize(scene);
            MakeAnEdit(window);

            ClickOn(window.SceneTabs.CloseButton, window);

            Assert.Equal(0, scene.Closed);
            Assert.True(window.SceneTabs.Tab.IsVisible);
        });
    }

    [Fact]
    public void The_instance_button_opens_a_flyout_that_lists_the_other_scenes_and_no_folders()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { SceneFile = "res://field.oscn" };
            scene.ScenePaths.Add("res://levels/");
            var window = OpenAtReferenceSize(scene);

            ClickOn(window.ScenePane.InstanceButton, window);

            Assert.True(window.InstanceFlyout.IsOpen);
            Assert.Same(window.InstanceScene, window.InstanceFlyout.Content);
            Assert.Equal(["res://levels/second.oscn"], window.InstanceScene.Scene.ItemsSource!.Cast<string>());
        });
    }

    [Fact]
    public void A_selection_change_does_not_read_the_project_scene_list()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            var asked = scene.ScenesAsked;

            window.Outline.SelectedItem = FakeScene.Lamp;

            Assert.Equal(asked, scene.ScenesAsked);
        });
    }

    [Fact]
    public void Instance_is_off_until_a_scene_is_chosen_and_while_none_is_open()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            OpenInstanceFlyout(window);
            var instance = window.InstanceScene.InstanceButton;
            Assert.False(instance.IsEnabled);

            window.InstanceScene.Scene.SelectedIndex = 0;
            Assert.True(instance.IsEnabled);

            var empty = OpenAtReferenceSize(new FakeScene { StartsEmpty = true });
            OpenInstanceFlyout(empty);
            empty.InstanceScene.Scene.SelectedIndex = 0;
            Assert.False(empty.InstanceScene.InstanceButton.IsEnabled);
        });
    }

    [Fact]
    public void Opening_the_flyout_again_clears_the_chosen_scene()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            OpenInstanceFlyout(window);
            window.InstanceScene.Scene.SelectedIndex = 0;

            window.InstanceScene.LoadScenes();

            Assert.Equal(-1, window.InstanceScene.Scene.SelectedIndex);
            Assert.False(window.InstanceScene.InstanceButton.IsEnabled);
        });
    }

    [Fact]
    public void An_instance_goes_under_the_selected_entity_and_is_selected_and_marks_an_edit()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            window.Outline.SelectedItem = FakeScene.Lamp;
            OpenInstanceFlyout(window);
            window.InstanceScene.Scene.SelectedIndex = 0;

            Click(window.InstanceScene.InstanceButton);

            Assert.Equal([(FakeScene.Lamp.Handle, "res://field.oscn")], scene.Instanced);
            Assert.Equal("field", window.ScenePane.Selected!.Name);
            Assert.Equal("Instanced field under Lamp.", window.Status);
            Assert.StartsWith("(*)", window.Title);
            Assert.False(window.InstanceFlyout.IsOpen);
        });
    }

    [Fact]
    public void With_nothing_selected_an_instance_goes_under_the_root()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            OpenInstanceFlyout(window);
            window.InstanceScene.Scene.SelectedIndex = 0;

            Click(window.InstanceScene.InstanceButton);

            Assert.Equal([(FakeScene.Field.Handle, "res://field.oscn")], scene.Instanced);
            Assert.Equal("Instanced field under Field.", window.Status);
        });
    }

    [Fact]
    public void A_refused_instance_goes_to_the_status_and_marks_no_edit()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(new FakeScene { InstanceFails = true });
            OpenInstanceFlyout(window);
            window.InstanceScene.Scene.SelectedIndex = 0;

            Click(window.InstanceScene.InstanceButton);

            Assert.Equal("res://field.oscn is not a scene.", window.Status);
            Assert.DoesNotContain("(*)", window.Title);
        });
    }
}

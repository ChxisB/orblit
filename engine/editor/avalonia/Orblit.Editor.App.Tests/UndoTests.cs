using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class UndoTests
{
    static void Press(EditorWindow window, Key key, KeyModifiers modifiers) =>
        window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers });

    static string[] Names(FakeScene scene) => [.. scene.Outline()!.Children.Select(child => child.Name)];

    static string[] Rows(EditorWindow window) =>
        [.. window.History.Rows.GetLogicalDescendants().OfType<TextBlock>().Select(text => text.Text ?? "")];

    [Fact]
    public void Command_z_takes_a_rename_back_and_command_shift_z_makes_it_again()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            window.Outline.SelectedItem = FakeScene.Lamp;
            window.Tools.Rename("Sun");

            Press(window, Key.Z, window.CommandKey);
            Assert.Equal(["Lamp", "Sound"], Names(scene));
            Assert.Equal("Undid Renamed Lamp. Save keeps the change.", window.Status);

            Press(window, Key.Z, window.CommandKey | KeyModifiers.Shift);
            Assert.Equal(["Sun", "Sound"], Names(scene));
            Assert.Equal("Redid Renamed Lamp. Save keeps the change.", window.Status);
        });
    }

    [Fact]
    public void Undo_puts_a_removed_entity_back_in_its_place_and_a_cleared_history_frees_it()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            window.Outline.SelectedItem = FakeScene.Lamp;
            Click(window.Tools.RemoveButton);
            Assert.Equal(["Sound"], Names(scene));

            Press(window, Key.Z, window.CommandKey);
            Assert.Equal(["Lamp", "Sound"], Names(scene));
            Press(window, Key.Z, window.CommandKey | KeyModifiers.Shift);
            Assert.Equal(["Sound"], Names(scene));
            Assert.Empty(scene.Destroyed);

            Click(window.History.ClearButton);
            Assert.Equal([FakeScene.Lamp.Handle], scene.Destroyed);
        });
    }

    [Fact]
    public void History_lists_each_step_under_the_open_scene_and_a_click_goes_back_to_one()
    {
        UiThread.Run(() =>
        {
            var time = new ManualTime();
            var scene = new FakeScene { SceneFile = "res://field.oscn" };
            var window = OpenAtReferenceSize(scene, time: time);
            Assert.False(window.History.ClearButton.IsEnabled);
            window.Outline.SelectedItem = FakeScene.Lamp;
            window.Tools.Rename("Sun");
            time.Advance(TimeSpan.FromMinutes(2));
            window.Outline.SelectedItem = window.Outline.ItemsSource!.Cast<Bridge.OutlineNode>().Single().Children[1];
            window.Tools.Rename("Bell");

            Assert.Equal(["16:35", "Renamed Sound", "16:33", "Renamed Lamp", "16:33", "Opened field.oscn"], Rows(window));

            Click((Button)window.History.Rows.Children[^1]);

            Assert.Equal(["Lamp", "Sound"], Names(scene));
            Assert.Equal("Undid 2 steps. Save keeps the change.", window.Status);
            Assert.Equal([0.45, 0.45, 1], window.History.Rows.Children.Select(row => row.Opacity));
            Assert.Equal(["Faded steps are undone.", "⌃⇧Z", "redoes them."], Texts(window.History).Take(3));
        });
    }
}

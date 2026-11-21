using System.Numerics;
using Avalonia.Controls;
using Avalonia.Input;
using Orblit.Editor.Bridge;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class MixedValuesTests
{
    // Eye and Lens are added first, because the outline lists only what the scene held when the window opened.
    static (EditorWindow Window, FakeScene Scene, nint Eye, nint Other) Two(string className)
    {
        var scene = new FakeScene();
        var eye = scene.AddChild(FakeScene.Field.Handle, "Camera3D", "Eye");
        var other = scene.AddChild(FakeScene.Field.Handle, className, "Lens");
        return (OpenAtReferenceSize(scene), scene, eye, other);
    }

    static void PickBoth(EditorWindow window, FakeScene scene, nint first, nint second)
    {
        window.Outline.SelectedItem = scene.Outline()!.Find(first);
        window.Outline.SelectedItems!.Add(scene.Outline()!.Find(second));
        window.UpdateLayout();
    }

    static TextBox Box(EditorWindow window, string label) => (TextBox)window.Inspector.Rows.Single(row => row.Label == label).Editor;

    static void Type(TextBox box, string text)
    {
        box.Text = text;
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
    }

    static void Undo(EditorWindow window) =>
        window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Z, KeyModifiers = window.CommandKey });

    [Fact]
    public void A_field_whose_values_differ_shows_Mixed_and_one_edit_writes_both_as_one_undo_step()
    {
        UiThread.Run(() =>
        {
            var (window, scene, eye, lens) = Two("Camera3D");
            scene.SetProperty(eye, "fov", 90.0);
            PickBoth(window, scene, eye, lens);

            Assert.Equal(("", "Mixed"), (Box(window, "FOV").Text, Box(window, "FOV").PlaceholderText));
            Assert.Equal("0.05", Box(window, "Near").Text);

            Type(Box(window, "FOV"), "60");
            Assert.Equal((60.0, 60.0), (scene.PropertyValues[(eye, "fov")], scene.PropertyValues[(lens, "fov")]));
            Assert.Equal("60", Box(window, "FOV").Text);

            Undo(window);
            Assert.Equal((90.0, 75.0), (scene.PropertyValues[(eye, "fov")], scene.PropertyValues[(lens, "fov")]));
            Assert.Equal("Undid Edited 2 entities. Save keeps the change.", window.Status);
        });
    }

    [Fact]
    public void A_vector_axis_that_differs_shows_Mixed_and_an_edit_on_another_axis_keeps_each_entitys_own()
    {
        UiThread.Run(() =>
        {
            var (window, scene, eye, lens) = Two("Camera3D");
            scene.MoveTo(eye, Vector3.Zero);
            scene.MoveTo(lens, new Vector3(1, 0, 0));
            PickBoth(window, scene, eye, lens);

            var axes = window.Inspector.Position.Axes;
            Assert.Equal(["", "0", "0"], axes.Select(axis => axis.Text));
            Assert.Equal("Mixed", axes[0].PlaceholderText);

            Type((TextBox)axes[1], "2");
            Assert.Equal((new Vector3(0, 2, 0), new Vector3(1, 2, 0)), (scene.PositionOf(eye), scene.PositionOf(lens)));

            Undo(window);
            Assert.Equal((Vector3.Zero, new Vector3(1, 0, 0)), (scene.PositionOf(eye), scene.PositionOf(lens)));
        });
    }

    [Fact]
    public void Leaving_a_Mixed_box_untouched_writes_nothing()
    {
        UiThread.Run(() =>
        {
            var (window, scene, eye, lens) = Two("Camera3D");
            scene.SetProperty(eye, "fov", 90.0);
            scene.MoveTo(eye, Vector3.Zero);
            scene.MoveTo(lens, new Vector3(1, 0, 0));
            PickBoth(window, scene, eye, lens);

            Box(window, "FOV").RaiseEvent(new FocusChangedEventArgs(InputElement.LostFocusEvent));
            window.Inspector.Position.Axes[0].RaiseEvent(new FocusChangedEventArgs(InputElement.LostFocusEvent));

            Assert.False(scene.PropertyValues.ContainsKey((lens, "fov")));
            Assert.False(window.Inspector.Position.Fault.IsVisible);
            Assert.DoesNotContain("(*)", window.Title);
        });
    }

    [Fact]
    public void The_inspector_shows_only_the_classes_every_picked_entity_has_and_the_chip_counts_them()
    {
        UiThread.Run(() =>
        {
            var (window, scene, eye, sun) = Two("Light3D");
            PickBoth(window, scene, eye, sun);

            Assert.Equal(["Entity3D", "Entity"], window.Inspector.Chain);
            Assert.DoesNotContain(window.Inspector.Rows, row => row.Label is "FOV" or "Light Color");
            Assert.False(window.Inspector.MetadataButton.IsVisible);
            Assert.Equal("2 selected", window.Toolbar.NameText.Text);
            // The gizmo and the tools follow the entity picked last.
            Assert.Equal(sun, window.ScenePane.Selected!.Handle);
        });
    }

    [Fact]
    public void Dropping_one_of_two_from_the_selection_shows_the_other_alone()
    {
        UiThread.Run(() =>
        {
            var (window, scene, eye, lens) = Two("Camera3D");
            scene.SetProperty(eye, "fov", 90.0);
            PickBoth(window, scene, eye, lens);

            window.Outline.SelectedItems!.Remove(scene.Outline()!.Find(lens));
            window.UpdateLayout();

            Assert.Equal(eye, window.ScenePane.Selected!.Handle);
            Assert.Equal("90", Box(window, "FOV").Text);
            Assert.Equal("Eye", window.Toolbar.NameText.Text);
        });
    }
}

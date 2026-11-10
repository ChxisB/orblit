using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class SelectionFlagsTests
{
    // The fake scene: Field (3) holds Lamp (1), which has a place, and Sound (2), which has none.
    const int LampHandle = 1;
    const int SoundHandle = 2;
    const int FieldHandle = 3;

    static OutlineRow[] Rows(EditorWindow window)
    {
        window.UpdateLayout();
        return window.Outline.GetVisualDescendants().OfType<OutlineRow>().ToArray();
    }

    static Button EyeOf(OutlineRow row) => row.GetVisualDescendants().OfType<Button>().Single();

    // The first icon of a row is the class icon. The eye is the last.
    static EditorIcon ClassIconOf(OutlineRow row) => row.GetVisualDescendants().OfType<EditorIcon>().First();

    static string[] IconsOf(Visual visual) => visual.GetVisualDescendants().OfType<EditorIcon>().Select(icon => icon.IconName).ToArray();

    static void Select(EditorWindow window, FakeScene scene, nint handle) => window.Outline.SelectedItem = scene.Outline()!.Find(handle);

    static EditorWindow OpenSelected(FakeScene scene, nint handle)
    {
        var window = OpenAtReferenceSize(scene);
        Select(window, scene, handle);
        return window;
    }

    [Fact]
    public void Lock_and_group_light_for_the_flags_the_selection_already_has()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            scene.SetLocked(LampHandle, true);
            scene.SetGrouped(LampHandle, true);
            var window = OpenSelected(scene, LampHandle);

            Assert.Equal([true, true], [window.Toolbar.LockButton.IsChecked == true, window.Toolbar.GroupButton.IsChecked == true]);

            Select(window, scene, FieldHandle);
            Assert.Equal([false, false], [window.Toolbar.LockButton.IsChecked == true, window.Toolbar.GroupButton.IsChecked == true]);
        });
    }

    [Fact]
    public void An_entity_with_no_place_cannot_be_locked_or_grouped()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenSelected(scene, SoundHandle);

            Assert.False(window.Toolbar.LockButton.IsEnabled);
            Assert.False(window.Toolbar.GroupButton.IsEnabled);
        });
    }

    [Fact]
    public void A_click_on_lock_locks_the_selection_then_frees_it()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenSelected(scene, LampHandle);

            ClickOn(window.Toolbar.LockButton, window);

            Assert.True(scene.Outline()!.Find(LampHandle)!.Locked);
            Assert.True(window.Toolbar.LockButton.IsChecked);
            Assert.Equal("Locked Lamp. Save keeps the change.", window.Status);
            Assert.StartsWith("(*)", window.Title);
            Assert.Equal(LampHandle, ((OutlineNode)window.Outline.SelectedItem!).Handle);

            ClickOn(window.Toolbar.LockButton, window);

            Assert.False(scene.Outline()!.Find(LampHandle)!.Locked);
            Assert.False(window.Toolbar.LockButton.IsChecked);
            Assert.Equal("Unlocked Lamp. Save keeps the change.", window.Status);
        });
    }

    [Fact]
    public void A_click_on_group_groups_the_selection_then_ungroups_it()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenSelected(scene, LampHandle);

            ClickOn(window.Toolbar.GroupButton, window);

            Assert.True(scene.Outline()!.Find(LampHandle)!.Grouped);
            Assert.False(scene.Outline()!.Find(LampHandle)!.Locked);
            Assert.True(window.Toolbar.GroupButton.IsChecked);
            Assert.Equal("Grouped Lamp. Save keeps the change.", window.Status);

            ClickOn(window.Toolbar.GroupButton, window);

            Assert.False(scene.Outline()!.Find(LampHandle)!.Grouped);
            Assert.Equal("Ungrouped Lamp. Save keeps the change.", window.Status);
        });
    }

    [Fact]
    public void A_locked_selection_has_no_gizmo_and_a_freed_one_has_it_again()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { Handles = new GizmoHandles(new Vector2(50, 25), new Vector2(80, 25), new Vector2(50, 0), new Vector2(50, 25)) };
            scene.SetLocked(LampHandle, true);
            var window = OpenAtReferenceSize(scene);
            var viewport = new ViewportControl(() => false, Frame);
            window.ShowViewport(viewport);
            window.UpdateLayout();
            Select(window, scene, LampHandle);
            scene.HandlesAsked.Clear();

            viewport.Tick();
            Assert.Empty(scene.HandlesAsked);
            Assert.True(window.FrameButton.IsEnabled);

            ClickOn(window.Toolbar.LockButton, window);
            viewport.Tick();
            Assert.Contains((nint)LampHandle, scene.HandlesAsked);
        });
    }

    static bool Frame(out int width, out int height, ref byte[] rgba)
    {
        width = 100;
        height = 50;
        rgba = new byte[width * height * 4];
        return true;
    }

    [Fact]
    public void Keep_child_transforms_is_off_at_the_start_and_follows_its_button()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            Assert.False(scene.KeepChildren);
            Assert.False(window.Toolbar.KeepChildrenButton.IsChecked);

            ClickOn(window.Toolbar.KeepChildrenButton, window);
            Assert.True(scene.KeepChildren);
            Assert.True(window.Toolbar.KeepChildrenButton.IsChecked);
            Assert.DoesNotContain("not built", window.Status);

            ClickOn(window.Toolbar.KeepChildrenButton, window);
            Assert.False(scene.KeepChildren);
        });
    }

    [Fact]
    public void Keep_child_transforms_pressed_before_a_scene_loads_is_carried_to_the_scene()
    {
        UiThread.Run(() =>
        {
            var window = new EditorWindow { Width = 3440, Height = 1279 };
            window.Show();
            window.UpdateLayout();

            ClickOn(window.Toolbar.KeepChildrenButton, window);
            var scene = new FakeScene();
            window.Load(scene);

            Assert.True(scene.KeepChildren);
        });
    }

    [Fact]
    public void A_click_on_the_eye_hides_the_entity_and_the_row_shows_it()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenSelected(scene, FieldHandle);
            var lamp = Rows(window)[1];
            Assert.Equal(["GuiVisibilityVisible"], IconsOf(EyeOf(lamp)));
            Assert.Equal([1, 1], [lamp.GetVisualDescendants().OfType<TextBlock>().Single().Opacity, ClassIconOf(lamp).Opacity]);

            ClickOn(EyeOf(lamp), window);

            lamp = Rows(window)[1];
            Assert.True(scene.Outline()!.Find(LampHandle)!.Hidden);
            Assert.Equal(["GuiVisibilityHidden"], IconsOf(EyeOf(lamp)));
            Assert.True(lamp.GetVisualDescendants().OfType<TextBlock>().Single().Opacity < 1);
            Assert.True(ClassIconOf(lamp).Opacity < 1);
            Assert.Equal("Hid Lamp. Save keeps the change.", window.Status);
            Assert.StartsWith("(*)", window.Title);
        });
    }

    [Fact]
    public void A_second_click_on_the_eye_shows_the_entity_again()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            scene.SetHidden(LampHandle, true);
            var window = OpenAtReferenceSize(scene);

            ClickOn(EyeOf(Rows(window)[1]), window);

            var lamp = Rows(window)[1];
            Assert.False(scene.Outline()!.Find(LampHandle)!.Hidden);
            Assert.Equal(["GuiVisibilityVisible"], IconsOf(EyeOf(lamp)));
            Assert.Equal([1, 1], [lamp.GetVisualDescendants().OfType<TextBlock>().Single().Opacity, ClassIconOf(lamp).Opacity]);
            Assert.Equal("Showed Lamp. Save keeps the change.", window.Status);
        });
    }

    [Fact]
    public void A_click_on_an_eye_leaves_the_selection_where_it_was()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenSelected(scene, FieldHandle);

            ClickOn(EyeOf(Rows(window)[1]), window);

            Assert.Equal(FieldHandle, ((OutlineNode)window.Outline.SelectedItem!).Handle);
        });
    }

    [Fact]
    public void A_locked_row_shows_a_lock_and_a_grouped_row_a_group()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            Assert.DoesNotContain("Lock", IconsOf(Rows(window)[1]));
            Assert.DoesNotContain("Group", IconsOf(Rows(window)[1]));

            scene.SetLocked(LampHandle, true);
            scene.SetGrouped(LampHandle, true);
            window.Load(scene);

            Assert.Contains("Lock", IconsOf(Rows(window)[1]));
            Assert.Contains("Group", IconsOf(Rows(window)[1]));
            Assert.DoesNotContain("Lock", IconsOf(Rows(window)[0]));
        });
    }

    [Fact]
    public void A_row_without_flags_keeps_its_name_where_it_was()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var plain = BoxOf(Rows(window)[1].GetVisualDescendants().OfType<TextBlock>().Single(), window);

            var scene = new FakeScene();
            scene.SetLocked(LampHandle, true);
            var locked = OpenAtReferenceSize(scene);

            Assert.Equal(plain.X, BoxOf(Rows(locked)[1].GetVisualDescendants().OfType<TextBlock>().Single(), locked).X);
        });
    }
}

using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using Orblit.Editor.Bridge;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class InspectorPropertyTests
{
    static (EditorWindow Window, FakeScene Scene, nint Handle) Picked(string className)
    {
        var scene = new FakeScene();
        var handle = scene.AddChild(FakeScene.Field.Handle, className, "Eye");
        var window = OpenAtReferenceSize(scene);
        Pick(window, scene, handle);
        return (window, scene, handle);
    }

    static PropertyRow Row(EditorWindow window, string label) => window.Inspector.Rows.Single(row => row.Label == label);

    // A Camera3D named Eye is picked beside a second entity. Both are added first, because the outline lists only what the scene held when the window opened.
    static (EditorWindow Window, FakeScene Scene, nint Eye, nint Other) Two(string className, string name)
    {
        var scene = new FakeScene();
        var eye = scene.AddChild(FakeScene.Field.Handle, "Camera3D", "Eye");
        var other = scene.AddChild(FakeScene.Field.Handle, className, name);
        var window = OpenAtReferenceSize(scene);
        Pick(window, scene, eye);
        return (window, scene, eye, other);
    }

    static void Pick(EditorWindow window, FakeScene scene, nint handle)
    {
        window.Outline.SelectedItem = scene.Outline()!.Find(handle);
        window.UpdateLayout();
    }

    static void Choose(EditorWindow window, string header) =>
        ((MenuFlyout)window.InspectorPane.ToolsButton.Flyout!).Items.OfType<MenuItem>().Single(item => (string?)item.Header == header).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    [Fact]
    public void Paste_Properties_sets_the_copied_values_on_another_entity_of_the_class_and_leaves_objects_out()
    {
        UiThread.Run(() =>
        {
            var (window, scene, eye, lens) = Two("Camera3D", "Lens");
            scene.SetProperty(eye, "fov", 90.0);
            scene.SetProperty(eye, "current", true);

            Choose(window, "Copy Properties");
            Pick(window, scene, lens);
            Choose(window, "Paste Properties");

            Assert.Equal((90.0, true), (scene.PropertyValues[(lens, "fov")], scene.PropertyValues[(lens, "current")]));
            Assert.False(scene.PropertyValues.ContainsKey((lens, "environment")));
            Assert.Equal("90", ((TextBox)Row(window, "FOV").Editor).Text);
        });
    }

    [Fact]
    public void Paste_Properties_refuses_an_entity_of_another_class_and_says_so()
    {
        UiThread.Run(() =>
        {
            var (window, scene, _, sun) = Two("Light3D", "Sun");

            Choose(window, "Paste Properties");
            Assert.Equal("Copy Properties first, then paste them onto an entity of the same class.", window.Status);

            Choose(window, "Copy Properties");
            Pick(window, scene, sun);
            Choose(window, "Paste Properties");

            Assert.Equal("The copied properties are a Camera3D's, and Sun is a Light3D.", window.Status);
            Assert.DoesNotContain(scene.PropertyValues.Keys, key => key.Entity == sun);
        });
    }

    [Fact]
    public void Make_Sub_Resources_Unique_gives_the_selected_entity_its_own_resources()
    {
        UiThread.Run(() =>
        {
            var (window, scene, eye) = Picked("Camera3D");

            Choose(window, "Make Sub-Resources Unique");

            Assert.Equal([eye], scene.MadeUnique);
        });
    }

    static void Type(TextBox box, string text)
    {
        box.Text = text;
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
    }

    [Fact]
    public void A_Camera3D_shows_each_class_under_its_bar_with_its_own_rows_and_the_chain_in_the_chip()
    {
        UiThread.Run(() =>
        {
            var (window, _, _) = Picked("Camera3D");
            var bars = window.Inspector.GetVisualDescendants().OfType<ClassBar>().Where(bar => bar.IsEffectivelyVisible);

            Assert.Equal(["Camera3D", "Entity3D", "Entity"], bars.Select(bar => bar.Title));
            Assert.Equal("Camera3D › Entity3D › Entity", window.InspectorPane.InspectedClass.Text);
            Assert.Equal(["Cull Mask", "Environment", "Projection", "Current", "FOV", "Frustum Offset", "Near", "Far"], window.Inspector.Rows.Take(8).Select(row => row.Label));
            Assert.All(window.Inspector.Rows.Take(8), row => Assert.True(row.IsEffectivelyVisible));
        });
    }

    [Fact]
    public void A_long_chain_in_the_chip_ends_before_its_chevron()
    {
        UiThread.Run(() =>
        {
            var (window, _, _) = Picked("MeshInstance3D");
            var pane = window.InspectorPane;
            var chevron = ((DockPanel)pane.Chip.Content!).Children[0];
            var classRight = pane.InspectedClass.TranslatePoint(new Point(pane.InspectedClass.Bounds.Width, 0), pane.Chip)!.Value.X;

            Assert.True(classRight <= chevron.TranslatePoint(default, pane.Chip)!.Value.X, $"the class ends at {classRight}");
        });
    }

    [Fact]
    public void A_number_shows_its_unit_takes_typed_text_and_puts_back_text_that_is_not_a_number()
    {
        UiThread.Run(() =>
        {
            var (window, scene, handle) = Picked("Camera3D");
            var fov = (TextBox)Row(window, "FOV").Editor;
            Assert.Equal(("75", "°"), (fov.Text, ((TextBlock)fov.InnerRightContent!).Text));

            Type(fov, "90.5");
            Assert.Equal(90.5, scene.PropertyValues[(handle, "fov")]);
            Assert.Equal("90.5", fov.Text);

            Type(fov, "wide");
            Assert.Equal("90.5", fov.Text);
            Assert.Equal(90.5, scene.PropertyValues[(handle, "fov")]);
        });
    }

    [Fact]
    public void An_enum_a_switch_and_a_flat_vector_each_set_a_value_of_their_type()
    {
        UiThread.Run(() =>
        {
            var (window, scene, handle) = Picked("Camera3D");
            var projection = (ComboBox)Row(window, "Projection").Editor;
            var offset = (VectorRow)Row(window, "Frustum Offset").Editor;
            Assert.Equal(0, projection.SelectedIndex);
            Assert.Equal(2, offset.Axes.Count);

            projection.SelectedIndex = 2;
            ((ToggleSwitch)Row(window, "Current").Editor).IsChecked = true;
            Type(offset.Axes[0], "3");

            Assert.Equal(2L, scene.PropertyValues[(handle, "projection")]);
            Assert.Equal(true, scene.PropertyValues[(handle, "current")]);
            Assert.Equal(new Vector2(3, 0), scene.PropertyValues[(handle, "frustum_offset")]);
        });
    }

    [Fact]
    public void Another_kind_shows_its_value_read_only_and_an_empty_object_says_so()
    {
        UiThread.Run(() =>
        {
            var (window, _, _) = Picked("Camera3D");

            Assert.Equal("<empty>", ((TextBlock)Row(window, "Environment").Editor).Text);
            Assert.Equal("1048575", ((TextBlock)Row(window, "Cull Mask").Editor).Text);
        });
    }

    [Fact]
    public void A_colour_shows_its_hex_and_a_colour_with_no_alpha_keeps_it_full()
    {
        UiThread.Run(() =>
        {
            var (window, scene, handle) = Picked("DirectionalLight3D");
            var hex = ((DockPanel)Row(window, "Color").Editor).Children.OfType<TextBox>().Single();
            Assert.Equal("#ffffff", hex.Text);

            Type(hex, "#ff000080");

            Assert.Equal(new Vector4(1, 0, 0, 1), scene.PropertyValues[(handle, "light_color")]);
            Assert.Equal("#ff0000", hex.Text);
        });
    }

    [Fact]
    public void A_filter_counts_the_class_rows_and_keeps_only_their_bar()
    {
        UiThread.Run(() =>
        {
            var (window, _, _) = Picked("Camera3D");

            window.InspectorPane.FilterBox.Text = "fov";

            Assert.Equal(["Camera3D"], window.Inspector.GetVisualDescendants().OfType<ClassBar>().Where(bar => bar.IsEffectivelyVisible).Select(bar => bar.Title));
            Assert.Equal("1 of 16 properties match.", window.Inspector.FilterCount.Text);
        });
    }

    [Fact]
    public void A_drag_across_a_number_label_steps_the_number_and_an_angle_in_degrees()
    {
        UiThread.Run(() =>
        {
            var count = new PropertyRow(new EntityProperty("", "", "max_count", 2, 5L));
            var angle = new PropertyRow(new EntityProperty("", "", "spin", 3, 0.0) { HintString = "radians_as_degrees" });
            var window = new Window { Content = new StackPanel { Children = { count, angle } }, Width = 400, Height = 100 };
            window.Show();
            window.UpdateLayout();
            var entered = new List<object>();
            count.Entered += entered.Add;
            angle.Entered += entered.Add;

            foreach (var row in new[] { count, angle })
            {
                var at = BoxOf(row.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == row.Label), window).Center;
                window.MouseDown(at, MouseButton.Left);
                window.MouseMove(at + new Point(6, 0));
                window.MouseUp(at + new Point(6, 0), MouseButton.Left);
            }

            Assert.Equal(8L, entered[0]);
            Assert.Equal(0.3 * Math.PI / 180, (double)entered[1], 9);
            Assert.Equal("0.3", ((TextBox)angle.Editor).Text);
        });
    }
}

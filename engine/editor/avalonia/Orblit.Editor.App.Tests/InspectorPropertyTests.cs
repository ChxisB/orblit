using System.Numerics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class InspectorPropertyTests
{
    static (EditorWindow Window, FakeScene Scene, nint Handle) Picked(string className)
    {
        var scene = new FakeScene();
        var handle = scene.AddChild(FakeScene.Field.Handle, className, "Eye");
        var window = OpenAtReferenceSize(scene);
        window.Outline.SelectedItem = scene.Outline()!.Find(handle);
        window.UpdateLayout();
        return (window, scene, handle);
    }

    static PropertyRow Row(EditorWindow window, string label) => window.Inspector.Rows.Single(row => row.Label == label);

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
    public void Another_type_shows_read_only_and_an_empty_object_says_so()
    {
        UiThread.Run(() =>
        {
            var (window, _, _) = Picked("Camera3D");

            Assert.Equal("<empty>", ((TextBlock)Row(window, "Environment").Editor).Text);
            Assert.Equal("Int", ((TextBlock)Row(window, "Cull Mask").Editor).Text);
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
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Orblit.Editor.App.Theme;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class VectorRowTests
{
    static VectorRow Selected(EditorWindow window)
    {
        window.Outline.SelectedItem = FakeScene.Lamp;
        window.UpdateLayout();
        return window.Inspector.Position;
    }

    static TextBlock[] Texts(VectorRow row) => [.. row.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsVisible)];

    static Border[] Fields(VectorRow row) => [.. row.GetVisualDescendants().OfType<Border>().Where(border => border.Classes.Contains("num"))];

    static Border TagOf(Border field) => (Border)((DockPanel)field.Child!).Children[0];

    [Fact]
    public void A_row_is_a_label_beside_three_fields_28_high_and_4_apart()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var row = Selected(window);
            var rowBox = BoxOf(row, window);
            var label = Texts(row).Single(text => text.Text == "Position");
            var fields = Fields(row).Select(field => BoxOf(field, window)).ToArray();

            Assert.Equal(3, fields.Length);
            Assert.All(fields, field => Assert.Equal((rowBox.Y + 1, 28d), (field.Y, field.Height)));
            Assert.Equal(rowBox.X + 96 + 8, fields[0].X);
            Near(rowBox.Right, fields[2].Right);
            Near(fields[0].Right + 4, fields[1].X);
            Near(fields[1].Right + 4, fields[2].X);
            Assert.All(Fields(row), field => Assert.Equal(
                (EditorBrushes.Field, EditorBrushes.Line, new Thickness(1), new CornerRadius(6)),
                (field.Background, field.BorderBrush, field.BorderThickness, field.CornerRadius)));
            Assert.Equal(rowBox.X, BoxOf(label, window).X);
            Assert.Equal((12d, EditorBrushes.TextSoft), (label.FontSize, label.Foreground));
            Assert.Equal(30, rowBox.Height);
        });
    }

    [Fact]
    public void Each_field_starts_with_an_18_pixel_tag_in_its_axis_colour_at_12_percent()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var row = Selected(window);
            Color[] axes = [EditorColors.AxisX, EditorColors.AxisY, EditorColors.AxisZ];

            for (var i = 0; i < 3; i++)
            {
                var field = BoxOf(Fields(row)[i], window);
                var tag = TagOf(Fields(row)[i]);
                var letter = (TextBlock)tag.Child!;

                AssertBox(field.X + 1, field.Y + 1, 18, 26, BoxOf(tag, window));
                Assert.Equal(Color.FromArgb(0x1F, axes[i].R, axes[i].G, axes[i].B), ((ISolidColorBrush)tag.Background!).Color);
                Assert.Equal((new[] { "X", "Y", "Z" }[i], axes[i], FontWeight.Bold), (letter.Text, ((ISolidColorBrush)letter.Foreground!).Color, letter.FontWeight));
                Assert.Equal(BoxOf(tag, window).Right, BoxOf(row.Axes[i], window).X);
                Assert.Equal(6, row.Axes[i].Padding.Left);
            }
        });
    }

    [Fact]
    public void The_unit_is_dim_and_ends_7_pixels_inside_its_field()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var row = Selected(window);
            var units = Texts(row).Where(text => text.Text == "m").ToArray();

            Assert.Equal(3, units.Length);
            for (var i = 0; i < 3; i++)
            {
                Assert.Equal(BoxOf(Fields(row)[i], window).Right - 1 - 7, BoxOf(units[i], window).Right);
                Assert.Equal(BoxOf(units[i], window).X, BoxOf(row.Axes[i], window).Right);
                Assert.Equal(EditorColors.Unit, ((ISolidColorBrush)units[i].Foreground!).Color);
            }
        });
    }

    [Fact]
    public void A_long_value_stays_inside_its_field()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var row = Selected(window);
            var before = Fields(row).Select(field => BoxOf(field, window)).ToArray();

            row.Show(new System.Numerics.Vector3(123456789.125f, -0.000123456f, 98765.4321f));
            window.UpdateLayout();

            Assert.Equal(before, Fields(row).Select(field => BoxOf(field, window)));
            Assert.All(Enumerable.Range(0, 3), i => Assert.True(BoxOf(row.Axes[i], window).Right <= BoxOf(Fields(row)[i], window).Right));
        });
    }

    [Fact]
    public void A_field_lights_its_edge_while_its_box_has_focus()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var row = Selected(window);

            row.Axes[1].Focus();
            window.UpdateLayout();

            Assert.Equal([EditorBrushes.Line, EditorBrushes.Accent, EditorBrushes.Line], Fields(row).Select(field => field.BorderBrush));
            Assert.Equal(new Thickness(0), row.Axes[1].GetVisualDescendants().OfType<Border>().First(border => border.Name == "PART_BorderElement").BorderThickness);
        });
    }

    [Fact]
    public void Rotation_counts_in_degrees_and_scale_has_no_unit()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            Selected(window);

            Assert.Equal(["°", "°", "°"], Texts(window.Inspector.Rotation).Where(text => text.Text == "°").Select(text => text.Text));
            Assert.DoesNotContain(Texts(window.Inspector.Scale), text => text.Text is "m" or "°");
            Assert.Equal("Rotation", window.Inspector.Rotation.Label);
        });
    }

    [Fact]
    public void A_field_rounds_its_value_at_rest_and_shows_it_in_full_with_the_caret()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var row = Selected(window);

            row.Show(new System.Numerics.Vector3(0.99960005f, -29.75703f, 2));
            Assert.Equal(["0.9996", "-29.757", "2"], row.Axes.Select(box => box.Text));

            row.Axes[0].Focus();
            Assert.Equal("0.99960005", row.Axes[0].Text);

            row.Axes[1].Focus();
            Assert.Equal(["0.9996", "-29.75703"], row.Axes.Take(2).Select(box => box.Text));
        });
    }

    [Fact]
    public void Leaving_a_field_unchanged_writes_nothing_though_the_others_are_rounded()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            var row = Selected(window);
            row.Show(new System.Numerics.Vector3(0.99960005f, 2, 3));
            var edited = 0;
            row.Entered += _ => edited++;

            row.Axes[1].Focus();
            row.Axes[2].Focus();

            Assert.Equal(0, edited);
        });
    }

    [Fact]
    public void Bad_text_stays_with_a_red_edge_and_the_scene_keeps_its_value()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            var row = Selected(window);

            row.Axes[1].Text = "1.0.2";
            Enter(row.Axes[1]);

            Assert.Equal(new System.Numerics.Vector3(1, 2, 3), scene.PositionOf(FakeScene.Lamp.Handle));
            Assert.Equal("1.0.2", row.Axes[1].Text);
            Assert.True(row.Fault.IsVisible);
            Assert.Equal("Type a number.", row.Fault.Text);
            Assert.Equal(EditorBrushes.FaultText, row.Fault.Foreground);
            Assert.Equal([EditorBrushes.Line, EditorBrushes.Fault, EditorBrushes.Line], row.Fields.Select(field => field.BorderBrush));
        });
    }

    [Fact]
    public void A_good_number_clears_the_fault()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            var row = Selected(window);
            row.Axes[1].Text = "high";
            Enter(row.Axes[1]);

            row.Axes[1].Text = "7";
            Enter(row.Axes[1]);

            Assert.Equal(new System.Numerics.Vector3(1, 7, 3), scene.PositionOf(FakeScene.Lamp.Handle));
            Assert.False(row.Fault.IsVisible);
            Assert.All(row.Fields, field => Assert.DoesNotContain("bad", field.Classes));
        });
    }

    static void Enter(TextBox box) => box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
}

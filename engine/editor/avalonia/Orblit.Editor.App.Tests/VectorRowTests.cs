using Avalonia.Controls;
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

    [Fact]
    public void A_row_is_a_label_over_a_dark_field_28_high()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var row = Selected(window);
            var rowBox = BoxOf(row, window);
            var label = Texts(row).Single(text => text.Text == "Position");
            var field = row.GetVisualDescendants().OfType<Border>().Single(border => border.Background == EditorBrushes.Field);

            AssertBox(rowBox.X, rowBox.Y + 30, rowBox.Width, 28, BoxOf(field, window));
            Assert.Equal(new Avalonia.CornerRadius(4), field.CornerRadius);
            Assert.Equal(rowBox.X + 8, BoxOf(label, window).X);
            Assert.Equal(59, rowBox.Height);
        });
    }

    [Fact]
    public void The_field_has_three_cells_with_the_letter_8_in_and_the_value_28_in()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var row = Selected(window);
            var field = BoxOf(row.GetVisualDescendants().OfType<Border>().Single(border => border.Background == EditorBrushes.Field), window);
            var letters = Texts(row).Where(text => text.Text is "x" or "y" or "z").ToArray();

            Assert.Equal(["x", "y", "z"], letters.Select(text => text.Text));
            for (var i = 0; i < 3; i++)
            {
                var cellX = field.X + field.Width / 3 * i;
                Near(cellX + 8, BoxOf(letters[i], window).X + letters[i].Padding.Left);
                Near(cellX + 28, BoxOf(row.Axes[i], window).X);
            }
        });
    }

    [Fact]
    public void The_letters_are_red_green_and_blue_and_the_unit_is_dim()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var row = Selected(window);

            var colours = Texts(row).Where(text => text.Text is "x" or "y" or "z").Select(text => ((ISolidColorBrush)text.Foreground!).Color).ToArray();
            var unit = Texts(row).First(text => text.Text == "m");

            Assert.Equal([Color.Parse("#7b3d49"), Color.Parse("#6f8341"), Color.Parse("#436488")], colours);
            Assert.Equal(Color.Parse("#606060"), ((ISolidColorBrush)unit.Foreground!).Color);
        });
    }

    [Fact]
    public void The_unit_follows_the_value_7_pixels_after_it()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var row = Selected(window);
            var units = Texts(row).Where(text => text.Text == "m").ToArray();

            Assert.Equal(3, units.Length);
            for (var i = 0; i < 3; i++)
            {
                Assert.Equal(BoxOf(row.Axes[i], window).Right + 7, BoxOf(units[i], window).X);
            }
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
}

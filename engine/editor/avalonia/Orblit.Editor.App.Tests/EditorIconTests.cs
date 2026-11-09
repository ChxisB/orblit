using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Tests;

public sealed class EditorIconTests
{
    static IEnumerable<KeyValuePair<string, IconDrawing>> All() =>
        (Dictionary<string, IconDrawing>)typeof(EditorIcons).GetField("s_icons", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    [Fact]
    public void Icons_come_from_the_icon_folder()
    {
        Assert.True(All().Count() > 1000);
    }

    [Fact]
    public void An_icon_is_found_by_name_and_an_unknown_name_is_not()
    {
        Assert.True(EditorIcons.Has("Add"));
        Assert.False(EditorIcons.Has("NoSuchIcon"));
    }

    [Fact]
    public void A_themed_icon_is_one_filled_path_in_a_sixteen_box()
    {
        var add = EditorIcons.Get("Add");

        Assert.Equal((16, 16), (add.Width, add.Height));
        var shape = Assert.Single(add.Shapes);
        Assert.Equal(IconShape.Themed, shape.Fill);
        Assert.Equal(0u, shape.Stroke);
        Assert.Equal((1d, PenLineCap.Flat, PenLineJoin.Miter, Matrix.Identity), (shape.StrokeWidth, shape.Cap, shape.Join, shape.Transform));
    }

    [Fact]
    public void A_coloured_icon_keeps_its_stroke_width_and_colour()
    {
        var stroke = EditorIcons.Get("2DNodes").Shapes[0];

        Assert.Equal(0xFF6393FFu, stroke.Stroke);
        Assert.Equal(2, stroke.StrokeWidth);
    }

    [Fact]
    public void A_transform_reaches_the_shape_as_a_matrix()
    {
        var moved = All().SelectMany(pair => pair.Value.Shapes).First(shape => shape.Transform != Matrix.Identity);

        Assert.NotEqual(Matrix.Identity, moved.Transform);
    }

    [Fact]
    public void A_clipped_use_reaches_the_shape_as_a_clip_geometry()
    {
        var clipped = EditorIcons.Get("ToolTransform").Shapes.Where(shape => shape.Clip is not null).ToArray();

        Assert.Equal(2, clipped.Length);
        Assert.All(clipped, shape => Assert.NotNull(shape.ClipGeometry));
        Assert.Null(EditorIcons.Get("Add").Shapes[0].ClipGeometry);
    }

    [Fact]
    public void An_unknown_name_is_refused_with_the_name()
    {
        var error = Assert.Throws<ArgumentException>(() => EditorIcons.Get("NoSuchIcon"));

        Assert.Contains("NoSuchIcon", error.Message);
    }

    [Fact]
    public void Every_path_of_every_icon_parses()
    {
        UiThread.Run(() =>
        {
            foreach (var (name, drawing) in All())
            {
                foreach (var shape in drawing.Shapes)
                {
                    Assert.True(shape.Geometry is not null, name);
                    Assert.True(shape.Clip is null || shape.ClipGeometry is not null, name);
                }
            }
        });
    }

    [Fact]
    public void The_control_is_sixteen_square_and_draws_in_a_window()
    {
        UiThread.Run(() =>
        {
            var icon = new EditorIcon("2DNodes");
            icon.Measure(new Size(100, 100));
            var window = new Window { Content = icon };
            window.Show();

            Assert.Equal(new Size(16, 16), icon.DesiredSize);
            window.Close();
        });
    }

    [Fact]
    public void A_control_for_an_unknown_icon_is_refused_at_build()
    {
        Assert.Throws<ArgumentException>(() => new EditorIcon("NoSuchIcon"));
    }
}

using Avalonia.Media;
using Orblit.Editor.App.Graph;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Tests.Graph;

public sealed class PinColorsTests
{
    static Color Of(GraphPortSort sort, int type) => ((ISolidColorBrush)PinColors.Of(new GraphPort("pin", sort, type, "", false))).Color;

    [Theory]
    [InlineData(GraphPortSort.Flow, 0, "#E8EBF0")]
    [InlineData(GraphPortSort.Data, 3, "#7FD4C1")]
    [InlineData(GraphPortSort.Data, 4, "#F0A365")]
    [InlineData(GraphPortSort.Data, 24, "#8DB8FF")]
    [InlineData(GraphPortSort.Data, 30, "#8DB8FF")]
    [InlineData(GraphPortSort.Data, 2, "#A6D86B")]
    [InlineData(GraphPortSort.Data, 21, "#E3D27A")]
    public void A_pin_takes_the_canvas_tint_of_its_type(GraphPortSort sort, int type, string tint)
    {
        Assert.Equal(Color.Parse(tint), Of(sort, type));
    }

    [Fact]
    public void A_flow_pin_is_white_whatever_its_type()
    {
        Assert.Equal(Of(GraphPortSort.Flow, 0), Of(GraphPortSort.Flow, 4));
    }
}

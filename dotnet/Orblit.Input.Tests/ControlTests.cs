using Orblit.Json;

namespace Orblit.Input.Tests;

public class ControlTests
{
    [Fact]
    public void ItIsTheSameControlWhenItIsTheSameKindAndName()
    {
        Assert.Equal<Control>(new KeyControl("A"), new KeyControl("A"));
        Assert.Equal(new KeyControl("A").GetHashCode(), new KeyControl("A").GetHashCode());
        Assert.NotEqual<Control>(new KeyControl("A"), new TouchControl("A"));
        Assert.NotEqual<Control>(new PadAxisControl(PadAxis.LeftX), new PadAxisControl(PadAxis.LeftY));
    }

    [Fact]
    public void ItGoesThroughJsonAndComesBack()
    {
        Control[] controls =
        [
            new KeyControl("KeyW"),
            new PadButtonControl(PadButton.DpadLeft),
            new PadAxisControl(PadAxis.RightTrigger),
            new PadAxisControl(PadAxis.LeftX, AxisSide.Positive),
            new PadAxisControl(PadAxis.LeftX, AxisSide.Negative),
            new PadStickControl(PadStick.Right),
            new TouchControl("fire"),
        ];

        foreach (var control in controls)
        {
            Assert.Equal(control, Control.FromJson(control.ToJson()));
        }
    }

    [Fact]
    public void ItTellsTheHalvesOfAnAxisApart()
    {
        var both = new PadAxisControl(PadAxis.LeftX);
        var positive = new PadAxisControl(PadAxis.LeftX, AxisSide.Positive);
        var negative = new PadAxisControl(PadAxis.LeftX, AxisSide.Negative);

        Assert.Equal(3, new HashSet<Control> { both, positive, negative }.Count);
        Assert.Equal<Control>(positive, new PadAxisControl(PadAxis.LeftX, AxisSide.Positive));
        Assert.Equal("""{"padAxisPositive":"leftX"}""", JsonText.Encode(positive.ToJson()));
        Assert.Equal("""{"padAxisNegative":"leftX"}""", JsonText.Encode(negative.ToJson()));
        Assert.Equal("""{"padAxis":"leftX"}""", JsonText.Encode(both.ToJson()));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"KeyW\"")]
    [InlineData("{}")]
    [InlineData("""{"key":1}""")]
    [InlineData("""{"key":"A","touch":"B"}""")]
    [InlineData("""{"padButton":"nope"}""")]
    [InlineData("""{"padAxis":"nope"}""")]
    [InlineData("""{"padAxisPositive":"nope"}""")]
    [InlineData("""{"padAxisNegative":3}""")]
    [InlineData("""{"padStick":"nope"}""")]
    [InlineData("""{"wat":"KeyW"}""")]
    public void ItRejectsJsonThatIsNotAControl(string json)
    {
        Assert.Throws<FormatException>(() => Control.FromJson(JsonText.Decode(json)));
    }
}

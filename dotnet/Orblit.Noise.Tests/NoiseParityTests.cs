using Orblit.Parity;

namespace Orblit.Noise.Tests;

public class NoiseParityTests
{
    [Theory]
    [MemberData(nameof(Fields.Names), MemberType = typeof(Fields))]
    public void MatchesTheDartTwin(string name)
    {
        var field = Fields.All[name];
        var fixture = Fixture.Load("orblit_noise", "noise");
        var checkedCases = 0;

        foreach (var c in fixture.Cases.Where(c => c.GetProperty("field").GetString() == name))
        {
            var at = c.GetProperty("at");
            var expected = c.GetProperty("expect").GetDouble();
            var actual = field.At(at[0].GetDouble(), at[1].GetDouble(), at[2].GetDouble());
            Assert.True(
                Tolerance.Close(expected, actual),
                $"{name} at {at}: Dart {expected:R}, C# {actual:R}, from Dart commit {fixture.DartCommit}");
            checkedCases++;
        }

        Assert.True(checkedCases > 0, $"the fixture has no cases for {name}");
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void ANonFiniteCoordinateIsRefusedAsDartRefusesIt(double bad) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Fields.Gradient.At(bad));
}

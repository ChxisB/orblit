namespace Orblit.Noise.Tests;

public class EveryFieldTests
{
    /// <summary>Samples a field over a grid and reports what it found.</summary>
    internal static (double Low, double High) Survey(NoiseField field, double step = 0.37, int count = 40)
    {
        var low = double.PositiveInfinity;
        var high = double.NegativeInfinity;
        for (var i = 0; i < count; i++)
        {
            for (var j = 0; j < count; j++)
            {
                var v = field.At(i * step, j * step, 1.5);
                low = Math.Min(low, v);
                high = Math.Max(high, v);
            }
        }

        return (low, high);
    }

    [Theory]
    [MemberData(nameof(Fields.Names), MemberType = typeof(Fields))]
    public void GivesTheSameAnswerTwice(string name)
    {
        // The whole contract. A field that drifted would be a world that
        // regenerated differently after an upgrade.
        var field = Fields.All[name];
        Assert.Equal(field.At(1.5, 2.25, 3.125), field.At(1.5, 2.25, 3.125));
    }

    [Theory]
    [MemberData(nameof(Fields.Names), MemberType = typeof(Fields))]
    public void StaysInsideMinusOneAndOne(string name)
    {
        var (low, high) = Survey(Fields.All[name]);
        Assert.True(low >= -1.0000001, $"{name} went to {low}");
        Assert.True(high <= 1.0000001, $"{name} went to {high}");
    }

    [Theory]
    [MemberData(nameof(Fields.Names), MemberType = typeof(Fields))]
    public void IsSmoothASmallStepIsASmallChange(string name)
    {
        // What separates noise from static. A random number per sample would
        // swing the full range between neighbouring samples.
        var field = Fields.All[name];
        var worst = 0.0;
        for (var i = 0; i < 300; i++)
        {
            var x = i * 0.01;
            worst = Math.Max(worst, Math.Abs(field.At(x + 0.01, 4.0) - field.At(x, 4.0)));
        }

        Assert.True(worst < 0.35, $"{name} jumps");
    }

    [Theory]
    [MemberData(nameof(Fields.Names), MemberType = typeof(Fields))]
    public void UsesTheWholeRangeRatherThanSittingNearNought(string name)
    {
        var (low, high) = Survey(Fields.All[name]);
        Assert.True(high - low > 0.5, $"{name} is flat");
    }

    [Theory]
    [MemberData(nameof(Fields.Names), MemberType = typeof(Fields))]
    public void HasAUnitFormThatRunsNoughtToOne(string name)
    {
        var field = Fields.All[name];
        var u = field.Unit(2.5, 1.25);
        Assert.InRange(u, 0.0, 1.0);
        Assert.Equal(field.At(2.5, 1.25) * 0.5 + 0.5, u, 1e-12);
    }
}

public class WhatEachOneIsForTests
{
    private static readonly GradientNoise Gradient = Fields.Gradient;

    [Fact]
    public void ADifferentSeedIsADifferentField()
    {
        // Sampled off the lattice and off the axis planes. On them the dot
        // product collapses: with two coordinates whole, twelve gradients give
        // only three possible answers, and two seeds agreeing is ordinary
        // rather than suspicious.
        var other = new GradientNoise(8);
        var same = 0;
        for (var i = 0; i < 50; i++)
        {
            var x = i * 0.37 + 0.13;
            if (Gradient.At(x, 1.7, 0.43) == other.At(x, 1.7, 0.43))
            {
                same++;
            }
        }

        Assert.Equal(0, same);
    }

    [Fact]
    public void GradientNoiseIsNoughtOnTheLatticeValueNoiseIsNot()
    {
        // The whole reason to prefer it: the extremes of value noise sit on the
        // grid, so the grid is visible in the field.
        foreach (var at in new[] { 0.0, 1.0, 2.0, -3.0 })
        {
            Assert.True(Math.Abs(Gradient.At(at, at, at)) < 1e-9);
        }

        var onLattice = Enumerable.Range(0, 8).Select(i => Math.Abs(Fields.Value.At(i, 0, 0)));
        Assert.Contains(onLattice, v => v > 0.1);
    }

    [Fact]
    public void AFractalHasFinerDetailThanTheFieldItIsBuiltFrom()
    {
        // More octaves means more happening between two nearby samples.
        static double Roughness(NoiseField field)
        {
            var total = 0.0;
            for (var i = 0; i < 200; i++)
            {
                total += Math.Abs(field.At(i * 0.02, 2.0) - field.At(i * 0.02 + 0.02, 2.0));
            }

            return total;
        }

        Assert.True(Roughness(new FractalNoise(Gradient, 6)) > Roughness(new FractalNoise(Gradient, 1)));
    }

    [Fact]
    public void OneOctaveOfAFractalIsTheFieldItself()
    {
        var one = new FractalNoise(Gradient, 1);
        Assert.Equal(Gradient.At(1.3, 2.7, 0.4), one.At(1.3, 2.7, 0.4), 1e-12);
    }

    [Fact]
    public void RidgedNoiseCreasesWhereTheFieldCrossesNought()
    {
        // A crease rather than a smooth pass through: the peak is where the
        // field was zero.
        Assert.Equal(1, new RidgedNoise(Gradient).At(1.0, 1.0, 1.0), 1e-9);
    }

    [Fact]
    public void TilingNoiseJoinsUpWithItself()
    {
        // The point of it. Sampled a period apart it is the same field, so a
        // texture laid end to end has no seam.
        var tiling = new TilingNoise(Gradient, 8);
        foreach (var at in new[] { 0.0, 1.25, 3.5, 6.75 })
        {
            Assert.Equal(tiling.At(at, 2.5), tiling.At(at + 8, 2.5), 1e-12);
            Assert.Equal(tiling.At(2.5, at), tiling.At(2.5, at + 8), 1e-12);
        }
    }

    [Fact]
    public void TilingNoiseStillVariesInsideAPeriod()
    {
        // A field that wrapped by going flat would also "join up".
        var (low, high) = EveryFieldTests.Survey(new TilingNoise(Gradient, 8), step: 0.25);
        Assert.True(high - low > 0.5);
    }

    [Fact]
    public void CellNoiseIsNoughtAtACellPointAndRisesAwayFromIt()
    {
        // Sampled densely, the lowest value found is near the bottom of the
        // range, which is a point, and the field climbs away from it.
        var (low, high) = EveryFieldTests.Survey(new CellNoise(3), step: 0.11, count: 60);
        Assert.True(low < -0.5);
        Assert.True(high > 0);
    }
}

public class AwkwardArgumentsTests
{
    [Fact]
    public void NegativeCoordinatesWorkTheWayPositiveOnesDo()
    {
        var gradient = Fields.Gradient;
        Assert.Equal(gradient.At(-3.25, -1.5, -0.75), gradient.At(-3.25, -1.5, -0.75));
        Assert.True(Math.Abs(gradient.At(-3.25, -1.5)) <= 1);
    }

    [Fact]
    public void APeriodOfNothingDoesNotDivideByIt() =>
        Assert.True(double.IsFinite(new TilingNoise(Fields.Gradient, 0).At(1.5, 2.5)));

    [Fact]
    public void NoOctavesIsSilenceRatherThanADivisionByNought() =>
        Assert.Equal(0, new FractalNoise(Fields.Gradient, 0).At(1.5, 2.5));
}

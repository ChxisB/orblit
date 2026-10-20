using System.Diagnostics;

namespace Orblit.Weather.Tests;

// What C# does that Dart cannot be asked: an enum takes any number, and a record compares by value.
public class AddedTests
{
    [Fact]
    public void RefusesALabelForABodyThatIsNotOneOfTheTwo() =>
        Assert.Throws<UnreachableException>(() => ((CelestialBody)99).Label());

    [Fact]
    public void RefusesALabelForAConditionThatIsNotOneOfTheEight() =>
        Assert.Throws<UnreachableException>(() => ((WeatherCondition)99).Label());

    [Fact]
    public void RefusesALabelForACloudKindThatIsNotOneOfTheSix() =>
        Assert.Throws<UnreachableException>(() => ((CloudKind)99).Label());

    [Fact]
    public void RefusesACloudForAConditionThatIsNotOneOfTheEight() =>
        Assert.Throws<UnreachableException>(() => CloudKindExtensions.ForCondition((WeatherCondition)99));

    [Fact]
    public void FallsBackToClearForAConditionThatIsNotOneOfTheEight() =>
        Assert.Same(WeatherState.Presets[WeatherCondition.Clear], WeatherState.Of((WeatherCondition)99));

    [Fact]
    public void HasAPresetForEveryCondition()
    {
        foreach (var condition in Enum.GetValues<WeatherCondition>())
        {
            Assert.Same(WeatherState.Presets[condition], WeatherState.Of(condition));
        }
    }

    [Fact]
    public void ChangesOneThingAboutAWeatherWithWith()
    {
        var clear = WeatherState.Of(WeatherCondition.Clear);
        var wet = clear with { Rain = 0.5 };

        Assert.True(wet.IsWet);
        Assert.False(clear.IsWet);
        Assert.Equal(clear.FogColour, wet.FogColour);
        Assert.Equal(clear, wet with { Rain = 0 });
    }

    [Fact]
    public void GivesTheNothingStrikeWhenThereIsNoStorm()
    {
        Assert.Same(Strike.None, Strike.At(5, 0));
        Assert.Same(Strike.None, Strike.At(-1, 0.6));
    }
}

using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Weather.Tests;

public class SkyTests
{
    // the day

    [Fact]
    public void TheSunIsUpAtNoonAndTheMoonAtMidnight()
    {
        Assert.Equal(CelestialBody.Sun, DayCycle.At(12).Body);
        Assert.Equal(CelestialBody.Moon, DayCycle.At(0).Body);
        Assert.Equal(CelestialBody.Moon, DayCycle.At(23).Body);
    }

    [Fact]
    public void ItComesFromOverheadAtNoon() =>
        // Light travelling downwards, which is what a sun overhead does.
        Assert.True(DayCycle.At(12).Direction.Y < -0.85);

    [Fact]
    public void ItRisesOnOneSideAndSetsOnTheOther()
    {
        var morning = DayCycle.At(8).Direction;
        var evening = DayCycle.At(16).Direction;

        // A sun that went back the way it came would light both ends of a day
        // identically.
        Assert.NotEqual(Doubles.Sign(morning.X), Doubles.Sign(evening.X));
    }

    [Fact]
    public void TheMoonIsOppositeWhereTheSunWouldHaveBeen()
    {
        // Two in the morning, and the sun eight hours later.
        var moon = DayCycle.At(2).Direction;
        var sun = DayCycle.At(10).Direction;

        Assert.NotEqual(Doubles.Sign(moon.X), Doubles.Sign(sun.X));
        Assert.True(moon.Y < 0);
    }

    [Fact]
    public void TheLightGoesWarmAtTheHorizonAndWhiteOverhead()
    {
        var dawn = DayCycle.At(6.5).LightColour;
        var noon = DayCycle.At(12).LightColour;

        static double Warmth(Tint colour) => colour.Red - colour.Blue;
        Assert.True(Warmth(dawn) > Warmth(noon));
    }

    [Fact]
    public void TheSkyIsDarkerAtNightThanAtNoon()
    {
        static double Lightness(Tint colour) => colour.Red + colour.Green + colour.Blue;
        Assert.True(Lightness(DayCycle.At(1).SkyColour) < Lightness(DayCycle.At(12).SkyColour));
    }

    [Fact]
    public void ANightIsManyStopsDarkerThanADay() =>
        // About seventeen stops, which is why the exposure has to move.
        Assert.True(DayCycle.At(12).Power > DayCycle.At(0).Power * 10000);

    [Fact]
    public void TheCameraOpensUpForTheNightAndStopsDownForTheDay()
    {
        var night = DayCycle.At(0).Exposure;
        var noon = DayCycle.At(12).Exposure;

        Assert.True(night.Aperture < noon.Aperture);
        Assert.True(night.ShutterSpeed > noon.ShutterSpeed);
        Assert.True(night.Sensitivity > noon.Sensitivity);
    }

    [Fact]
    public void NoonMetersWhereACenturyOfFilmBoxesPutIt()
    {
        var noon = DayCycle.At(12).Exposure;

        // Sunny sixteen is EV 15 at ISO 100, and a meter that lands anywhere
        // else has its calibration wrong.
        Assert.Equal(15, noon.Ev100, 0.5);
        Assert.Equal(100.0, noon.Sensitivity);
    }

    [Fact]
    public void NoHourOfTheDayComesOutBlownOutOrBlack()
    {
        // A cycle whose light levels and exposure are authored apart will
        // disagree somewhere. Metering off the light that falls on the scene
        // stops that, and this proves it at every hour.
        for (var minute = 0; minute < 24 * 60; minute += 5)
        {
            var sky = DayCycle.At(minute / 60.0);

            var incident = (sky.Power * 683 * Math.Max(0, Math.Sin(sky.Altitude))) + sky.Ambient;
            // What Filament does with the three numbers, and then what a
            // mid-grey surface facing the light comes out as.
            var exposure = 1 / (1.2 * Math.Pow(2, sky.Exposure.Ev100));
            var grey = incident * 0.5 / Math.PI * exposure;

            Assert.True(
                grey is >= 0.05 and <= 0.6,
                $"at {minute / 60}:{(minute % 60).ToString().PadLeft(2, '0')} a mid-grey surface comes out at {grey}");
        }
    }

    [Fact]
    public void TheLightAndTheSkyRiseAndFallTogether()
    {
        // The sky is lit by the same body everything else is, so the two cannot
        // be authored apart without the shadows going the wrong depth.
        foreach (var hour in new[] { 7.0, 9.0, 12.0, 15.0, 17.0 })
        {
            var sky = DayCycle.At(hour);
            Assert.Equal(sky.Power * 683 * DayCycle.SkyShare, sky.Ambient, 1);
        }
    }

    [Fact]
    public void AnHourPastTheEndOfTheDayIsAnHourIntoTheNext()
    {
        Assert.Equal(DayCycle.At(1).Body, DayCycle.At(25).Body);
        Assert.Equal(DayCycle.At(1).Power, DayCycle.At(25).Power);
    }

    [Fact]
    public void EachBodyCrossesTheSkySmoothly()
    {
        // Walked a minute at a time through the sun's half of the day and then
        // the moon's. A body that teleported would read as a light being
        // switched on somewhere else.
        foreach (var (start, end) in new[] { (6.2, 17.8), (18.2, 29.8) })
        {
            var previous = DayCycle.At(start);
            for (var minute = 1; minute < (end - start) * 60; minute++)
            {
                var now = DayCycle.At(start + (minute / 60.0));
                Assert.True(
                    (now.Direction - previous.Direction).Length < 0.01,
                    $"jumped at {start + (minute / 60.0)} hours");
                previous = now;
            }
        }
    }

    [Fact]
    public void TheSwapAtTheHorizonHappensInTheDark()
    {
        // The one place the direction does jump. It is allowed because the sun
        // has faded to nothing by the time it reaches the horizon.
        foreach (var hour in new[] { 6.0, 18.0 })
        {
            Assert.True(DayCycle.At(hour - 0.01).Power < 0.05);
            Assert.True(DayCycle.At(hour + 0.01).Power < 0.05);
        }
    }

    [Fact]
    public void EverythingThatCanBeSeenChangesSmoothlyAcrossDawn()
    {
        var before = DayCycle.At(5.99);
        var after = DayCycle.At(6.01);

        static double Gap(Tint a, Tint b) =>
            Math.Abs(a.Red - b.Red) + Math.Abs(a.Green - b.Green) + Math.Abs(a.Blue - b.Blue);

        Assert.True(Gap(before.SkyColour, after.SkyColour) < 0.02);
        Assert.True(Math.Abs(before.Exposure.Sensitivity - after.Exposure.Sensitivity) < 50);
        Assert.True(Math.Abs(before.Ambient - after.Ambient) < 200);
    }
}

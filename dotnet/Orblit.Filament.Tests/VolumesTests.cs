using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

public sealed class VolumesTests
{
    private const double Tight = 1e-12;

    private static RenderScene Courtyard(
        IReadOnlyList<EnvironmentVolume>? volumes = null,
        Vector3? camera = null,
        RenderEnvironment? environment = null) =>
        new()
        {
            Objects = [],
            Camera = new RenderCamera { Position = camera ?? new Vector3(0, 0, 20), Target = Vector3.Zero() },
            Sky = new Sky { Colour = new Vector3(0.6, 0.7, 0.9), Ambient = 20000 },
            Fog = new Fog { Density = 0.002, Colour = new Vector3(0.7, 0.8, 0.9) },
            Environment = environment ?? RenderEnvironment.None,
            Volumes = volumes ?? [],
        };

    // A hall ten metres square round the origin, with a two metre doorway's worth of blend outside it.
    private static EnvironmentVolume Hall(
        EnvironmentOverrides? overrides = null,
        int priority = 0,
        long key = 1,
        double weight = 1) =>
        new()
        {
            Key = key,
            Shape = VolumeShape.Box,
            Centre = Vector3.Zero(),
            HalfExtents = Vector3.All(5),
            BlendDistance = 2,
            Priority = priority,
            Weight = weight,
            Overrides = overrides ?? new EnvironmentOverrides
            {
                FogDensity = 0.1,
                FogColour = new Vector3(0.4, 0.3, 0.2),
                Ambient = 2000,
                ExposureCompensation = -1,
            },
        };

    private static EnvironmentVolume Ball(double radius, double blend) =>
        EnvironmentVolume.Sphere(1, Vector3.Zero(), radius, new EnvironmentOverrides()) with { BlendDistance = blend };

    [Fact]
    public void FullInsideNothingPastTheBlendASmoothstepBetween()
    {
        var sphere = Ball(3, 2);
        Assert.Equal(1, sphere.InfluenceAt(Vector3.Zero()));
        Assert.Equal(1, sphere.InfluenceAt(new Vector3(3, 0, 0)));
        Assert.Equal(0.5, sphere.InfluenceAt(new Vector3(4, 0, 0)), Tight);
        Assert.Equal(0, sphere.InfluenceAt(new Vector3(5, 0, 0)));
        Assert.Equal(0, sphere.InfluenceAt(new Vector3(9, 0, 0)));

        // A quarter of the way into the band: 1 - smoothstep(0.25).
        Assert.Equal(0.84375, sphere.InfluenceAt(new Vector3(3.5, 0, 0)), Tight);
    }

    [Fact]
    public void TheWeightScalesAllOfIt()
    {
        var half = Hall(weight: 0.5);
        Assert.Equal(0.5, half.InfluenceAt(Vector3.Zero()));
        Assert.Equal(0.25, half.InfluenceAt(new Vector3(6, 0, 0)), Tight);
    }

    [Fact]
    public void AHardEdgeIsAllOrNothing()
    {
        var hard = Ball(1, 0);
        Assert.Equal(1, hard.InfluenceAt(new Vector3(0.99, 0, 0)));
        Assert.Equal(0, hard.InfluenceAt(new Vector3(1.01, 0, 0)));
    }

    [Fact]
    public void ABoxMeasuresFromItsSurfaceCornersIncluded()
    {
        var box = Hall();
        Assert.Equal(0, box.DistanceTo(new Vector3(4, 4, 4)));
        Assert.Equal(1, box.DistanceTo(new Vector3(6, 0, 0)), Tight);

        // Past a corner the distance is to the corner and not to either face.
        Assert.Equal(Math.Sqrt(2), box.DistanceTo(new Vector3(6, 6, 0)), Tight);
    }

    [Fact]
    public void ATurnedBoxIsTurned()
    {
        // Long along x, then turned a quarter about the vertical so it is long along z instead.
        var box = EnvironmentVolume.Box(1, Vector3.Zero(), new Vector3(10, 1, 1), new EnvironmentOverrides()) with
        {
            Rotation = Quaternion.AxisAngle(new Vector3(0, 1, 0), Math.PI / 2),
            BlendDistance = 0,
        };
        Assert.Equal(1, box.InfluenceAt(new Vector3(0, 0, 8)));
        Assert.Equal(0, box.InfluenceAt(new Vector3(8, 0, 0)));
    }

    [Fact]
    public void ABoxTurnedPartwayTurnsTheWayAModelWould()
    {
        // A quarter turn lands a box on itself whichever way it goes and an eighth does not.
        // Turning is right handed, as a model's transform is, so +x swings towards -z.
        var box = EnvironmentVolume.Box(1, Vector3.Zero(), new Vector3(10, 1, 1), new EnvironmentOverrides()) with
        {
            Rotation = Quaternion.AxisAngle(new Vector3(0, 1, 0), Math.PI / 4),
            BlendDistance = 0,
        };
        Assert.Equal(1, box.InfluenceAt(new Vector3(5, 0, -5)));
        Assert.Equal(0, box.InfluenceAt(new Vector3(5, 0, 5)));
    }

    [Fact]
    public void OutsideEveryVolumeTheSceneIsTheOneTheHostBuilt()
    {
        var scene = Courtyard([Hall()], new Vector3(0, 0, 20));
        var seen = scene.Resolved();

        // The same objects and not equal copies: nothing downstream should see anything move.
        Assert.Same(scene.Fog, seen.Fog);
        Assert.Same(scene.Sky, seen.Sky);
        Assert.Same(scene.Post, seen.Post);
        Assert.Same(scene.Camera, seen.Camera);
        Assert.Empty(seen.Volumes);
    }

    [Fact]
    public void InsideWhatTheVolumeNamesIsWhatItSays()
    {
        var seen = Courtyard([Hall()], new Vector3(1, 1, 1)).Resolved();
        Assert.Equal(0.1, seen.Fog.Density, Tight);
        Assert.Equal(new Vector3(0.4, 0.3, 0.2), seen.Fog.Colour);
        Assert.Equal(2000, seen.Sky.Ambient, 1e-9);
        Assert.Equal(1.0 / 250, seen.Camera.ShutterSpeed, Tight);
    }

    [Fact]
    public void WhatTheVolumeDoesNotNameIsLeftExactlyAlone()
    {
        var scene = Courtyard([Hall(new EnvironmentOverrides())], Vector3.Zero());
        var seen = scene.Resolved();
        Assert.Same(scene.Fog, seen.Fog);
        Assert.Same(scene.Sky, seen.Sky);

        // Only the fog, and only its density.
        var fogOnly = Courtyard([Hall(new EnvironmentOverrides { FogDensity = 0.3 })], Vector3.Zero());
        var fogged = fogOnly.Resolved();
        Assert.Equal(0.3, fogged.Fog.Density);
        Assert.Equal(fogOnly.Fog.Colour, fogged.Fog.Colour);
        Assert.Equal(fogOnly.Fog.CutOffDistance, fogged.Fog.CutOffDistance);
        Assert.Same(fogOnly.Sky, fogged.Sky);
        Assert.Same(fogOnly.Post, fogged.Post);
        Assert.Equal(fogOnly.Camera.ShutterSpeed, fogged.Camera.ShutterSpeed);
    }

    [Fact]
    public void InTheBandEachKindOfNumberBlendsInItsOwnSpace()
    {
        // Halfway through the band, where the smoothstep is a half.
        var seen = Courtyard([Hall()], new Vector3(6, 0, 0)).Resolved();
        Assert.Equal((0.002 + 0.1) / 2, seen.Fog.Density, Tight);
        Assert.Equal((0.7 + 0.4) / 2, seen.Fog.Colour.X, Tight);

        // Light in log space: halfway between 20000 and 2000 is their geometric mean.
        Assert.Equal(Math.Sqrt(20000 * 2000), seen.Sky.Ambient, 1e-6);

        // Half a stop down.
        Assert.Equal(1.0 / 125 * Math.Pow(2, -0.5), seen.Camera.ShutterSpeed, Tight);
    }

    [Fact]
    public void ATurnBlendsTheShortWayRound()
    {
        var scene = Courtyard(
            [Hall(new EnvironmentOverrides { EnvironmentRotation = 10 * Math.PI / 180 })],
            new Vector3(6, 0, 0),
            new RenderEnvironment { Rotation = 350 * Math.PI / 180 });

        // Through nought and not through a hundred and eighty.
        Assert.Equal(1, Math.Cos(scene.Resolved().Environment.Rotation), Tight);
    }

    [Fact]
    public void ASwitchedOffBloomAndGradeComeOnFromNothing()
    {
        var scene = Courtyard(
            [
                Hall(
                    new EnvironmentOverrides
                    {
                        BloomStrength = 0.4,
                        Saturation = 0.5,
                        Temperature = 0.2,
                        Midtones = new Vector3(1, 0.9, 0.8),
                    }),
            ],
            new Vector3(6, 0, 0));
        Assert.False(scene.Post.Bloom.Enabled);
        Assert.False(scene.Post.Grading.Enabled);

        var post = scene.Resolved().Post;
        Assert.True(post.Bloom.Enabled);
        Assert.Equal(0.2, post.Bloom.Strength, Tight);
        Assert.True(post.Grading.Enabled);
        Assert.Equal(0.75, post.Grading.Saturation, Tight);
        Assert.Equal(0.1, post.Grading.Temperature, Tight);
        Assert.Equal(1, post.Grading.Contrast);
        Assert.Equal(0.9, post.Grading.Midtones.Z, Tight);

        // The host's objects are the host's: nothing was switched on in them.
        Assert.False(scene.Post.Bloom.Enabled);
        Assert.False(scene.Post.Grading.Enabled);
    }

    [Fact]
    public void WhereTwoOverlapTheHigherPriorityHasTheLastWord()
    {
        var low = Hall(new EnvironmentOverrides { FogDensity = 0.1 }, key: 1);
        var high = Hall(new EnvironmentOverrides { FogDensity = 0.5 }, priority: 5, key: 2);
        foreach (var order in new[] { new[] { low, high }, new[] { high, low } })
        {
            var inside = Courtyard(order, Vector3.Zero()).Resolved();
            Assert.Equal(0.5, inside.Fog.Density, Tight);

            // In the band both are at a half: the low one takes the scene's 0.002 halfway to 0.1,
            // and the high one takes that halfway to 0.5.
            var band = Courtyard(order, new Vector3(6, 0, 0)).Resolved();
            Assert.Equal((((0.002 + 0.1) / 2) + 0.5) / 2, band.Fog.Density, Tight);
        }
    }

    [Fact]
    public void EqualPrioritiesAreSettledByKeyNotByListingOrder()
    {
        var a = Hall(new EnvironmentOverrides { FogDensity = 0.1 }, key: 3);
        var b = Hall(new EnvironmentOverrides { FogDensity = 0.5 }, key: 4);
        var one = Courtyard([a, b], Vector3.Zero());
        var other = Courtyard([b, a], Vector3.Zero());
        Assert.Equal(other.Resolved().Fog.Density, one.Resolved().Fog.Density);
        Assert.Equal(0.5, one.Resolved().Fog.Density, Tight);
    }

    [Fact]
    public void TheSameQuestionAlwaysGetsTheSameAnswer()
    {
        EnvironmentVolume[] volumes =
        [
            Hall(key: 1),
            EnvironmentVolume.Sphere(
                2,
                new Vector3(3, 0, 0),
                2,
                new EnvironmentOverrides { FogColour = new Vector3(0.1, 0.2, 0.6), Ambient = 500 }) with
            {
                BlendDistance = 3,
                Priority = 1,
            },
        ];
        var basis = EnvironmentSettings.Of(Courtyard());
        foreach (var at in new[] { new Vector3(5.5, 0.3, -1), new Vector3(0, 0, 0) })
        {
            var first = basis.Resolve(volumes, at).Values;
            var second = basis.Resolve([.. volumes.Reverse()], at).Values;
            Assert.Equal(first, second);
        }
    }

    [Fact]
    public void WalkingThroughTheDoorChangesTheLookWithoutAStep()
    {
        // A hundred and one paces from well outside to well inside. A blend that stepped would show
        // as one pace changing far more than its neighbours, and a continuous one changes by a
        // bounded amount each pace and only ever in one direction.
        EnvironmentVolume[] volumes = [Hall()];
        var basis = EnvironmentSettings.Of(Courtyard());
        var densities = new List<double>();
        var ambients = new List<double>();
        for (var i = 0; i <= 100; i++)
        {
            var seen = basis.Resolve(volumes, new Vector3(10 - (i * 0.1), 0, 0));
            densities.Add(seen.FogDensity);
            ambients.Add(seen.Ambient);
        }

        Assert.Equal(0.002, densities[0]);
        Assert.Equal(0.1, densities[^1], Tight);
        for (var i = 1; i < densities.Count; i++)
        {
            // The smoothstep's steepest slope is 1.5 per band width, so a pace of a twentieth of
            // the band moves at most 7.5% of the way.
            Assert.True(densities[i] - densities[i - 1] <= 0.098 * 0.076);
            Assert.True(densities[i] >= densities[i - 1]);
            Assert.True(ambients[i] <= ambients[i - 1]);
        }
    }

    [Fact]
    public void ASceneWithVolumesSendsTheSceneTheyResolveTo()
    {
        var scene = Courtyard([Hall()], new Vector3(6, 0, 0));
        var sent = scene.ToMessage(1);
        var expected = scene.Resolved().ToMessage(1);
        SceneBuilders.AssertSameBut(sent, expected, "at");

        // The density, blended.
        Assert.Equal(0.051, SceneBuilders.Read<float[]>(sent, "fogParams")[3], 1e-6);
    }

    [Fact]
    public void CopyingASceneKeepsItsProbesFieldAndVolumes()
    {
        var scene = new RenderScene
        {
            Objects = [],
            Camera = new RenderCamera { Position = Vector3.Zero(), Target = new Vector3(0, 0, -1) },
            Probes = [new Probe { Key = 9, Position = Vector3.Zero() }],
            Volumes = [Hall()],
        };
        var copy = scene with { Fog = Fog.None };
        Assert.Single(copy.Probes);
        Assert.Single(copy.Volumes);
        Assert.Same(scene.Field, copy.Field);
    }
}

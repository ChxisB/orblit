using System.Globalization;
using Orblit.Json;
using Orblit.VectorMath;

namespace Orblit.Terrain.Tests;

public class ScatterTests
{
    private static readonly ScatterLayer Grass = new("grass", seed: 7, density: 2);

    /// <summary>Flat ground, <paramref name="across"/> regions each way, 16 texels a region.</summary>
    private static Terrain Flat(
        int across = 2,
        int regionSize = 16,
        IEnumerable<ScatterLayer>? scatter = null,
        Func<double, double, double>? height = null)
    {
        var terrain = new Terrain(regionSize: regionSize, scatter: scatter);
        for (var z = 0; z < across; z++)
        {
            for (var x = 0; x < across; x++)
            {
                terrain.FillHeights(new RegionKey(x, z), height ?? ((_, _) => 0));
            }
        }

        return terrain;
    }

    private static ScatterPlacer Placed(Terrain terrain)
    {
        var placer = new ScatterPlacer();
        placer.Update(terrain);
        return placer;
    }

    /// <summary>Where each one stands: the translation of each transform.</summary>
    private static List<Vector3> Spots(ScatterGroup group) =>
    [
        .. Enumerable.Range(0, group.Count).Select(n => new Vector3(
            group.Transforms[(n * 16) + 12],
            group.Transforms[(n * 16) + 13],
            group.Transforms[(n * 16) + 14])),
    ];

    private static List<Vector3> AllSpots(ScatterPlacer placer) => [.. placer.Groups.SelectMany(Spots)];

    /// <summary>Column <paramref name="c"/> of member <paramref name="n"/>'s transform.</summary>
    private static Vector3 Column(ScatterGroup group, int n, int c) => new(
        group.Transforms[(n * 16) + (c * 4)],
        group.Transforms[(n * 16) + (c * 4) + 1],
        group.Transforms[(n * 16) + (c * 4) + 2]);

    private static void Near(double expected, double actual, double tolerance) =>
        Assert.True(Math.Abs(expected - actual) <= tolerance, $"expected {expected:R} within {tolerance}, got {actual:R}");

    private static void NearVector(Vector3 expected, Vector3 actual) =>
        Assert.True((actual - expected).Length < 1e-5, $"expected {actual} within 1e-5 of {expected}");

    private static double Terrain3(Terrain terrain, Vector3 spot) =>
        terrain.HeightAt(spot.X, spot.Z) ?? double.NaN;

    // placing

    [Fact]
    public void LaysTheSamePatternEveryTime()
    {
        var a = Placed(Flat(scatter: [Grass]));
        var b = Placed(Flat(scatter: [Grass]));
        Assert.True(a.Count > 0);
        Assert.Equal(b.Groups.Count, a.Groups.Count);
        for (var g = 0; g < a.Groups.Count; g++)
        {
            Assert.Equal(b.Groups[g].Transforms, a.Groups[g].Transforms);
            Assert.Equal(b.Groups[g].Colours, a.Groups[g].Colours);
        }
    }

    [Fact]
    public void KeepsThePatternItHasAlwaysLaid()
    {
        // A change here moves every stone in every saved world. Make it only on purpose.
        var placer = Placed(Flat(across: 1, scatter: [Grass]));
        var first = Spots(placer.Groups.Single()).Take(3).ToList();

        // The same on the web: the pattern is integer arithmetic that stays exact in a double.
        Near(0.03972, first[0].X, 1e-5);
        Near(0.92124, first[1].X, 1e-5);
        Near(1.71098, first[2].X, 1e-5);
        Near(0.58308, first[0].Z, 1e-5);
        Near(0.70224, first[1].Z, 1e-5);
        Near(0.50828, first[2].Z, 1e-5);
    }

    [Fact]
    public void LaysThePatternOverTheWorldNotOverEachRegion()
    {
        static double Hills(double x, double z) => (Math.Sin(x * 0.3) * 2) + (z * 0.1);
        var small = Placed(Flat(across: 4, regionSize: 8, height: Hills, scatter: [Grass]));
        var large = Placed(Flat(across: 1, regionSize: 32, height: Hills, scatter: [Grass]));

        static List<string> Sorted(ScatterPlacer placer) =>
        [
            .. AllSpots(placer)
                .Select(spot => $"{Doubles.ToStringAsFixed(spot.X, 4)},{Doubles.ToStringAsFixed(spot.Z, 4)}")
                .Order(StringComparer.Ordinal),
        ];

        // Everything but the last row and column of texels, which the large region has ground
        // beyond and the small ones do not.
        static bool Inside(string spot)
        {
            var parts = spot.Split(',').Select(part => double.Parse(part, CultureInfo.InvariantCulture)).ToList();
            return parts[0] < 31 && parts[1] < 31;
        }

        Assert.Equal(Sorted(large).Where(Inside).ToList(), Sorted(small).Where(Inside).ToList());
    }

    [Fact]
    public void PutsDownAboutAsManyAsTheDensitySays()
    {
        var placer = Placed(Flat(across: 2, scatter: [Grass]));

        // Four regions of 16 m square at 2 per square metre, less the far edges, where there is no
        // ground beyond the last texel to stand on.
        Near(2 * 31 * 31, placer.Count, 2 * 31 * 31 * 0.06);
    }

    [Fact]
    public void StandsOnlyOnGroundThatIsThere()
    {
        var terrain = Flat(across: 2, scatter: [Grass]);
        terrain.RemoveRegion(new RegionKey(1, 1));
        var placer = Placed(terrain);
        Assert.Empty(placer.GroupsIn(new RegionKey(1, 1)));
        foreach (var spot in AllSpots(placer))
        {
            Assert.NotNull(terrain.HeightAt(spot.X, spot.Z));
        }
    }

    [Fact]
    public void PutsNothingInAHole()
    {
        var terrain = Flat(across: 1, scatter: [Grass]);
        var region = terrain.RegionAt(new RegionKey(0, 0))!;
        for (var j = 4; j < 10; j++)
        {
            for (var i = 4; i < 10; i++)
            {
                region.SetCover(i, j, Cover.Auto.WithHole(true));
            }
        }

        Assert.True(new Cover(region.Cover[region.IndexOf(5, 5)]).Hole);
        var before = Placed(Flat(across: 1, scatter: [Grass])).Count;
        var placer = Placed(terrain);
        Assert.True(placer.Count < before - (2 * 25));
        foreach (var spot in AllSpots(placer))
        {
            Assert.NotNull(terrain.HeightAt(spot.X, spot.Z));
            var inHole = spot.X > 4 && spot.X < 9 && spot.Z > 4 && spot.Z < 9;
            Assert.False(inHole, $"{spot}");
        }
    }

    [Fact]
    public void GrowsOnlyOnItsSetsAsThickAsTheyCoverTheGround()
    {
        var onGrass = new ScatterLayer("grass", seed: 7, density: 4, sets: [1]);

        Terrain Painted(Cover cover)
        {
            var terrain = Flat(across: 1, scatter: [onGrass]);
            var region = terrain.RegionAt(new RegionKey(0, 0))!;
            for (var at = 0; at < region.Cover.Length; at++)
            {
                region.Cover[at] = cover.Word;
            }

            region.Touch();
            return terrain;
        }

        var all = Placed(Painted(Cover.Of(@base: 1, overlay: 1))).Count;
        var none = Placed(Painted(Cover.Of(@base: 0, overlay: 0))).Count;
        var half = Placed(Painted(Cover.Of(@base: 0, overlay: 1, blend: 0.5))).Count;
        var quarter = Placed(Painted(Cover.Of(@base: 1, overlay: 0, blend: 0.75))).Count;
        Assert.True(all > 800);
        Assert.Equal(0, none);
        Near(0.5, (double)half / all, 0.06);
        Near(0.25, (double)quarter / all, 0.06);
    }

    [Fact]
    public void ReadsAutomaticGroundAsTheSetsItChoosesBetween()
    {
        var onGrass = new ScatterLayer("grass", density: 4, sets: [1]);
        var onScree = new ScatterLayer("scree", density: 4, sets: [0]);

        // Level: all the flat set, which is 1 by default.
        var level = Placed(Flat(across: 1, scatter: [onGrass, onScree]));
        Assert.True(level.Groups[0].Count > 800);
        Assert.Equal(0, level.Groups[1].Count);

        // Steep: 70 degrees is past where the flat set is gone. Asked of the middle region, since
        // the slope at the terrain's outer edge is read from one side only and comes out gentler.
        var steep = Placed(Flat(
            across: 3,
            scatter: [onGrass, onScree],
            height: (x, _) => x * Math.Tan(70 * Math.PI / 180))).GroupsIn(new RegionKey(1, 1));
        Assert.Equal(0, steep[0].Count);
        Assert.True(steep[1].Count > 800);
    }

    [Fact]
    public void KeepsToItsSlopes()
    {
        // The middle region of three, away from the terrain's outer edge.
        static int Ramp(double degrees, ScatterLayer layer) => Placed(Flat(
            across: 3,
            scatter: [layer],
            height: (x, _) => x * Math.Tan(degrees * Math.PI / 180))).GroupsIn(new RegionKey(1, 1)).Single().Count;

        var gentle = new ScatterLayer("gentle", density: 2, maxSlope: 20);
        var steep = new ScatterLayer("steep", density: 2, minSlope: 25);
        Assert.Equal(0, Ramp(30, gentle));
        Assert.True(Ramp(10, gentle) > 400);
        Assert.Equal(0, Ramp(10, steep));
        Assert.True(Ramp(30, steep) > 400);
    }

    [Fact]
    public void KeepsToItsHeights()
    {
        var low = new ScatterLayer("low", density: 2, maxHeight: 5);
        var high = new ScatterLayer("high", density: 2, minHeight: 5);
        var terrain = Flat(across: 1, scatter: [low, high], height: (x, _) => x);
        var placer = Placed(terrain);
        Assert.True(placer.Groups[0].Count > 0);
        Assert.True(placer.Groups[1].Count > 0);
        foreach (var spot in Spots(placer.Groups[0]))
        {
            Assert.True(Terrain3(terrain, spot) <= 5);
        }

        foreach (var spot in Spots(placer.Groups[1]))
        {
            Assert.True(Terrain3(terrain, spot) >= 5);
        }
    }

    [Fact]
    public void ATrunkAndACrownWithOneSeedStandTogether()
    {
        var trunk = new ScatterLayer("trunk", seed: 3, density: 0.5, size: (0.3, 2, 0.3), minScale: 0.8, maxScale: 1.2);
        var crown = new ScatterLayer(
            "crown",
            seed: 3,
            density: 0.5,
            size: (1.5, 1.5, 1.5),
            lift: 2,
            minScale: 0.8,
            maxScale: 1.2);
        var placer = Placed(Flat(across: 1, scatter: [trunk, crown]));
        var trunks = Spots(placer.Groups[0]);
        var crowns = Spots(placer.Groups[1]);
        Assert.Equal(crowns.Count, trunks.Count);
        for (var n = 0; n < trunks.Count; n++)
        {
            Near(trunks[n].X, crowns[n].X, 1e-5);
            Near(trunks[n].Z, crowns[n].Z, 1e-5);

            // The crown's bottom is the trunk's top, whatever the size drawn.
            var trunkTop = trunks[n].Y + Column(placer.Groups[0], n, 1).Y;
            var crownBottom = crowns[n].Y - Column(placer.Groups[1], n, 1).Y;
            Near(trunkTop, crownBottom, 1e-4);
        }
    }

    [Fact]
    public void LayersWithTheirOwnSeedsStandApart()
    {
        var stones = new ScatterLayer("stones", seed: 1, density: 2);
        var flowers = new ScatterLayer("flowers", seed: 2, density: 2);
        var placer = Placed(Flat(across: 1, scatter: [stones, flowers]));
        var a = Spots(placer.Groups[0]).Select(s => $"{Doubles.ToDartString(s.X)},{Doubles.ToDartString(s.Z)}").ToHashSet();
        var b = Spots(placer.Groups[1]).Select(s => $"{Doubles.ToDartString(s.X)},{Doubles.ToDartString(s.Z)}").ToHashSet();
        Assert.Empty(a.Intersect(b));
    }

    // standing

    [Fact]
    public void ABlockStandsOnItsBottomFaceSunkByItsLift()
    {
        var block = new ScatterLayer("block", density: 1, size: (0.2, 1, 0.4), lift: -0.1, turn: false);
        var terrain = Flat(across: 1, scatter: [block], height: (_, _) => 3);
        var group = Placed(terrain).Groups.Single();
        Assert.True(group.Count > 0);
        for (var n = 0; n < group.Count; n++)
        {
            // The cube runs -1 to 1, so each column is half the block.
            Near(0.1, Column(group, n, 0).Length, 1e-5);
            NearVector(new Vector3(0, 0.5, 0), Column(group, n, 1));
            Near(0.2, Column(group, n, 2).Length, 1e-5);
            Near(3 - 0.1 + 0.5, Spots(group)[n].Y, 1e-5);
        }
    }

    [Fact]
    public void AModelStandsOnItsOriginAtItsSize()
    {
        var tree = new ScatterLayer(
            "tree",
            density: 1,
            mesh: "models/tree.glb",
            size: (2, 3, 2),
            minScale: 0.5,
            maxScale: 1.5);
        var terrain = Flat(across: 1, scatter: [tree], height: (_, _) => 3);
        var group = Placed(terrain).Groups.Single();
        for (var n = 0; n < group.Count; n++)
        {
            var scale = Column(group, n, 1).Length / 3;
            Assert.InRange(scale, 0.5 - 1e-6, 1.5 + 1e-6);
            Near(2 * scale, Column(group, n, 0).Length, 1e-4);
            Near(2 * scale, Column(group, n, 2).Length, 1e-4);
            Near(3, Spots(group)[n].Y, 1e-5);
        }

        // Not all one size.
        var sizes = Enumerable.Range(0, group.Count)
            .Select(n => Doubles.ToStringAsFixed(Column(group, n, 1).Length, 3))
            .ToHashSet();
        Assert.True(sizes.Count > 10);
    }

    [Fact]
    public void LeansWithTheGroundAsFarAsItIsTold()
    {
        var upright = new ScatterLayer("upright", density: 1, mesh: "m");
        var square = new ScatterLayer("square", density: 1, mesh: "m", lean: 1);
        var between = new ScatterLayer("between", density: 1, mesh: "m", lean: 0.5);
        var terrain = Flat(
            across: 1,
            scatter: [upright, square, between],
            height: (x, _) => x * Math.Tan(30 * Math.PI / 180));
        var placer = Placed(terrain);
        for (var layer = 0; layer < 3; layer++)
        {
            var group = placer.Groups[layer];
            Assert.True(group.Count > 0);
            for (var n = 0; n < group.Count; n++)
            {
                var spot = Spots(group)[n];
                var normal = terrain.NormalAt(spot.X, spot.Z)!;
                var up = Column(group, n, 1).Normalized();
                var tilt = Math.Acos(Doubles.Clamp(up.Y, -1.0, 1.0)) * 180 / Math.PI;
                switch (layer)
                {
                    case 0:
                        NearVector(new Vector3(0, 1, 0), up);
                        break;
                    case 1:
                        NearVector(normal, up);
                        break;
                    case 2:
                        // Halfway leans half as far as the ground does.
                        var ground = Math.Acos(normal.Y) * 180 / Math.PI;
                        Near(ground / 2, tilt, 1e-3);
                        Assert.True(up.X < 0);
                        break;
                }

                // Still a rotation: the columns square to one another.
                var side = Column(group, n, 0).Normalized();
                var front = Column(group, n, 2).Normalized();
                Near(0, side.Dot(up), 1e-5);
                Near(0, front.Dot(up), 1e-5);
                Near(1, side.Cross(up).Dot(front), 1e-5);
            }
        }
    }

    [Fact]
    public void TurnsEachADifferentWayUnlessToldNotTo()
    {
        var turned = new ScatterLayer("turned", density: 1, mesh: "m");
        var straight = new ScatterLayer("straight", density: 1, mesh: "m", turn: false);
        var placer = Placed(Flat(across: 1, scatter: [turned, straight]));
        var angles = Enumerable.Range(0, placer.Groups[0].Count)
            .Select(n => Doubles.ToStringAsFixed(Column(placer.Groups[0], n, 0).X, 2))
            .ToHashSet();
        Assert.True(angles.Count > 20);
        for (var n = 0; n < placer.Groups[1].Count; n++)
        {
            NearVector(new Vector3(1, 0, 0), Column(placer.Groups[1], n, 0));
        }
    }

    [Fact]
    public void ItsBoxHoldsEveryOneOfThem()
    {
        var placer = Placed(Flat(
            across: 1,
            scatter: [new ScatterLayer("rocks", density: 1, size: (1, 2, 1))],
            height: (x, _) => Math.Sin(x) * 3));
        var group = placer.Groups.Single();
        for (var n = 0; n < group.Count; n++)
        {
            var spot = Spots(group)[n];
            var top = spot + Column(group, n, 1);
            foreach (var point in new[] { spot, top })
            {
                for (var axis = 0; axis < 3; axis++)
                {
                    Assert.True(point[axis] >= group.Minimum[axis]);
                    Assert.True(point[axis] <= group.Maximum[axis]);
                }
            }
        }
    }

    [Fact]
    public void IsItsColourVariedAndTintedByTheGround()
    {
        var plain = new ScatterLayer("plain", density: 1, colour: 0x808080);
        var varied = new ScatterLayer("varied", density: 1, colour: 0x808080, colourVariation: 0.5);
        var tinted = new ScatterLayer("tinted", density: 1, colour: 0xFFFFFF, groundTint: 1);
        var terrain = Flat(across: 1, scatter: [plain, varied, tinted]);
        var region = terrain.RegionAt(new RegionKey(0, 0))!;
        for (var j = 0; j < 16; j++)
        {
            for (var i = 0; i < 16; i++)
            {
                region.SetColour(i, j, GroundColour.Of(red: 255, green: 0, blue: 0));
            }
        }

        var placer = Placed(terrain);

        // 0x80 is a fifth of the way up in linear light.
        foreach (var channel in placer.Groups[0].Colours)
        {
            Near(0.2158, channel, 1e-3);
        }

        var shades = placer.Groups[1].Colours;
        Assert.True(shades.Min() < 0.2158 * 0.8);
        Assert.True(shades.Max() > 0.2158 * 1.2);
        Assert.True(shades.Min() >= (0.2158 * 0.5) - 1e-6);
        var red = placer.Groups[2].Colours;
        for (var n = 0; n < red.Length; n += 3)
        {
            Near(1, red[n], 1e-6);
            Near(0, red[n + 1], 1e-6);
            Near(0, red[n + 2], 1e-6);
        }
    }

    // keeping up

    [Fact]
    public void DoesNothingWhenNothingChanged()
    {
        var terrain = Flat(scatter: [Grass]);
        var placer = new ScatterPlacer();
        Assert.True(placer.Update(terrain));
        var revision = placer.Revision;
        Assert.False(placer.Update(terrain));
        Assert.Equal(revision, placer.Revision);
    }

    [Fact]
    public void PlacesAgainOnlyTheRegionThatWasEdited()
    {
        var terrain = Flat(across: 3, scatter: [Grass]);
        var placer = Placed(terrain);
        var before = placer.Groups.ToDictionary(group => group.Key);

        // The middle of the middle region: nowhere near an edge.
        terrain.RegionAt(new RegionKey(1, 1))!.SetHeight(8, 8, 2);
        Assert.True(placer.Update(terrain));
        foreach (var group in placer.Groups)
        {
            if (group.Key == new RegionKey(1, 1))
            {
                Assert.NotEqual(before[group.Key].Revision, group.Revision);
                Assert.Equal(before[group.Key].Id, group.Id);
            }
            else
            {
                Assert.Same(before[group.Key], group);
            }
        }
    }

    [Fact]
    public void PlacesANeighbourAgainWhenTheirSharedEdgeMoves()
    {
        var terrain = Flat(across: 3, scatter: [Grass]);
        var placer = Placed(terrain);
        var before = placer.Groups.ToDictionary(group => group.Key);

        // The first column of the middle region: its left neighbour reads it.
        terrain.RegionAt(new RegionKey(1, 1))!.SetHeight(0, 8, 2);
        placer.Update(terrain);
        var moved = placer.Groups
            .Where(group => !ReferenceEquals(group, before[group.Key]))
            .Select(group => group.Key)
            .ToHashSet();
        Assert.True(moved.SetEquals([new RegionKey(1, 1), new RegionKey(0, 1)]));
    }

    [Fact]
    public void MovesNothingOutsideTheRegionItPlacesAgain()
    {
        var terrain = Flat(across: 2, scatter: [Grass]);
        var placer = Placed(terrain);
        var before = AllSpots(placer).Select(s => s.ToString()).ToList();
        var region = terrain.RegionAt(new RegionKey(0, 0))!;
        for (var j = 2; j < 12; j++)
        {
            for (var i = 2; i < 12; i++)
            {
                region.SetCover(i, j, Cover.Auto.WithHole(true));
            }
        }

        placer.Update(terrain);
        var after = AllSpots(placer).Select(s => s.ToString()).ToHashSet();
        var kept = before.Count(after.Contains);

        // Everything outside the hole is where it was.
        Assert.Equal(AllSpots(placer).Count, kept);
        Assert.True(after.Count < before.Count);
    }

    [Fact]
    public void PlacesALayerAgainEverywhereWhenItsRulesChangeAndOnlyIt()
    {
        var terrain = Flat(scatter: [Grass, new ScatterLayer("rocks", seed: 2, density: 0.2)]);
        var placer = Placed(terrain);
        var rocks = placer.Groups.Where(group => group.Layer == 1).ToList();
        terrain.Scatter[0] = new ScatterLayer("grass", seed: 7, density: 1);
        Assert.True(placer.Update(terrain));
        foreach (var group in placer.Groups.Where(group => group.Layer == 1))
        {
            Assert.Contains(rocks, rock => ReferenceEquals(rock, group));
        }

        Assert.True(placer.Count < 2 * 31 * 31);
    }

    [Fact]
    public void FollowsLayersAndRegionsComingAndGoing()
    {
        var terrain = Flat(scatter: [Grass]);
        var placer = Placed(terrain);
        Assert.Equal(4, placer.Groups.Count);
        terrain.Scatter.Add(new ScatterLayer("rocks", seed: 2));
        Assert.True(placer.Update(terrain));
        Assert.Equal(8, placer.Groups.Count);
        Assert.Equal(["grass", "rocks"], placer.Layers.Select(layer => layer.Name));
        terrain.Scatter.RemoveAt(0);

        // The rules a group was placed by stay until the next update.
        Assert.Equal(["grass", "rocks"], placer.Layers.Select(layer => layer.Name));
        Assert.True(placer.Update(terrain));
        Assert.Equal(4, placer.Groups.Count);
        Assert.True(placer.Groups.Select(group => group.Layer).ToHashSet().SetEquals([0]));
        Assert.Equal("rocks", placer.Layers.Single().Name);
        terrain.RemoveRegion(new RegionKey(1, 0));
        Assert.True(placer.Update(terrain));
        Assert.Equal(3, placer.Groups.Count);
        Assert.Empty(placer.GroupsIn(new RegionKey(1, 0)));

        // Ids are never shared between two groups held at once.
        var ids = placer.Groups.Select(group => group.Id).ToList();
        Assert.Equal(ids.Count, ids.ToHashSet().Count);
    }

    [Fact]
    public void PlacesAutomaticGroundAgainWhenItsRuleChanges()
    {
        var onGrass = new ScatterLayer("grass", density: 2, sets: [1]);
        var terrain = Flat(across: 1, scatter: [onGrass]);
        var placer = Placed(terrain);
        Assert.True(placer.Count > 0);
        terrain.AutoCover = new AutoCover(steep: 1, flat: 0);
        Assert.True(placer.Update(terrain));
        Assert.Equal(0, placer.Count);
    }

    // the file

    [Fact]
    public void ALayerSaysOnlyWhatIsNotItsDefault()
    {
        var expected = new OrderedDictionary<string, object?>
        {
            ["name"] = "grass",
            ["density"] = 1.0,
            ["size"] = new List<object?> { 1.0, 1.0, 1.0 },
            ["colour"] = "#ffffff",
        };
        Assert.Equal(DartJson.Encode(expected), DartJson.Encode(new ScatterLayer("grass").ToJson()));
    }

    [Fact]
    public void ALayerComesBackAsItWent()
    {
        var layer = new ScatterLayer(
            "pines",
            seed: 12,
            density: 0.05,
            sets: [1, 3],
            minSlope: 2,
            maxSlope: 35,
            minHeight: -4,
            maxHeight: 400,
            size: (1.2, 1.4, 1.2),
            minScale: 0.7,
            maxScale: 1.6,
            lean: 0.1,
            turn: false,
            lift: -0.3,
            colour: 0x2f5d3a,
            colourVariation: 0.15,
            groundTint: 0.4,
            range: 600,
            castShadows: true,
            mesh: "models/pine.glb",
            material: "materials/pine.omat");
        var problems = new List<string>();
        Assert.Equal(layer, ScatterLayer.FromJson(layer.ToJson(), problems));
        Assert.Empty(problems);
    }

    [Fact]
    public void ALayerOutOfRangeIsBroughtIntoItWithANote()
    {
        var problems = new List<string>();
        var layer = ScatterLayer.FromJson(
            DartJson.Decode("""{"name":"weeds","density":5000,"sets":[1,99],"minSlope":50,"maxSlope":10,"lean":3,"size":[1,-2,1]}"""),
            problems)!;
        Assert.Equal(ScatterLayer.MaxDensity, layer.Density);
        Assert.Equal([1], layer.Sets);
        Assert.Equal((10.0, 50.0), (layer.MinSlope, layer.MaxSlope));
        Assert.Equal(1, layer.Lean);
        Assert.Equal((1.0, 1.0, 1.0), layer.Size);
        Assert.Equal(4, problems.Count);
        Assert.Null(ScatterLayer.FromJson(DartJson.Decode("""{"density":2}"""), problems));
    }

    [Fact]
    public void TravelsInTheTerrainFileALayerToALine()
    {
        var terrain = new Terrain(regionSize: 16, scatter: [Grass, new ScatterLayer("rocks", seed: 2, density: 0.1)]);
        var text = terrain.Encode();
        Assert.Contains("\n    {\"name\":\"grass\"", text);
        Assert.Contains("\n    {\"name\":\"rocks\"", text);
        var back = Terrain.Decode(text);
        Assert.Empty(back.Problems);
        Assert.Equal(terrain.Scatter, back.Terrain.Scatter);
    }

    [Fact]
    public void ATerrainWithNothingScatteredIsWrittenAsItWasBefore()
    {
        Assert.DoesNotContain("scatter", new Terrain(regionSize: 16).Encode());
    }
}

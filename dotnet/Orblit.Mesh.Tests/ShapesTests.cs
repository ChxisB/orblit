namespace Orblit.Mesh.Tests;

public class ShapesTests
{
    /// <summary>
    /// Whether every face points away from the middle of the shape.
    ///
    /// The one property that matters and is easy to get wrong: a face wound the other way is
    /// invisible from outside, and a shape with one of them looks like it has a hole in it.
    /// </summary>
    private static bool EveryFaceOutwards(Mesh mesh, Vector3? about = null)
    {
        var box = mesh.Bounds;
        var centre = about ?? ((box.Min + box.Max) / 2);

        foreach (var face in mesh.Faces)
        {
            var outwards = mesh.CentreOf(face) - centre;
            if (outwards.Length < 1e-9)
            {
                continue;
            }

            if (mesh.NormalOf(face).Dot(outwards) <= 0)
            {
                return false;
            }
        }

        return true;
    }

    private static Mesh Build(ShapeKind kind) => new Shape { Kind = kind }.Build();

    // every shape

    [Fact]
    public void IsBuiltAndHasFacesWithAtLeastThreeCorners()
    {
        foreach (var kind in Enum.GetValues<ShapeKind>())
        {
            var mesh = Shape.Of(kind).Build();
            Assert.True(mesh.Faces.Count > 0, kind.ToString());
            foreach (var face in mesh.Faces)
            {
                Assert.True(face.Vertices.Count >= 3, kind.ToString());
            }
        }
    }

    [Fact]
    public void NamesPointsThatExist()
    {
        foreach (var kind in Enum.GetValues<ShapeKind>())
        {
            var mesh = Shape.Of(kind).Build();
            foreach (var face in mesh.Faces)
            {
                foreach (var index in face.Vertices)
                {
                    Assert.True(index >= 0 && index <= mesh.Positions.Count - 1, kind.ToString());
                }
            }
        }
    }

    [Fact]
    public void HasNoFaceWithACornerUsedTwice()
    {
        foreach (var kind in Enum.GetValues<ShapeKind>())
        {
            var mesh = Shape.Of(kind).Build();
            foreach (var face in mesh.Faces)
            {
                Assert.True(face.Vertices.ToHashSet().Count == face.Vertices.Count, kind.ToString());
            }
        }
    }

    [Fact]
    public void StandsOnTheGroundRatherThanHalfThroughIt()
    {
        foreach (var kind in Enum.GetValues<ShapeKind>())
        {
            var mesh = (Shape.Of(kind) with { Height = 2 }).Build();

            // Somebody putting a box in a scene expects it on the floor; a shape centred on its
            // middle sinks half into it.
            Assert.True(Math.Abs(mesh.Bounds.Min.Y) <= 1e-9, kind.ToString());
        }
    }

    [Fact]
    public void IsAsBigAsItWasAskedToBe()
    {
        foreach (var kind in Enum.GetValues<ShapeKind>())
        {
            // A plane and a sprite are flat, and a torus is as tall as its tube rather than as tall
            // as its box. The tube radius is its own measurement, which is what makes a thin ring
            // possible.
            if (kind is ShapeKind.Plane or ShapeKind.Sprite or ShapeKind.Torus)
            {
                continue;
            }

            var mesh = (Shape.Of(kind) with { Width = 3, Height = 5, Depth = 7 }).Build();
            var box = mesh.Bounds;
            Assert.True(Math.Abs((box.Max.Y - box.Min.Y) - 5) <= 0.6, kind.ToString());
        }
    }

    [Fact]
    public void SurvivesCountsNobodyWouldChoose()
    {
        // A dragged slider goes through every number on the way to the one somebody wanted,
        // including nought and a hundred thousand.
        foreach (var kind in Enum.GetValues<ShapeKind>())
        {
            foreach (var sides in new[] { -5, 0, 1, 2, 3, 4096 })
            {
                var mesh = (Shape.Of(kind) with
                {
                    Sides = sides,
                    Rings = sides,
                    Columns = sides,
                    Steps = sides,
                    Subdivisions = sides,
                    WidthCuts = sides,
                    HeightCuts = sides,
                }).Build();
                Assert.True(mesh.Faces.Count > 0, $"{kind} x {sides}");
                Assert.True(mesh.Positions.All(at => double.IsFinite(at.X)), $"{kind} x {sides}");
            }
        }
    }

    // the box

    [Fact]
    public void TheBoxIsSixFacesOverEightCorners()
    {
        var mesh = Build(ShapeKind.Cube);
        Assert.Equal(6, mesh.Faces.Count);
        Assert.Equal(8, mesh.Positions.Count);
        Assert.All(mesh.Faces, face => Assert.True(face.IsQuad));
    }

    [Fact]
    public void TheBoxFacesOutwardsOnEverySide()
    {
        Assert.True(EveryFaceOutwards(Build(ShapeKind.Cube)));
    }

    [Fact]
    public void TheBoxIsTheSizeItWasGiven()
    {
        var box = new Shape { Kind = ShapeKind.Cube, Width = 4, Height = 2, Depth = 6 }.Build().Bounds;

        Assert.Equal(4, box.Max.X - box.Min.X, 1e-9);
        Assert.Equal(2, box.Max.Y - box.Min.Y, 1e-9);
        Assert.Equal(6, box.Max.Z - box.Min.Z, 1e-9);
    }

    // the plane

    [Fact]
    public void IsAGridSoThereAreEdgesToPullOnLater()
    {
        var mesh = new Shape { Kind = ShapeKind.Plane, WidthCuts = 2, HeightCuts = 3 }.Build();

        // Three columns by four rows, from two cuts across and three along.
        Assert.Equal(12, mesh.Faces.Count);
        Assert.Equal(4 * 5, mesh.Positions.Count);
    }

    [Fact]
    public void EveryFaceFacesUp()
    {
        var mesh = new Shape { Kind = ShapeKind.Plane, WidthCuts = 2 }.Build();
        foreach (var face in mesh.Faces)
        {
            Assert.Equal(1, mesh.NormalOf(face).Y, 1e-9);
        }
    }

    [Fact]
    public void WithNoCutsItIsOneQuad()
    {
        var mesh = new Shape { Kind = ShapeKind.Plane, WidthCuts = 0, HeightCuts = 0 }.Build();
        Assert.Single(mesh.Faces);
    }

    [Fact]
    public void ASpriteIsAPlaneOneUnitSquare()
    {
        var mesh = new Shape { Kind = ShapeKind.Sprite, Width = 9, Depth = 9 }.Build();
        var box = mesh.Bounds;

        Assert.Single(mesh.Faces);
        Assert.Equal(1, box.Max.X - box.Min.X, 1e-9);
    }

    // the prism

    [Fact]
    public void IsABoxWithARoof()
    {
        var mesh = Build(ShapeKind.Prism);

        // A bottom, two slopes and two gables.
        Assert.Equal(5, mesh.Faces.Count);
        Assert.Equal(6, mesh.Positions.Count);
        Assert.True(EveryFaceOutwards(mesh));
    }

    // the pipe

    [Fact]
    public void HasAnOutsideAnInsideAndTwoRims()
    {
        var mesh = new Shape { Kind = ShapeKind.Pipe, Sides = 8 }.Build();

        // Eight outside, eight inside, eight top and eight bottom.
        Assert.Equal(32, mesh.Faces.Count);
    }

    [Fact]
    public void TheInsideFacesInwards()
    {
        var mesh = new Shape { Kind = ShapeKind.Pipe, Sides = 12 }.Build();

        // A pipe whose bore faces outwards is a pipe you cannot see through.
        var inward = 0;
        foreach (var face in mesh.Faces)
        {
            var centre = mesh.CentreOf(face);
            var radial = new Vector3(centre.X, 0, centre.Z);
            if (radial.Length < 1e-6)
            {
                continue;
            }

            // Normalised: an unnormalised dot is scaled by how far out the face is, and the bore of
            // a thin pipe is close to the middle.
            if (mesh.NormalOf(face).Dot(radial.Normalized()) < -0.5)
            {
                inward++;
            }
        }

        Assert.Equal(12, inward);
    }

    [Fact]
    public void AWallThickerThanThePipeDoesNotTurnItInsideOut()
    {
        var mesh = new Shape { Kind = ShapeKind.Pipe, Sides = 8, Thickness = 99 }.Build();
        Assert.True(mesh.Positions.All(at => double.IsFinite(at.X)));
        Assert.NotEmpty(mesh.Faces);
    }

    [Fact]
    public void HeightCutsAddRingsOfFaces()
    {
        var plain = new Shape { Kind = ShapeKind.Pipe, Sides = 8 }.Build();
        var cut = new Shape { Kind = ShapeKind.Pipe, Sides = 8, HeightCuts = 2 }.Build();

        Assert.True(cut.Faces.Count > plain.Faces.Count);
    }

    // the torus

    [Fact]
    public void IsAClosedRingWithNoBorder()
    {
        var mesh = new Shape { Kind = ShapeKind.Torus, Rings = 12, Columns = 8 }.Build();

        Assert.Equal(12 * 8, mesh.Faces.Count);

        // Closed all the way round, so nothing is on the border.
        Assert.Empty(mesh.OpenFaces);
    }

    [Fact]
    public void PartOfACircumferenceIsOpenAtBothEnds()
    {
        var mesh = new Shape { Kind = ShapeKind.Torus, Rings = 12, Columns = 8, Circumference = 180 }.Build();

        Assert.NotEmpty(mesh.OpenFaces);
    }

    // the door

    [Fact]
    public void IsTwoUprightsAndALintelWithAGapBetween()
    {
        var mesh = new Shape
        {
            Kind = ShapeKind.Door,
            Width = 3,
            Height = 4,
            SideWidth = 0.5,
            PedimentHeight = 0.5,
        }.Build();

        // Three boxes, six faces each.
        Assert.Equal(18, mesh.Faces.Count);

        // Nothing in the middle at the bottom, which is the point of a door.
        var atFloor = mesh.Positions.Where(at => at.Y < 0.01);
        Assert.DoesNotContain(atFloor, at => Math.Abs(at.X) < 0.4);
    }

    // the cylinder

    [Fact]
    public void HasASidePerSegmentAndTwoEnds()
    {
        var mesh = new Shape { Kind = ShapeKind.Cylinder, Sides = 12 }.Build();
        Assert.Equal(12 + 2, mesh.Faces.Count);
    }

    [Fact]
    public void HeightCutsDivideTheSidesWithoutChangingTheShape()
    {
        var mesh = new Shape { Kind = ShapeKind.Cylinder, Sides = 8, HeightCuts = 3 }.Build();

        // Four rings of eight, plus the two ends.
        Assert.Equal((8 * 4) + 2, mesh.Faces.Count);
        Assert.Equal(1, mesh.Bounds.Max.Y, 1e-9);
    }

    [Fact]
    public void WithoutEndsItIsATube()
    {
        var mesh = new Shape { Kind = ShapeKind.Cylinder, Sides = 12, Capped = false }.Build();
        Assert.Equal(12, mesh.Faces.Count);

        // Every face is on the border, because there are no ends to close it.
        Assert.Equal(12, mesh.OpenFaces.Count);
    }

    [Fact]
    public void FacesOutwards()
    {
        Assert.True(EveryFaceOutwards(new Shape { Kind = ShapeKind.Cylinder, Sides = 16 }.Build()));
    }

    [Fact]
    public void ItsSidesAreSmoothAndItsEndsAreNot()
    {
        var mesh = new Shape { Kind = ShapeKind.Cylinder, Sides = 8 }.Build();
        var smooth = mesh.Faces.Count(face => face.Smooth);

        // A cylinder that shades flat looks like a nut, and ends that shade smooth look like a
        // bulge.
        Assert.Equal(8, smooth);
    }

    // the cone

    [Fact]
    public void FacesOutwardsCapIncluded()
    {
        Assert.True(EveryFaceOutwards(new Shape { Kind = ShapeKind.Cone, Sides = 12 }.Build()));
    }

    [Fact]
    public void IsAFanOfTrianglesAroundOneTip()
    {
        var mesh = new Shape { Kind = ShapeKind.Cone, Sides = 9 }.Build();
        Assert.Equal(9, mesh.Faces.Count(face => face.IsTriangle));
        Assert.Equal(10, mesh.Positions.Count);
    }

    [Fact]
    public void WithoutACapItIsOpenUnderneath()
    {
        var mesh = new Shape { Kind = ShapeKind.Cone, Sides = 9, Capped = false }.Build();
        Assert.Equal(9, mesh.Faces.Count);
    }

    // the sphere

    [Fact]
    public void IsAnIcosphereSoItsFacesAreAllAboutTheSameSize()
    {
        var mesh = new Shape { Kind = ShapeKind.Sphere, Subdivisions = 1 }.Build();

        // The twenty faces of an icosahedron over its twelve corners. Latitude and longitude would
        // crowd every face into the poles, where a texture pinches and a deformation bunches.
        Assert.Equal(20, mesh.Faces.Count);
        Assert.Equal(12, mesh.Positions.Count);

        var areas = mesh.Faces.Select(mesh.AreaOf).ToList();
        Assert.Equal(1, areas.Max() / areas.Min(), 1e-6);
    }

    [Fact]
    public void EachDivisionQuadruplesTheFaces()
    {
        foreach (var (times, faces) in new[] { (1, 20), (2, 80), (3, 320) })
        {
            Assert.Equal(faces, new Shape { Kind = ShapeKind.Sphere, Subdivisions = times }.Build().Faces.Count);
        }
    }

    // The same name as the cylinder test above, in the other group.
    [Fact]
    public void SphereFacesOutwards()
    {
        Assert.True(EveryFaceOutwards(new Shape { Kind = ShapeKind.Sphere, Subdivisions = 2 }.Build()));
    }

    [Fact]
    public void EveryPointIsOnTheSurface()
    {
        var mesh = new Shape
        {
            Kind = ShapeKind.Sphere,
            Width = 2,
            Height = 2,
            Depth = 2,
            Subdivisions = 3,
        }.Build();

        foreach (var at in mesh.Positions)
        {
            Assert.Equal(1, (at - new Vector3(0, 1, 0)).Length, 1e-9);
        }
    }

    // the stairs

    [Fact]
    public void HasAStepPerFlight()
    {
        var mesh = new Shape { Kind = ShapeKind.Stairs, Steps = 5 }.Build();

        // Six faces a step, since each is a box until somebody welds them.
        Assert.Equal(5 * 6, mesh.Faces.Count);
    }

    [Fact]
    public void ByHeightItWorksOutHowManyStepsFit()
    {
        var mesh = new Shape { Kind = ShapeKind.Stairs, ByCount = false, StepHeight = 0.25, Height = 2 }.Build();

        // Eight steps of a quarter each, rather than a number somebody counted.
        Assert.Equal(8 * 6, mesh.Faces.Count);
    }

    [Fact]
    public void AStepHeightOfNothingDoesNotMakeAnInfinityOfSteps()
    {
        var mesh = new Shape { Kind = ShapeKind.Stairs, ByCount = false, StepHeight = 0 }.Build();
        Assert.True(mesh.Faces.Count < (256 * 6) + 1);
    }

    [Fact]
    public void ClimbsByTheHeightItWasGiven()
    {
        var mesh = new Shape { Kind = ShapeKind.Stairs, Steps = 4, Height = 2, Depth = 4 }.Build();
        Assert.Equal(2, mesh.Bounds.Max.Y, 1e-9);
        Assert.Equal(4, mesh.Bounds.Max.Z - mesh.Bounds.Min.Z, 1e-9);
    }

    // the file it is written as

    [Fact]
    public void SurvivesARoundTrip()
    {
        var was = new Shape { Kind = ShapeKind.Cube, Width = 3 }.Build();
        var now = Mesh.FromJson(was.ToJson())!;

        Assert.Equal(was.Faces.Count, now.Faces.Count);
        Assert.Equal(was.Positions[0].X, now.Positions[0].X, 1e-9);
    }

    [Fact]
    public void AFaceNamingAPointThatIsNotThereIsLeftOut()
    {
        var mesh = Mesh.FromJson(
            new OrderedDictionary<string, object?>
            {
                ["positions"] = new List<object?> { 0L, 0L, 0L, 1L, 0L, 0L, 0L, 1L, 0L },
                ["faces"] = new List<object?>
                {
                    new OrderedDictionary<string, object?> { ["v"] = new List<object?> { 0L, 1L, 2L } },
                    new OrderedDictionary<string, object?> { ["v"] = new List<object?> { 0L, 1L, 99L } },
                },
            })!;

        Assert.Single(mesh.Faces);
    }

    [Fact]
    public void IsNotReadFromSomethingThatIsNotOne()
    {
        Assert.Null(Mesh.FromJson("nonsense"));
        Assert.Null(Mesh.FromJson(new OrderedDictionary<string, object?> { ["faces"] = new List<object?>() }));
    }

    [Fact]
    public void AShapeSurvivesARoundTripToo()
    {
        var was = new Shape { Kind = ShapeKind.Stairs, Steps = 12, Width = 4 };
        var now = Shape.FromJson(was.ToJson())!;

        Assert.Equal(ShapeKind.Stairs, now.Kind);
        Assert.Equal(12, now.Steps);
        Assert.Equal(4.0, now.Width);
    }
}

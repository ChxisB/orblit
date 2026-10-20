using Orblit.VectorMath;

namespace Orblit.Collide.Tests;

public class CollideTests
{
    private static Vector3 V(double x, double y = 0, double z = 0) => new(x, y, z);

    private static Vector2 P(double x, double y) => new(x, y);

    // shapes touching

    [Fact]
    public void TwoSpheresApartAreNotTouching() =>
        Assert.Null(Shapes.Contact(new Sphere(V(0), 1), new Sphere(V(5), 1)));

    [Fact]
    public void TwoSpheresOverlappingSayHowFarAndWhichWay()
    {
        var found = Shapes.Contact(new Sphere(V(0), 1), new Sphere(V(1.5), 1))!;
        Assert.Equal(0.5, found.Depth, 1e-9);
        Assert.Equal(-1, found.Normal.X, 1e-9);
    }

    [Fact]
    public void TwoSpheresExactlyOnTopOfEachOtherPickAStableWayOut()
    {
        // No direction exists, and a random one would jitter for as long as
        // they stayed there.
        var found = Shapes.Contact(new Sphere(V(0), 1), new Sphere(V(0), 1))!;
        Assert.Equal(1, found.Normal.Length, 1e-9);
        Assert.Equal(found.Normal, Shapes.Contact(new Sphere(V(0), 1), new Sphere(V(0), 1))!.Normal);
    }

    [Fact]
    public void ASphereOnABoxFaceComesOutThroughTheFace()
    {
        var found = Shapes.Contact(new Sphere(V(0, 1.4), 0.5), new Box(V(0), V(2, 2, 2)))!;
        Assert.Equal(1, found.Normal.Y, 1e-9);
        Assert.Equal(0.1, found.Depth, 1e-9);
    }

    [Fact]
    public void ASphereInsideABoxComesOutTheNearestFace()
    {
        // The centre is inside, so there is no direction in the offset. The
        // shortest way out has to be found from the faces.
        var found = Shapes.Contact(new Sphere(V(0, 0.9), 0.2), new Box(V(0), V(4, 2, 4)))!;
        Assert.Equal(1, found.Normal.Y, 1e-9);
    }

    [Fact]
    public void TwoBoxesComeApartAlongTheAxisTheyOverlapLeastOn()
    {
        // Any other axis is a longer way out, and pushing along it is what
        // makes a stack of boxes explode.
        var found = Shapes.Contact(new Box(V(0, 1.8), V(2, 2, 2)), new Box(V(0), V(6, 2, 6)))!;
        Assert.Equal(1, found.Normal.Y, 1e-9);
        Assert.Equal(0.2, found.Depth, 1e-9);
    }

    [Fact]
    public void ACapsuleStandingOnABoxTouchesThroughItsFoot()
    {
        var standing = new Capsule(V(0, 1.4), height: 2, radius: 0.4);
        var ground = new Box(V(0), V(10, 1, 10));
        var found = Shapes.Contact(standing, ground)!;
        Assert.Equal(1, found.Normal.Y, 1e-9);
    }

    [Fact]
    public void ACapsuleBesideAWallSlidesRatherThanCatching()
    {
        // What a capsule is for: the contact normal is along the wall's own
        // axis whatever height the body is at.
        var wall = new Box(V(2, 0), V(1, 10, 10));
        foreach (var height in new[] { -0.5, 0.0, 0.5, 0.9 })
        {
            var body = new Capsule(V(1.2, height), height: 2, radius: 0.4);
            var found = Shapes.Contact(body, wall);
            Assert.True(found is not null, $"at {height}");
            Assert.Equal(-1, found.Normal.X, 1e-6);
        }
    }

    [Fact]
    public void TwoCapsulesCrossingFindTheirNearestPoints()
    {
        var a = new Capsule(V(0), height: 4, radius: 0.5);
        var b = new Capsule(V(0.8), height: 4, radius: 0.5);
        Assert.NotNull(Shapes.Contact(a, b));
        Assert.Equal(0.2, Shapes.Contact(a, b)!.Depth, 1e-6);
    }

    [Fact]
    public void TheAnswerIsTheSameWhicheverWayRoundItIsAsked()
    {
        var a = new Sphere(V(0, 1.4), 0.5);
        var b = new Box(V(0), V(2, 2, 2));
        var forwards = Shapes.Contact(a, b)!;
        var backwards = Shapes.Contact(b, a)!;

        Assert.Equal(forwards.Depth, backwards.Depth, 1e-9);
        Assert.Equal(-forwards.Normal.Y, backwards.Normal.Y, 1e-9);
    }

    [Fact]
    public void OverlapsAgreesWithContact()
    {
        var pairs = new (Shape, Shape)[]
        {
            (new Sphere(V(0), 1), new Sphere(V(1.5), 1)),
            (new Sphere(V(0), 1), new Sphere(V(9), 1)),
            (new Box(V(0), V(2, 2, 2)), new Sphere(V(1.2), 0.5)),
            (new Capsule(V(0), height: 2, radius: 0.5), new Sphere(V(0.8), 0.4)),
        };
        foreach (var (a, b) in pairs)
        {
            Assert.Equal(Shapes.Contact(a, b) is not null, Shapes.Overlaps(a, b));
        }
    }

    // rays

    [Fact]
    public void ARayDownAnAxisHitsASphereWhereItShould()
    {
        var hit = Queries.Raycast(new Ray(V(-10), V(1)), new Sphere(V(0), 1))!;
        Assert.Equal(9, hit.Distance, 1e-9);
        Assert.Equal(-1, hit.Normal.X, 1e-9);
        Assert.False(hit.Inside);
    }

    [Fact]
    public void ARayPointingAwayFromASphereMissesIt() =>
        Assert.Null(Queries.Raycast(new Ray(V(10), V(1)), new Sphere(V(0), 1)));

    [Fact]
    public void ARayFromInsideReportsThatAndTheFarWall()
    {
        // A camera inside geometry and one looking at it are otherwise the same
        // hit at nothing, and they want opposite handling.
        var hit = Queries.Raycast(new Ray(V(0), V(1)), new Sphere(V(0), 2))!;
        Assert.True(hit.Inside);
        Assert.Equal(2, hit.Distance, 1e-9);
    }

    [Fact]
    public void ARayPastItsOwnReachFindsNothing() =>
        Assert.Null(Queries.Raycast(new Ray(V(-10), V(1), distance: 5), new Sphere(V(0), 1)));

    [Fact]
    public void ABoxIsHitOnTheFaceTheRayReachesFirst()
    {
        var hit = Queries.Raycast(new Ray(V(-10, 0.2), V(1)), new Box(V(0), V(2, 2, 2)))!;
        Assert.Equal(9, hit.Distance, 1e-9);
        Assert.Equal(-1, hit.Normal.X, 1e-9);
    }

    [Fact]
    public void ARayParallelToABoxFaceEitherMissesOrPassesThrough()
    {
        Assert.NotNull(Queries.Raycast(new Ray(V(-10, 0), V(1)), new Box(V(0), V(2, 2, 2))));
        Assert.Null(Queries.Raycast(new Ray(V(-10, 5), V(1)), new Box(V(0), V(2, 2, 2))));
    }

    [Fact]
    public void ACapsuleIsHitOnItsSideAndOnItsCap()
    {
        var body = new Capsule(V(0), height: 4, radius: 0.5);
        var side = Queries.Raycast(new Ray(V(-10, 0), V(1)), body)!;
        Assert.Equal(9.5, side.Distance, 0.02);

        var cap = Queries.Raycast(new Ray(V(0, 10), V(0, -1)), body)!;
        Assert.Equal(8, cap.Distance, 0.02);
    }

    [Fact]
    public void ARayWithNoDirectionPointsSomewhereRatherThanAtNaN()
    {
        var ray = new Ray(V(0), Vector3.Zero());
        Assert.Equal(1, ray.Direction.Length, 1e-9);
    }

    [Fact]
    public void SeveralShapesComeBackNearestFirst()
    {
        var shapes = new Shape[]
        {
            new Sphere(V(20), 1),
            new Sphere(V(5), 1),
            new Box(V(12), V(2, 2, 2)),
        };
        var hits = Queries.RaycastAll(new Ray(V(-10), V(1)), shapes);

        Assert.Equal(new[] { 1, 2, 0 }, hits.Select(h => h.Index));
        Assert.Equal(1, Queries.RaycastFirst(new Ray(V(-10), V(1)), shapes)!.Value.Index);
    }

    // the broadphase

    [Fact]
    public void ItFindsThePairsThatAFullSweepWould()
    {
        // The claim of a broadphase: a faster route to the same answer.
        var shapes = new Shape[]
        {
            new Sphere(V(0), 1),
            new Sphere(V(1.2), 1),
            new Sphere(V(40), 1),
            new Box(V(1.5, 0.5), V(1, 1, 1)),
        };
        var grid = new Broadphase(cellSize: 4);
        foreach (var shape in shapes)
        {
            grid.Add(shape);
        }

        var byGrid = grid.Pairs().ToHashSet();
        var byHand = new HashSet<(int, int)>();
        for (var i = 0; i < shapes.Length; i++)
        {
            for (var j = i + 1; j < shapes.Length; j++)
            {
                if (shapes[i].Bounds.Overlaps(shapes[j].Bounds))
                {
                    byHand.Add((i, j));
                }
            }
        }

        Assert.Equal(byHand, byGrid);
        Assert.NotEmpty(byHand);
    }

    [Fact]
    public void AShapeSpanningSeveralCellsIsReportedOnceNotOnceACell()
    {
        // The classic fault, and it reads as being pushed apart four times as
        // hard as it should be.
        var grid = new Broadphase(cellSize: 1);
        grid.Add(new Box(V(0), V(8, 8, 8)));
        grid.Add(new Box(V(0), V(8, 8, 8)));

        Assert.Single(grid.Pairs());
    }

    [Fact]
    public void AQueryFindsWhatIsNearABox()
    {
        var grid = new Broadphase(cellSize: 4);
        grid.Add(new Sphere(V(0), 1));
        grid.Add(new Sphere(V(50), 1));

        Assert.Equal(new[] { 0 }, grid.Near(new Aabb(V(-2, -2, -2), V(2, 2, 2))));
    }

    // layers

    [Fact]
    public void EitherCaringIsEnough()
    {
        // A bullet that cares about walls should hit a wall that cares about
        // nothing; requiring both would mean every passive thing listing
        // everything that might touch it.
        var bullet = new Layers(@is: 2, cares: 1);
        var wall = new Layers(@is: 1, cares: 0);
        Assert.True(Layers.Interact(bullet, wall));
    }

    [Fact]
    public void TwoThingsThatIgnoreEachOtherDoNotInteract()
    {
        var a = new Layers(@is: 2, cares: 4);
        var b = new Layers(@is: 8, cares: 16);
        Assert.False(Layers.Interact(a, b));
    }

    // two dimensions

    [Fact]
    public void CirclesAndRectanglesTouchTheWayTheir3DCousinsDo()
    {
        Assert.Null(Hitboxes.Touching(new Circle(P(0, 0), 1), new Circle(P(5, 0), 1)));
        Assert.Equal(0.5, Hitboxes.Touching(new Circle(P(0, 0), 1), new Circle(P(1.5, 0), 1))!.Depth, 1e-9);
        Assert.Equal(
            1,
            Hitboxes.Touching(
                new Rectangle(P(0, 1.8), P(2, 2)),
                new Rectangle(P(0, 0), P(6, 2)))!.Normal.Y,
            1e-9);
    }

    [Fact]
    public void AWorldSaysWhatBeganWhatIsTouchingAndWhatEnded()
    {
        // Only the middle one is easy. A world that reported just the current
        // state would leave every caller keeping its own previous frame.
        var world = new World2(cellSize: 8);
        world.Add(new Body(id: 1, hitbox: new Circle(P(0, 0), 1)));
        world.Add(new Body(id: 2, hitbox: new Circle(P(5, 0), 1)));

        var step = world.Step();
        Assert.Empty(step.Touching);
        Assert.Empty(step.Began);

        world[2]!.Hitbox = new Circle(P(1.5, 0), 1);
        step = world.Step();
        Assert.Single(step.Began);
        Assert.Single(step.Touching);

        // Still touching is not beginning again.
        step = world.Step();
        Assert.Empty(step.Began);
        Assert.Single(step.Touching);

        world[2]!.Hitbox = new Circle(P(9, 0), 1);
        step = world.Step();
        Assert.Equal(new[] { (1L, 2L) }, step.Ended);
        Assert.Empty(step.Touching);
    }

    [Fact]
    public void APassthroughBodyReportsItsTouchAndStopsNothing()
    {
        var world = new World2();
        world.Add(new Body(id: 1, hitbox: new Circle(P(0, 0), 1)));
        world.Add(new Body(id: 2, hitbox: new Circle(P(1.5, 0), 1), passthrough: true));

        var step = world.Step();
        Assert.Single(step.Touching);

        var before = world[1]!.Hitbox.Position.Clone();
        world.Separate(step.Touching);

        // A pickup that blocked the player would be the fault this prevents.
        Assert.Equal(before, world[1]!.Hitbox.Position);
    }

    [Fact]
    public void TwoSolidBodiesArePushedApartEvenly()
    {
        var world = new World2();
        world.Add(new Body(id: 1, hitbox: new Circle(P(0, 0), 1)));
        world.Add(new Body(id: 2, hitbox: new Circle(P(1.5, 0), 1)));

        world.Separate(world.Step().Touching);

        // A quarter each, so the order they were added in is not visible.
        Assert.Equal(-0.25, world[1]!.Hitbox.Position.X, 1e-9);
        Assert.Equal(1.75, world[2]!.Hitbox.Position.X, 1e-9);
        Assert.Empty(world.Step().Touching);
    }

    [Fact]
    public void LayersKeepTwoThingsFromSeeingEachOther()
    {
        var world = new World2();
        world.Add(new Body(id: 1, hitbox: new Circle(P(0, 0), 1), @is: 2, cares: 4));
        world.Add(new Body(id: 2, hitbox: new Circle(P(0.5, 0), 1), @is: 8, cares: 16));

        Assert.Empty(world.Step().Touching);
    }

    [Fact]
    public void APointPicksOutWhatIsUnderIt()
    {
        var world = new World2();
        world.Add(new Body(id: 1, hitbox: new Circle(P(0, 0), 1)));
        world.Add(new Body(id: 2, hitbox: new Rectangle(P(10, 0), P(4, 4))));

        Assert.Equal(new long[] { 1 }, world.At(P(0.5, 0)));
        Assert.Equal(new long[] { 2 }, world.At(P(10, 1)));
        Assert.Empty(world.At(P(50, 50)));
    }

    [Fact]
    public void ABodySpanningCellsIsReportedOnce()
    {
        var world = new World2(cellSize: 1);
        world.Add(new Body(id: 1, hitbox: new Rectangle(P(0, 0), P(8, 8))));
        world.Add(new Body(id: 2, hitbox: new Rectangle(P(0, 0), P(8, 8))));

        Assert.Single(world.Step().Touching);
    }
}

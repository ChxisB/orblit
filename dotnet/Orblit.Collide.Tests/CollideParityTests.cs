using System.Text.Json;
using Orblit.Parity;
using Orblit.VectorMath;

namespace Orblit.Collide.Tests;

public class CollideParityTests
{
    private static List<JsonElement> Cases(Fixture fixture, string op)
    {
        var cases = fixture.Cases.Where(c => c.GetProperty("op").GetString() == op).ToList();
        Assert.True(cases.Count > 0, $"the fixture has no {op} cases");
        return cases;
    }

    private static double Read(JsonElement spec, string name) => Fixture.Number(spec.GetProperty(name));

    private static double[] Numbers(JsonElement array) => [.. array.EnumerateArray().Select(Fixture.Number)];

    private static Vector3 Vec(JsonElement array)
    {
        var v = Numbers(array);
        return new Vector3(v[0], v[1], v[2]);
    }

    private static Vector2 Vec2(JsonElement array)
    {
        var v = Numbers(array);
        return new Vector2(v[0], v[1]);
    }

    private static double[] Triple(Vector3 v) => [v.X, v.Y, v.Z];

    private static double[] Pair(Vector2 v) => [v.X, v.Y];

    private static double[] Corners(Aabb box) => [.. Triple(box.Minimum), .. Triple(box.Maximum)];

    private static double[] Corners(Rect2 box) => [.. Pair(box.Minimum), .. Pair(box.Maximum)];

    private static double[] Flat(JsonElement corners) => [.. corners.EnumerateArray().SelectMany(Numbers)];

    private static void AssertClose(Fixture fixture, string what, double[] expected, double[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.True(
                Tolerance.Close(expected[i], actual[i]),
                $"{what}, value {i}: Dart {expected[i]:R}, C# {actual[i]:R}, from Dart commit {fixture.DartCommit}");
        }
    }

    private static void AssertSame<T>(Fixture fixture, string what, T expected, T actual) =>
        Assert.True(
            EqualityComparer<T>.Default.Equals(expected, actual),
            $"{what}: Dart {expected}, C# {actual}, from Dart commit {fixture.DartCommit}");

    private static void AssertSameList<T>(Fixture fixture, string what, IEnumerable<T> expected, IEnumerable<T> actual) =>
        Assert.True(
            expected.SequenceEqual(actual),
            $"{what}: Dart [{string.Join(", ", expected)}], C# [{string.Join(", ", actual)}], from Dart commit {fixture.DartCommit}");

    private static void AssertContact(Fixture fixture, string what, JsonElement expected, Contact? actual)
    {
        if (expected.ValueKind == JsonValueKind.Null)
        {
            Assert.True(actual is null, $"{what}: Dart found no contact, C# found {actual}, from Dart commit {fixture.DartCommit}");
            return;
        }

        Assert.True(actual is not null, $"{what}: Dart found a contact, C# found none, from Dart commit {fixture.DartCommit}");
        AssertClose(fixture, $"{what} point", Numbers(expected.GetProperty("point")), Triple(actual.Point));
        AssertClose(fixture, $"{what} normal", Numbers(expected.GetProperty("normal")), Triple(actual.Normal));
        AssertClose(fixture, $"{what} depth", [Read(expected, "depth")], [actual.Depth]);
    }

    private static void AssertTouch(Fixture fixture, string what, JsonElement expected, Touch? actual)
    {
        if (expected.ValueKind == JsonValueKind.Null)
        {
            Assert.True(actual is null, $"{what}: Dart found no touch, C# found {actual}, from Dart commit {fixture.DartCommit}");
            return;
        }

        Assert.True(actual is not null, $"{what}: Dart found a touch, C# found none, from Dart commit {fixture.DartCommit}");
        AssertClose(fixture, $"{what} point", Numbers(expected.GetProperty("point")), Pair(actual.Point));
        AssertClose(fixture, $"{what} normal", Numbers(expected.GetProperty("normal")), Pair(actual.Normal));
        AssertClose(fixture, $"{what} depth", [Read(expected, "depth")], [actual.Depth]);
    }

    private static void AssertHit(Fixture fixture, string what, JsonElement expected, Hit? actual)
    {
        if (expected.ValueKind == JsonValueKind.Null)
        {
            Assert.True(actual is null, $"{what}: Dart missed, C# hit at {actual?.Distance:R}, from Dart commit {fixture.DartCommit}");
            return;
        }

        Assert.True(actual is not null, $"{what}: Dart hit, C# missed, from Dart commit {fixture.DartCommit}");
        AssertClose(fixture, $"{what} distance", [Read(expected, "distance")], [actual.Distance]);
        AssertClose(fixture, $"{what} point", Numbers(expected.GetProperty("point")), Triple(actual.Point));
        AssertClose(fixture, $"{what} normal", Numbers(expected.GetProperty("normal")), Triple(actual.Normal));
        AssertSame(fixture, $"{what} inside", expected.GetProperty("inside").GetBoolean(), actual.Inside);
    }

    private static Shape ShapeFrom(JsonElement s)
    {
        var position = Vec(s.GetProperty("p"));
        return s.GetProperty("t").GetString() switch
        {
            "sphere" => new Sphere(position, Read(s, "r")),
            "box" => new Box(position, Vec(s.GetProperty("size"))),
            "capsule" => new Capsule(position, Read(s, "height"), Read(s, "radius")),
            var other => throw new InvalidOperationException($"unknown shape {other}"),
        };
    }

    private static Hitbox HitboxFrom(JsonElement s)
    {
        var position = Vec2(s.GetProperty("p"));
        return s.GetProperty("t").GetString() switch
        {
            "circle" => new Circle(position, Read(s, "r")),
            "rect" => new Rectangle(position, Vec2(s.GetProperty("size"))),
            var other => throw new InvalidOperationException($"unknown hitbox {other}"),
        };
    }

    private static Ray RayFrom(JsonElement s) =>
        new(Vec(s.GetProperty("from")), Vec(s.GetProperty("dir")), Read(s, "distance"));

    private static Aabb BoxFrom(JsonElement corners)
    {
        var pair = corners.EnumerateArray().ToList();
        return new Aabb(Vec(pair[0]), Vec(pair[1]));
    }

    private static Rect2 RectFrom(JsonElement corners)
    {
        var pair = corners.EnumerateArray().ToList();
        return new Rect2(Vec2(pair[0]), Vec2(pair[1]));
    }

    [Fact]
    public void EveryContactAgreesWithDart()
    {
        var fixture = Fixture.Load("orblit_collide", "collide");
        foreach (var spec in Cases(fixture, "contact"))
        {
            var a = ShapeFrom(spec.GetProperty("a"));
            var b = ShapeFrom(spec.GetProperty("b"));
            var what = $"{spec.GetProperty("a")} against {spec.GetProperty("b")}";
            AssertContact(fixture, $"{what} forward", spec.GetProperty("forward"), Shapes.Contact(a, b));
            AssertContact(fixture, $"{what} backward", spec.GetProperty("backward"), Shapes.Contact(b, a));
            AssertSame(fixture, $"{what} overlaps", spec.GetProperty("overlaps").GetBoolean(), Shapes.Overlaps(a, b));
        }
    }

    [Fact]
    public void EveryShapeProbeAgreesWithDart()
    {
        var fixture = Fixture.Load("orblit_collide", "collide");
        foreach (var spec in Cases(fixture, "shape"))
        {
            var shape = ShapeFrom(spec.GetProperty("s"));
            var q = Vec(spec.GetProperty("q"));
            var what = spec.GetProperty("s").ToString();
            AssertClose(fixture, $"{what} bounds", Flat(spec.GetProperty("bounds")), Corners(shape.Bounds));
            AssertClose(fixture, $"{what} nearest", Numbers(spec.GetProperty("nearest")), Triple(shape.NearestTo(q)));
            AssertSame(fixture, $"{what} contains", spec.GetProperty("contains").GetBoolean(), shape.Contains(q));
            var moved = shape.MovedTo(q);
            AssertClose(fixture, $"{what} moved", Numbers(spec.GetProperty("moved")), Triple(moved.Position));
            AssertClose(fixture, $"{what} movedBounds", Flat(spec.GetProperty("movedBounds")), Corners(moved.Bounds));
            if (spec.TryGetProperty("boxSize", out var size))
            {
                AssertClose(fixture, $"{what} size", Numbers(size), Triple(((Box)shape).Size));
            }

            if (spec.TryGetProperty("segment", out var segment))
            {
                var (low, high) = ((Capsule)shape).Segment;
                AssertClose(fixture, $"{what} segment", Flat(segment), [.. Triple(low), .. Triple(high)]);
            }
        }
    }

    [Fact]
    public void EveryBoxAgreesWithDart()
    {
        var fixture = Fixture.Load("orblit_collide", "collide");
        foreach (var spec in Cases(fixture, "aabb"))
        {
            var a = BoxFrom(spec.GetProperty("a"));
            var b = BoxFrom(spec.GetProperty("b"));
            var q = Vec(spec.GetProperty("q"));
            var what = spec.GetProperty("a").ToString();
            AssertClose(fixture, $"{what} centre", Numbers(spec.GetProperty("centreOf")), Triple(a.Centre));
            AssertClose(fixture, $"{what} size", Numbers(spec.GetProperty("sizeOf")), Triple(a.Size));
            AssertSame(fixture, $"{what} overlaps", spec.GetProperty("overlaps").GetBoolean(), a.Overlaps(b));
            AssertSame(fixture, $"{what} contains", spec.GetProperty("contains").GetBoolean(), a.Contains(q));
            AssertClose(fixture, $"{what} union", Flat(spec.GetProperty("union")), Corners(a.Union(b)));
            AssertClose(fixture, $"{what} grown", Flat(spec.GetProperty("grown")), Corners(a.GrownBy(Read(spec, "margin"))));
            var around = Aabb.Around(Vec(spec.GetProperty("centre")), Vec(spec.GetProperty("size")));
            AssertClose(fixture, $"{what} around", Flat(spec.GetProperty("around")), Corners(around));

            var nearest = spec.GetProperty("nearest");
            if (nearest.ValueKind == JsonValueKind.String)
            {
                Assert.ThrowsAny<ArgumentException>(() => a.NearestTo(q));
            }
            else
            {
                AssertClose(fixture, $"{what} nearest", Numbers(nearest), Triple(a.NearestTo(q)));
            }
        }
    }

    [Fact]
    public void EverySegmentAgreesWithDart()
    {
        var fixture = Fixture.Load("orblit_collide", "collide");
        foreach (var spec in Cases(fixture, "segments"))
        {
            var a0 = Vec(spec.GetProperty("a0"));
            var a1 = Vec(spec.GetProperty("a1"));
            var b0 = Vec(spec.GetProperty("b0"));
            var b1 = Vec(spec.GetProperty("b1"));
            var what = $"{spec.GetProperty("a0")} {spec.GetProperty("a1")} and {spec.GetProperty("b0")} {spec.GetProperty("b1")}";
            AssertClose(fixture, $"{what} on segment", Numbers(spec.GetProperty("onSegment")), Triple(Shapes.NearestOnSegment(a0, a1, b0)));
            var (pa, pb) = Shapes.NearestBetweenSegments(a0, a1, b0, b1);
            AssertClose(fixture, $"{what} between", Flat(spec.GetProperty("between")), [.. Triple(pa), .. Triple(pb)]);
        }
    }

    [Fact]
    public void EveryRayAgreesWithDart()
    {
        var fixture = Fixture.Load("orblit_collide", "collide");
        foreach (var spec in Cases(fixture, "ray"))
        {
            var ray = RayFrom(spec.GetProperty("ray"));
            var what = $"{spec.GetProperty("ray")} at {spec.GetProperty("s")}";
            AssertHit(fixture, what, spec.GetProperty("hit"), Queries.Raycast(ray, ShapeFrom(spec.GetProperty("s"))));
            AssertClose(fixture, $"{what} direction", Numbers(spec.GetProperty("direction")), Triple(ray.Direction));
            AssertClose(fixture, $"{what} at", Numbers(spec.GetProperty("at")), Triple(ray.At(Read(spec, "along"))));
        }
    }

    [Fact]
    public void EveryRayThroughManyShapesAgreesWithDart()
    {
        var fixture = Fixture.Load("orblit_collide", "collide");
        foreach (var spec in Cases(fixture, "rays"))
        {
            var ray = RayFrom(spec.GetProperty("ray"));
            var shapes = spec.GetProperty("shapes").EnumerateArray().Select(ShapeFrom).ToList();
            var what = spec.GetProperty("ray").ToString();

            var all = Queries.RaycastAll(ray, shapes);
            var expected = spec.GetProperty("all").EnumerateArray().ToList();
            AssertSame(fixture, $"{what} hits", expected.Count, all.Count);
            for (var i = 0; i < expected.Count; i++)
            {
                AssertSame(fixture, $"{what} hit {i} shape", expected[i].GetProperty("i").GetInt32(), all[i].Index);
                AssertHit(fixture, $"{what} hit {i}", expected[i].GetProperty("hit"), all[i].Hit);
            }

            var first = spec.GetProperty("first");
            var found = Queries.RaycastFirst(ray, shapes);
            if (first.ValueKind == JsonValueKind.Null)
            {
                Assert.True(found is null, $"{what}: Dart found no first hit, C# found one, from Dart commit {fixture.DartCommit}");
                continue;
            }

            Assert.True(found is not null, $"{what}: Dart found a first hit, C# found none, from Dart commit {fixture.DartCommit}");
            AssertSame(fixture, $"{what} first shape", first.GetProperty("i").GetInt32(), found.Value.Index);
            AssertHit(fixture, $"{what} first", first.GetProperty("hit"), found.Value.Hit);
        }
    }

    [Fact]
    public void EveryBroadphaseAgreesWithDart()
    {
        var fixture = Fixture.Load("orblit_collide", "collide");
        foreach (var spec in Cases(fixture, "broadphase"))
        {
            var grid = new Broadphase(Read(spec, "cell"));
            var indexes = new List<int>();
            foreach (var item in spec.GetProperty("items").EnumerateArray())
            {
                indexes.Add(item.GetProperty("t").GetString() == "bounds"
                    ? grid.AddBounds(new Aabb(Vec(item.GetProperty("min")), Vec(item.GetProperty("max"))))
                    : grid.Add(ShapeFrom(item)));
            }

            var what = $"cell {Read(spec, "cell")}, {indexes.Count} items";
            AssertSameList(fixture, $"{what} indexes", spec.GetProperty("indexes").EnumerateArray().Select(i => i.GetInt32()), indexes);
            AssertSame(fixture, $"{what} length", spec.GetProperty("length").GetInt32(), grid.Length);
            AssertSameList(
                fixture,
                $"{what} pairs",
                spec.GetProperty("pairs").EnumerateArray().Select(p => (p[0].GetInt32(), p[1].GetInt32())),
                grid.Pairs());

            var queries = spec.GetProperty("queries").EnumerateArray().ToList();
            var near = spec.GetProperty("near").EnumerateArray().ToList();
            for (var i = 0; i < queries.Count; i++)
            {
                var box = new Aabb(Vec(queries[i][0]), Vec(queries[i][1]));
                AssertSameList(fixture, $"{what} near {i}", near[i].EnumerateArray().Select(n => n.GetInt32()), grid.Near(box));
            }

            grid.Clear();
            AssertSame(fixture, $"{what} length after clear", spec.GetProperty("lengthAfterClear").GetInt32(), grid.Length);
            AssertSame(fixture, $"{what} pairs after clear", spec.GetProperty("pairsAfterClear").GetInt32(), grid.Pairs().Count);
        }
    }

    [Fact]
    public void EveryBoundWithNoCellIsRefusedAsDartRefusesIt()
    {
        var fixture = Fixture.Load("orblit_collide", "collide");
        foreach (var spec in Cases(fixture, "broadphaseRefuses"))
        {
            var bounds = new Aabb(Vec(spec.GetProperty("min")), Vec(spec.GetProperty("max")));
            var grid = new Broadphase();
            if (spec.GetProperty("throws").GetBoolean())
            {
                Assert.ThrowsAny<ArgumentException>(() => grid.AddBounds(bounds));
            }
            else
            {
                grid.AddBounds(bounds);
            }
        }
    }

    [Fact]
    public void EveryLayerPairAgreesWithDart()
    {
        var fixture = Fixture.Load("orblit_collide", "collide");
        foreach (var spec in Cases(fixture, "layers"))
        {
            var a = spec.GetProperty("a").EnumerateArray().Select(m => m.GetInt64()).ToList();
            var b = spec.GetProperty("b").EnumerateArray().Select(m => m.GetInt64()).ToList();
            AssertSame(
                fixture,
                $"layers {string.Join("/", a)} with {string.Join("/", b)}",
                spec.GetProperty("interact").GetBoolean(),
                Layers.Interact(new Layers(a[0], a[1]), new Layers(b[0], b[1])));
        }
    }

    [Fact]
    public void EveryFlatTouchAgreesWithDart()
    {
        var fixture = Fixture.Load("orblit_collide", "collide");
        foreach (var spec in Cases(fixture, "touching"))
        {
            var a = HitboxFrom(spec.GetProperty("a"));
            var b = HitboxFrom(spec.GetProperty("b"));
            var q = Vec2(spec.GetProperty("q"));
            var what = $"{spec.GetProperty("a")} against {spec.GetProperty("b")}";
            AssertTouch(fixture, $"{what} forward", spec.GetProperty("forward"), Hitboxes.Touching(a, b));
            AssertTouch(fixture, $"{what} backward", spec.GetProperty("backward"), Hitboxes.Touching(b, a));
            AssertClose(fixture, $"{what} bounds", Flat(spec.GetProperty("boundsA")), Corners(a.Bounds));
            AssertSame(fixture, $"{what} contains", spec.GetProperty("containsA").GetBoolean(), a.Contains(q));
            AssertClose(fixture, $"{what} position", Numbers(spec.GetProperty("positionA")), Pair(a.Position));
        }
    }

    [Fact]
    public void EveryFlatBoxAgreesWithDart()
    {
        var fixture = Fixture.Load("orblit_collide", "collide");
        foreach (var spec in Cases(fixture, "rect2"))
        {
            var a = RectFrom(spec.GetProperty("a"));
            var b = RectFrom(spec.GetProperty("b"));
            var q = Vec2(spec.GetProperty("q"));
            var what = spec.GetProperty("a").ToString();
            AssertClose(fixture, $"{what} centre", Numbers(spec.GetProperty("centreOf")), Pair(a.Centre));
            AssertClose(fixture, $"{what} size", Numbers(spec.GetProperty("sizeOf")), Pair(a.Size));
            AssertSame(fixture, $"{what} overlaps", spec.GetProperty("overlaps").GetBoolean(), a.Overlaps(b));
            AssertSame(fixture, $"{what} contains", spec.GetProperty("contains").GetBoolean(), a.Contains(q));
            var around = Rect2.Around(Vec2(spec.GetProperty("centre")), Vec2(spec.GetProperty("size")));
            AssertClose(fixture, $"{what} around", Flat(spec.GetProperty("around")), Corners(around));

            var nearest = spec.GetProperty("nearest");
            if (nearest.ValueKind == JsonValueKind.String)
            {
                Assert.ThrowsAny<ArgumentException>(() => a.NearestTo(q));
            }
            else
            {
                AssertClose(fixture, $"{what} nearest", Numbers(nearest), Pair(a.NearestTo(q)));
            }
        }
    }

    private static void AssertCollisions(Fixture fixture, string what, JsonElement expected, List<Collision> actual)
    {
        var list = expected.EnumerateArray().ToList();
        AssertSame(fixture, $"{what} count", list.Count, actual.Count);
        for (var i = 0; i < list.Count; i++)
        {
            AssertSame(fixture, $"{what} {i} first body", list[i].GetProperty("a").GetInt64(), actual[i].A);
            AssertSame(fixture, $"{what} {i} second body", list[i].GetProperty("b").GetInt64(), actual[i].B);
            AssertTouch(fixture, $"{what} {i}", list[i].GetProperty("touch"), actual[i].Touch);
        }
    }

    private static void AssertEnded(Fixture fixture, string what, JsonElement expected, List<(long, long)> actual) =>
        AssertSameList(
            fixture,
            what,
            expected.EnumerateArray().Select(p => (p[0].GetInt64(), p[1].GetInt64())),
            actual);

    private static void AssertBodies(Fixture fixture, string what, JsonElement expected, World2 world)
    {
        var list = expected.EnumerateArray().ToList();
        AssertSame(fixture, $"{what} length", list.Count, world.Length);
        foreach (var body in list)
        {
            var id = body.GetProperty("id").GetInt64();
            var found = world[id];
            Assert.True(found is not null, $"{what}: body {id} is missing, from Dart commit {fixture.DartCommit}");
            AssertClose(fixture, $"{what} body {id}", Numbers(body.GetProperty("p")), Pair(found.Hitbox.Position));
        }
    }

    private static void AssertStep(Fixture fixture, string what, JsonElement expected, World2 world, bool separate)
    {
        var step = world.Step();
        if (separate)
        {
            world.Separate(step.Touching);
        }

        AssertCollisions(fixture, $"{what} touching", expected.GetProperty("touching"), step.Touching);
        AssertCollisions(fixture, $"{what} began", expected.GetProperty("began"), step.Began);
        AssertEnded(fixture, $"{what} ended", expected.GetProperty("ended"), step.Ended);
        AssertBodies(fixture, what, expected.GetProperty("bodies"), world);
    }

    private static void Replay(Fixture fixture, JsonElement spec)
    {
        var world = new World2(Read(spec, "cell"));
        var ops = spec.GetProperty("ops").EnumerateArray().ToList();
        var log = spec.GetProperty("log").EnumerateArray().ToList();
        for (var i = 0; i < ops.Count; i++)
        {
            var op = ops[i];
            var what = $"world op {i} ({op.GetProperty("op")})";
            switch (op.GetProperty("op").GetString())
            {
                case "add":
                    var body = op.GetProperty("body");
                    // F2: the body's own fields, called by name.
                    world.Add(new Body(
                        body.GetProperty("id").GetInt64(),
                        HitboxFrom(body.GetProperty("hitbox")),
                        passthrough: body.GetProperty("passthrough").GetBoolean(),
                        @is: body.GetProperty("is").GetInt64(),
                        cares: body.GetProperty("cares").GetInt64()));
                    break;
                case "move":
                    var moved = world[op.GetProperty("id").GetInt64()]
                        ?? throw new InvalidOperationException($"{what} moves a body that is not there");
                    moved.Hitbox = HitboxFrom(op.GetProperty("hitbox"));
                    break;
                case "remove":
                    world.Remove(op.GetProperty("id").GetInt64());
                    break;
                case "step":
                    AssertStep(fixture, what, log[i], world, op.GetProperty("separate").GetBoolean());
                    break;
                case "at":
                    AssertSameList(
                        fixture,
                        what,
                        log[i].EnumerateArray().Select(id => id.GetInt64()),
                        world.At(Vec2(op.GetProperty("p"))));
                    break;
                case "inside":
                    var area = new Rect2(Vec2(op.GetProperty("min")), Vec2(op.GetProperty("max")));
                    AssertSameList(fixture, what, log[i].EnumerateArray().Select(id => id.GetInt64()), world.Inside(area));
                    break;
                default:
                    throw new InvalidOperationException($"unknown world op {op}");
            }
        }
    }

    [Fact]
    public void EveryWorldAgreesWithDart()
    {
        var fixture = Fixture.Load("orblit_collide", "collide");
        foreach (var spec in Cases(fixture, "world"))
        {
            Replay(fixture, spec);
        }
    }
}

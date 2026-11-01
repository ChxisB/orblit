using System.Collections;
using Orblit.Json;
using Orblit.Scene;

namespace Orblit.Motion.Tests;

public class ClipScopeTests
{
    [Fact]
    public void PlayedOnAnInstanceNamesItsPartsByThePrefabsIds()
    {
        var scope = new ClipScope("street1/lamp3", instance: "street1/lamp3");
        Assert.Equal("street1/lamp3", scope.Resolve(string.Empty));
        Assert.Equal("street1/lamp3/bulb", scope.Resolve("bulb"));
        Assert.Equal("street1/lamp3/bulb/glow", scope.Resolve("bulb/glow"));

        Assert.Equal(string.Empty, scope.TargetOf("street1/lamp3"));
        Assert.Equal("bulb", scope.TargetOf("street1/lamp3/bulb"));
        Assert.Equal("bulb/glow", scope.TargetOf("street1/lamp3/bulb/glow"));
    }

    [Fact]
    public void HasNoNameForWhatIsOutsideTheInstance()
    {
        var scope = new ClipScope("street1/lamp3", instance: "street1/lamp3");
        Assert.Null(scope.TargetOf("street1"));
        Assert.Null(scope.TargetOf("door"));
        Assert.Null(scope.TargetOf("street1/lamp30/bulb"));
    }

    [Fact]
    public void PlayedOnAPartNamesThePrefabRootByItsIdWhenKnown()
    {
        var scope = new ClipScope("street1/lamp3/bulb", instance: "street1/lamp3", root: "lamp");
        Assert.Equal("street1/lamp3/bulb", scope.Resolve(string.Empty));
        Assert.Equal("street1/lamp3", scope.Resolve("lamp"));
        Assert.Equal("street1/lamp3/flame", scope.Resolve("flame"));
        Assert.Equal("lamp", scope.TargetOf("street1/lamp3"));
        Assert.Equal(string.Empty, scope.TargetOf("street1/lamp3/bulb"));

        var unknown = new ClipScope("street1/lamp3/bulb", instance: "street1/lamp3");
        Assert.Null(unknown.TargetOf("street1/lamp3"));
    }

    [Fact]
    public void PlayedOutsideAnyInstanceNamesThingsByTheScenesIds()
    {
        var scope = new ClipScope("door");
        Assert.Equal("door", scope.Resolve(string.Empty));
        Assert.Equal("street1/lamp3", scope.Resolve("street1/lamp3"));
        Assert.Equal(string.Empty, scope.TargetOf("door"));
        Assert.Equal("street1/lamp3/bulb", scope.TargetOf("street1/lamp3/bulb"));
    }

    [Fact]
    public void IsWorkedOutFromWhereTheOwnerIsInTheScene()
    {
        var document = ClipSceneTests.Street();

        var lamp = ClipScope.InScene(document, "street1/lamp3");
        Assert.Equal("street1/lamp3", lamp.Instance);

        var bulb = ClipScope.InScene(document, "street1/lamp3/bulb");
        Assert.Equal("street1/lamp3", bulb.Instance);
        Assert.Equal("street1/lamp3/bulb", bulb.Resolve(string.Empty));

        var door = ClipScope.InScene(document, "door");
        Assert.Null(door.Instance);
    }
}

public class ClipSceneTests
{
    [Fact]
    public void AFrameInASceneIsTheChangesThatMakeTheSceneLookLikeIt()
    {
        var document = Street();
        var frame = new ClipFrame(0.5);
        frame.Values[string.Empty] = new OrderedDictionary<string, object>
        {
            ["transform.position"] = new Vector3(1, 2, 3),
            ["transform.rotation"] = Quaternion.AxisAngle(new Vector3(0, 1, 0), Doubles.Radians(30)),
        };
        frame.Values["bulb"] = new OrderedDictionary<string, object>
        {
            ["transform.scale"] = new Vector3(2, 2, 2),
        };

        var ops = LampOps(frame, document);

        Assert.Equal(3, ops.Count);
        var position = ops[0];
        Assert.Equal("street1/lamp3", position.Id);
        Assert.Equal("transform", position.Type);
        Assert.Equal("position", position.Field);
        Assert.Equal([0.0, 0.0, 0.0], Numbers(position.From));
        Assert.Equal([1.0, 2.0, 3.0], Numbers(position.To));

        // A transform is written in degrees, so a rotation becomes them.
        var rotation = ops[1];
        Assert.Equal("rotation", rotation.Field);
        var degrees = Numbers(rotation.To);
        Assert.Equal(0, degrees[0], 1e-9);
        Assert.Equal(30, degrees[1], 1e-9);
        Assert.Equal(0, degrees[2], 1e-9);

        Assert.Equal("street1/lamp3/bulb", ops[2].Id);
        Assert.Equal("scale", ops[2].Field);
    }

    [Fact]
    public void CanBePutBackExactly()
    {
        var document = Street();
        var frame = new ClipFrame(0);
        frame.Values["bulb"] = new OrderedDictionary<string, object>
        {
            ["transform.position"] = new Vector3(0, 3, 0),
        };
        var ops = LampOps(frame, document);

        var shown = document;
        foreach (var op in ops)
        {
            shown = op.ApplyTo(shown);
        }

        var bulb = Assert.IsType<TransformComponent>(shown["street1/lamp3/bulb"]![SceneComponents.Transform]);
        Assert.Equal(new Vector3(0, 3, 0), bulb.Position);

        var back = shown;
        foreach (var op in ops.Reverse())
        {
            back = op.Inverse.ApplyTo(back);
        }

        Assert.Equal(
            JsonText.Encode(document["street1/lamp3/bulb"]!.ToJson()),
            JsonText.Encode(back["street1/lamp3/bulb"]!.ToJson()));
    }

    [Fact]
    public void LeavesOutWhatTheSceneAlreadyHasOrDoesNotHaveAtAll()
    {
        var document = Street();
        var frame = new ClipFrame(0);
        frame.Values["bulb"] = new OrderedDictionary<string, object>
        {
            // Where the bulb already is.
            ["transform.position"] = Vector3.Zero(),

            // A lamp without a light: nothing to dim.
            ["light.power"] = 20.0,

            // Not a component and a field at all.
            ["power"] = 20.0,
        };

        // A part this lamp does not have.
        frame.Values["flame"] = new OrderedDictionary<string, object>
        {
            ["transform.scale"] = new Vector3(2, 2, 2),
        };

        Assert.Empty(LampOps(frame, document));
    }

    [Fact]
    public void LeavesBonesToWhoeverDrawsTheModel()
    {
        var document = Street();
        var frame = new ClipFrame(0);
        frame.Bones[string.Empty] = new OrderedDictionary<string, BoneLocal>
        {
            ["hips"] = new BoneLocal(position: new Vector3(0, 1, 0)),
        };
        Assert.Empty(ClipScene.OpsFor(frame, document, new ClipScope("door")));
    }

    [Fact]
    public void WritesARotationAnywhereElseAsItsFourNumbers()
    {
        var document = new SceneDocument(
            entities:
            [
                new SceneEntity(
                    "door",
                    "Door",
                    components: new OrderedDictionary<string, SceneComponent>
                    {
                        ["hinge"] = new UnknownComponent("hinge", Tree.Map(("turn", Tree.List(0L, 0L, 0L, 1L)))),
                    }),
            ]);
        var frame = new ClipFrame(0);
        frame.Values[string.Empty] = new OrderedDictionary<string, object>
        {
            ["hinge.turn"] = Quaternion.AxisAngle(new Vector3(0, 1, 0), Doubles.Radians(90)),
        };

        var ops = ClipScene.OpsFor(frame, document, new ClipScope("door"));
        var turn = Numbers(Assert.Single(ops).To);
        Assert.Equal(4, turn.Count);
        Assert.Equal(0.7071067811865476, turn[1], 1e-12);
    }

    internal static SceneDocument Street() => new(
        entities:
        [
            Entity("street1", prefab: true),
            Entity("street1/lamp3", parent: "street1", prefab: true),
            Entity("street1/lamp3/bulb", parent: "street1/lamp3"),
            Entity("door"),
        ]);

    private static SceneEntity Entity(string id, string? parent = null, bool prefab = false)
    {
        var components = new OrderedDictionary<string, SceneComponent>
        {
            [SceneComponents.Transform] = new TransformComponent(),
        };
        if (prefab)
        {
            components[SceneComponents.Prefab] = new PrefabComponent(asset: "lamp.oscene");
        }

        return new SceneEntity(id, id, parent: parent, components: components);
    }

    private static IReadOnlyList<SetField> LampOps(ClipFrame frame, SceneDocument document) =>
        ClipScene.OpsFor(frame, document, ClipScope.InScene(document, "street1/lamp3"));

    private static List<double> Numbers(object? list) =>
        [.. Assert.IsAssignableFrom<IEnumerable>(list).Cast<object>().Select(Convert.ToDouble)];
}

using Orblit.Core;
using Orblit.Json;

namespace Orblit.Remote.Tests;

public sealed class CoreWorldInspectorTests : IDisposable
{
    private readonly World _world = new();
    private readonly ComponentType _position;
    private readonly ComponentType _tag;
    private readonly ComponentType _health;
    private readonly TransformComponents _transforms;
    private readonly CoreWorldInspector _inspector;
    private readonly long _a;
    private readonly long _b;

    public CoreWorldInspectorTests()
    {
        _position = _world.RegisterComponent("Position", ComponentKind.Float32, 3);
        _tag = _world.RegisterComponent("Tag", ComponentKind.UInt8, 2);
        _health = _world.RegisterComponent("Health", ComponentKind.Int32);
        _transforms = _world.RegisterTransforms();
        var a = _world.CreateEntity();
        _world.Add(a, _position, [1f, 2f, 3f]);
        _world.Add(a, _transforms.Local, Transforms.Local(x: 4));
        var b = _world.CreateEntity();
        _world.Add(b, _position);
        _world.Add(b, _tag);
        _world.Add(b, _health, [10]);
        (_a, _b) = ((long)a, (long)b);
        _inspector = new CoreWorldInspector(
            _world,
            [_position, _tag, _health, _transforms.Local],
            [Described("Position", "x", "y", "z"), Described("Health", "hp")]);
    }

    public void Dispose() => _world.Dispose();

    private static Dictionary<string, object?> Described(string name, params string[] fields) =>
        new() { ["name"] = name, ["fields"] = fields.Cast<object?>().ToList() };

    private string Fields(long id, string component) => JsonText.Encode(_inspector.Read(id)![component]);

    private Span<float> Floats(long id, ComponentType type)
    {
        Assert.True(_world.TryGetFloat32((ulong)id, type, out var floats));
        return floats;
    }

    [Fact]
    public void EntitiesListEveryEntityWithItsComponentNamesSorted()
    {
        var rows = _inspector.Entities([]);

        Assert.Equal([_a, _b], rows.Select(row => row.Id));
        Assert.Equal(["Position", "orblit.LocalTransform"], rows[0].Components);
        Assert.Equal(["Health", "Position", "Tag"], rows[1].Components);
    }

    [Fact]
    public void EntitiesAreNarrowedByTheComponentsAskedFor()
    {
        Assert.Equal([_b], _inspector.Entities(["Tag"]).Select(row => row.Id));
        Assert.Equal(2, _inspector.Entities(["Position"]).Count);
        Assert.Single(_inspector.Entities(["Position", "Tag"]));
    }

    [Fact]
    public void EntitiesRefuseAComponentNobodyToldTheInspectorAbout() =>
        Assert.Throws<ArgumentException>(() => _inspector.Entities(["Nope"]));

    [Fact]
    public void EntitiesShowAComponentTheInspectorWasNotGivenAsItsId()
    {
        var other = _world.RegisterComponent("Other", ComponentKind.Int32);
        _world.Add((ulong)_a, other);

        Assert.Contains($"#{other.Id}", _inspector.Entities([])[0].Components);
    }

    [Fact]
    public void ReadNamesTheFieldsOfADescribedComponent()
    {
        var read = _inspector.Read(_a)!;

        Assert.Equal("""{"x":1.0,"y":2.0,"z":3.0}""", Fields(_a, "Position"));
        Assert.Equal(4.0, read["orblit.LocalTransform"]["x"]);
        Assert.Equal(1.0, read["orblit.LocalTransform"]["rotationW"]);
        Assert.Equal(10, read["orblit.LocalTransform"].Count);
    }

    [Fact]
    public void ReadSaysHowAnUndescribedComponentIsLaidOut()
    {
        Assert.Equal("""{"undescribed":"uint8 x2"}""", Fields(_b, "Tag"));
        Assert.Equal("""{"hp":10}""", Fields(_b, "Health"));
    }

    [Fact]
    public void ReadLeavesOutAComponentTheEntityLacks() => Assert.False(_inspector.Read(_a)!.ContainsKey("Tag"));

    [Fact]
    public void ReadAnswersNullForAnEntityThatIsGone()
    {
        _world.DestroyEntity((ulong)_a);

        Assert.Null(_inspector.Read(_a));
    }

    [Fact]
    public void ReadShowsAValueAFloatCannotHoldAsText()
    {
        Floats(_a, _position)[0] = float.NaN;
        Floats(_a, _position)[1] = float.PositiveInfinity;
        Floats(_a, _position)[2] = float.NegativeInfinity;

        Assert.Equal("""{"x":"NaN","y":"Infinity","z":"-Infinity"}""", Fields(_a, "Position"));
    }

    [Fact]
    public void WriteSetsOneFieldAndLeavesTheOthers()
    {
        _inspector.Write(_a, "Position", "y", 9);

        Assert.Equal([1f, 9f, 3f], Floats(_a, _position).ToArray());
        Assert.Equal(9.0, _inspector.Read(_a)!["Position"]["y"]);
    }

    [Fact]
    public void WriteSetsAFieldOfABuiltInTransform()
    {
        _inspector.Write(_a, "orblit.LocalTransform", "scaleY", 2);

        Assert.Equal(2f, Floats(_a, _transforms.Local)[8]);
    }

    [Fact]
    public void WriteSetsAWholeNumberInAnIntegerColumn()
    {
        _inspector.Write(_b, "Health", "hp", 7);

        Assert.Equal("""{"hp":7}""", Fields(_b, "Health"));
    }

    [Fact]
    public void WriteRefusesAFractionInAnIntegerColumn()
    {
        Assert.Throws<ArgumentException>(() => _inspector.Write(_b, "Health", "hp", 2.5));
        Assert.Equal("""{"hp":10}""", Fields(_b, "Health"));
    }

    [Fact]
    public void WriteRefusesAFieldOfAComponentWithNoNames() =>
        Assert.Throws<ArgumentException>(() => _inspector.Write(_b, "Tag", "x", 1));

    [Fact]
    public void WriteRefusesAFieldNobodyNamedAndAComponentTheEntityLacks()
    {
        Assert.Throws<ArgumentException>(() => _inspector.Write(_a, "Position", "w", 1));
        Assert.Throws<ArgumentException>(() => _inspector.Write(_a, "Health", "hp", 1));
        Assert.Throws<ArgumentException>(() => _inspector.Write(_a, "Nope", "x", 1));
    }

    [Fact]
    public void WriteRefusesAnEntityThatIsGone()
    {
        _world.DestroyEntity((ulong)_a);

        Assert.Throws<DeadEntityException>(() => _inspector.Write(_a, "Position", "x", 1));
    }

    [Fact]
    public void ADescriptionThatDoesNotMatchTheLayoutIsRefused() =>
        Assert.Throws<ArgumentException>(() =>
            new CoreWorldInspector(_world, [_position], [Described("Position", "x", "y")]));
}

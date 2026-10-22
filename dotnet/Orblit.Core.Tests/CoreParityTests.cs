using System.Text.Json;
using Orblit.Parity;

namespace Orblit.Core.Tests;

/// <summary>
/// Builds in C# the world the core fixture exporter builds, from
/// the same steps, and compares what each side reads back. Both call the
/// same C++, so this checks the wrapper: handles, layouts, views and counts.
/// </summary>
public sealed class CoreParityTests : IDisposable
{
    private readonly World _world = new();

    public void Dispose() => _world.Dispose();

    [Fact]
    public void ABuiltWorldReadsBackAsTheFixtureBuiltIt()
    {
        var fixture = Fixture.Load("orblit_core", "core");
        var (handles, position, velocity, t, propagated) = Build();
        var why = $"from fixture commit {fixture.SourceCommit}";

        var header = fixture.Cases[0];
        Assert.Equal(header.GetProperty("entityCount").GetInt32(), _world.EntityCount);
        Assert.Equal(header.GetProperty("componentCount").GetInt32(), _world.ComponentCount);
        Assert.Equal(header.GetProperty("version").GetUInt64(), _world.Version);
        Assert.Equal(header.GetProperty("propagated").GetInt32(), propagated);
        Assert.True(Tolerance.Close(header.GetProperty("elapsed").GetDouble(), _world.Elapsed), why);

        var rows = fixture.Cases.Skip(1).ToList();
        Assert.Equal(handles.Count, rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            Assert.Equal(row.GetProperty("id").GetUInt64(), handles[i]);
            Assert.Equal(row.GetProperty("alive").GetBoolean(), _world.IsAlive(handles[i]));
            AssertFloats(row.GetProperty("position"), _world.Float32Of(handles[i], position), $"position {i} {why}");
            AssertFloats(row.GetProperty("velocity"), _world.Float32Of(handles[i], velocity), $"velocity {i} {why}");
            AssertFloats(row.GetProperty("world"), _world.Float32Of(handles[i], t.World), $"world {i} {why}");
        }
    }

    private static void AssertFloats(JsonElement expected, float[]? actual, string label)
    {
        if (expected.ValueKind == JsonValueKind.Null)
        {
            Assert.Null(actual);
            return;
        }

        Assert.NotNull(actual);
        Assert.Equal(expected.GetArrayLength(), actual.Length);
        for (var i = 0; i < actual.Length; i++)
        {
            Assert.True(Tolerance.Close(expected[i].GetDouble(), actual[i]), $"{label}, element {i}");
        }
    }

    private (List<ulong> Handles, ComponentType Position, ComponentType Velocity, TransformComponents T, int Propagated) Build()
    {
        var t = _world.RegisterTransforms();
        var position = _world.RegisterComponent("Position", ComponentKind.Float32, 3);
        var velocity = _world.RegisterComponent("Velocity", ComponentKind.Float32, 3);

        var handles = new List<ulong>();
        for (var i = 0; i < 40; i++)
        {
            var entity = _world.CreateEntity();
            var turned = i % 3 == 0;
            _world.Add(
                entity,
                t.Local,
                Transforms.Local(
                    x: i,
                    y: i % 5 * 0.5f,
                    z: -i * 0.25f,
                    rotationY: turned ? 0.7071068f : 0,
                    rotationW: turned ? 0.7071068f : 1,
                    scaleX: 1 + (i % 4 * 0.5f)));
            _world.Add(entity, t.World);
            if (i % 2 == 1)
            {
                _world.Add(entity, t.Parent, [(long)handles[i - 1]]);
            }

            _world.Add(entity, position, [i, 0f, 0f]);
            _world.Add(entity, velocity, [i * 0.5f, 1f, 2f]);
            handles.Add(entity);
        }

        for (var i = 0; i < handles.Count; i += 7)
        {
            _world.DestroyEntity(handles[i]);
        }

        for (var i = 0; i < 5; i++)
        {
            var entity = _world.CreateEntity();
            _world.Add(entity, t.Local, Transforms.Local(x: 100f + i));
            _world.Add(entity, t.World);
            _world.Add(entity, position);
            handles.Add(entity);
        }

        for (var i = 0; i < 40; i += 5)
        {
            if (_world.IsAlive(handles[i]))
            {
                _world.Remove(handles[i], velocity);
            }
        }

        var propagated = _world.PropagateTransforms();
        _world.Tick(0.25);
        _world.Tick(0.25);
        return (handles, position, velocity, t, propagated);
    }
}

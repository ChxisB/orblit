namespace Orblit.Core.Tests;

public sealed class WorldTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly World _world = new();
    private readonly ComponentType _position;
    private readonly ComponentType _velocity;

    public WorldTests(ITestOutputHelper output)
    {
        _output = output;
        _position = _world.RegisterComponent("Position", ComponentKind.Float32, 3);
        _velocity = _world.RegisterComponent("Velocity", ComponentKind.Float32, 3);
    }

    public void Dispose() => _world.Dispose();

    [Fact]
    public void ComponentsRegisterWithDistinctIds()
    {
        Assert.NotEqual(0u, _position.Id);
        Assert.NotEqual(_position.Id, _velocity.Id);
        Assert.Equal(2, _world.ComponentCount);
    }

    [Fact]
    public void ReRegisteringAnIdenticalLayoutReturnsTheSameType()
    {
        var again = _world.RegisterComponent("Position", ComponentKind.Float32, 3);
        Assert.Equal(_position.Id, again.Id);
    }

    [Fact]
    public void ReRegisteringADifferentLayoutIsRefused() =>
        Assert.Throws<ArgumentException>(() => _world.RegisterComponent("Position", ComponentKind.Int32));

    [Fact]
    public void EntitiesAreCreatedAliveAndCounted()
    {
        var entity = _world.CreateEntity();
        Assert.NotEqual(0ul, entity);
        Assert.True(_world.IsAlive(entity));
        Assert.Equal(1, _world.EntityCount);
    }

    [Fact]
    public void AStaleHandleStaysDeadOnceItsSlotIsReused()
    {
        var first = _world.CreateEntity();
        _world.DestroyEntity(first);
        Assert.False(_world.IsAlive(first));

        var second = _world.CreateEntity();
        Assert.True(_world.IsAlive(second));
        // The generation is what makes this detectable rather than an alias.
        Assert.NotEqual(first, second);
        Assert.False(_world.IsAlive(first), "the old handle must not address the new entity");
    }

    [Fact]
    public void UsingADeadHandleIsRefusedRatherThanSilentlyIgnored()
    {
        var entity = _world.CreateEntity();
        _world.DestroyEntity(entity);
        Assert.Throws<DeadEntityException>(() => _world.Add(entity, _position));
    }

    [Fact]
    public void AddReadBackRemove()
    {
        var entity = _world.CreateEntity();
        _world.Add(entity, _position, [1f, 2f, 3f]);

        Assert.True(_world.Has(entity, _position));
        Assert.Equal([1f, 2f, 3f], _world.Float32Of(entity, _position)!);

        Assert.True(_world.Remove(entity, _position));
        Assert.False(_world.Has(entity, _position));
        Assert.Null(_world.Float32Of(entity, _position));
    }

    [Fact]
    public void AComponentWithNoValueStartsZeroed()
    {
        var entity = _world.CreateEntity();
        _world.Add(entity, _position);
        Assert.Equal([0f, 0f, 0f], _world.Float32Of(entity, _position)!);
    }

    [Fact]
    public void AddingTwiceIsRefused()
    {
        var entity = _world.CreateEntity();
        _world.Add(entity, _position);
        Assert.Throws<InvalidOperationException>(() => _world.Add(entity, _position));
    }

    [Fact]
    public void OtherComponentsSurviveARemoval()
    {
        var entity = _world.CreateEntity();
        _world.Add(entity, _position, [7f, 8f, 9f]);
        _world.Add(entity, _velocity, [1f, 1f, 1f]);

        _world.Remove(entity, _velocity);
        Assert.Equal([7f, 8f, 9f], _world.Float32Of(entity, _position)!);
    }

    [Fact]
    public void QueriesMatchOnlyEntitiesCarryingEveryComponent()
    {
        for (var i = 0; i < 10; i++)
        {
            var entity = _world.CreateEntity();
            _world.Add(entity, _position);
            if (i % 2 == 0)
            {
                _world.Add(entity, _velocity);
            }
        }

        using var query = _world.Query(_position, _velocity);
        Assert.Equal(5, query.EntityCount);
    }

    [Fact]
    public void QueriesRefreshAfterAStructuralChange()
    {
        var entity = _world.CreateEntity();
        _world.Add(entity, _position);
        _world.Add(entity, _velocity);

        using var query = _world.Query(_position, _velocity);
        Assert.Equal(1, query.EntityCount);

        _world.Remove(entity, _velocity);
        Assert.Equal(0, query.EntityCount);
    }

    [Fact]
    public void AWriteThroughAColumnLandsOnTheRightEntity()
    {
        var entities = Enumerable.Range(0, 4).Select(_ => _world.CreateEntity()).ToList();
        for (var i = 0; i < entities.Count; i++)
        {
            _world.Add(entities[i], _position, [i, 0f, 0f]);
        }

        using var query = _world.Query(_position);

        foreach (var chunk in query.Chunks)
        {
            var values = chunk.Float32(0);
            var handles = chunk.Entities;
            for (var row = 0; row < chunk.Length; row++)
            {
                // Prove the entity list and the column agree row for row.
                var expected = entities.IndexOf(handles[row]);
                Assert.Equal((float)expected, values[row * 3]);
                values[row * 3] = expected + 100;
            }
        }

        for (var i = 0; i < entities.Count; i++)
        {
            Assert.Equal((float)(i + 100), _world.Float32Of(entities[i], _position)![0]);
        }
    }

    [Fact]
    public void AChunkReportsComponentsTheQueryDidNotAskFor()
    {
        var entity = _world.CreateEntity();
        _world.Add(entity, _position, [1f, 2f, 3f]);
        _world.Add(entity, _velocity, [4f, 5f, 6f]);

        // Queried on position alone, but the run still knows it carries velocity,
        // which is how replication decides what to send.
        using var query = _world.Query(_position);

        var chunk = query.Chunks.First();
        Assert.Contains(_position.Id, chunk.ComponentIds);
        Assert.Contains(_velocity.Id, chunk.ComponentIds);
        Assert.True(chunk.TryGetFloat32(_velocity, out var column));
        Assert.Equal([4f, 5f, 6f], column.ToArray());
    }

    [Fact]
    public void AComponentTheRunDoesNotCarryReadsAsAbsent()
    {
        var entity = _world.CreateEntity();
        _world.Add(entity, _position);
        using var query = _world.Query(_position);

        Assert.False(query.Chunks.First().TryGetFloat32(_velocity, out _));
    }

    [Fact]
    public void AskingForTheWrongElementTypeIsRefused()
    {
        var entity = _world.CreateEntity();
        _world.Add(entity, _position);
        using var query = _world.Query(_position);

        Assert.Throws<ArgumentException>(() => query.Chunks.First().Int32(0));
    }

    [Fact]
    public void ASystemMovesTenThousandEntitiesInOneCrossing()
    {
        const int count = 10000;
        var entities = new List<ulong>();
        for (var i = 0; i < count; i++)
        {
            var entity = _world.CreateEntity();
            _world.Add(entity, _position, [i, 0f, 0f]);
            _world.Add(entity, _velocity, [2f, 0f, 0f]);
            entities.Add(entity);
        }

        Assert.Equal(count, _world.EntityCount);

        using var query = _world.Query(_position, _velocity);

        // Every entity shares one archetype, so the whole world is one run: the
        // system touches the boundary to fetch the columns and then works
        // entirely in managed code over the engine's own memory.
        Assert.Equal(1, query.Chunks.Count);

        const float delta = 0.5f;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        foreach (var chunk in query.Chunks)
        {
            var positions = chunk.Float32(0);
            var velocities = chunk.Float32(1);
            for (var i = 0; i < positions.Length; i++)
            {
                positions[i] += velocities[i] * delta;
            }
        }

        stopwatch.Stop();

        // Correctness first: x started at i and should have advanced by 1.
        for (var i = 0; i < count; i += 997)
        {
            Assert.Equal(i + 1, _world.Float32Of(entities[i], _position)![0], 1e-3f);
        }

        // Reported rather than asserted. A timing bound would be flaky on shared
        // hardware, but a regression into per-entity calls would show here as
        // orders of magnitude.
        _output.WriteLine($"10k entities advanced in {stopwatch.Elapsed.TotalMicroseconds:F0}us (one boundary crossing)");
    }

    [Fact]
    public void UseAfterDisposeIsRefusedRatherThanCrashing()
    {
        var other = new World();
        other.Dispose();
        Assert.True(other.IsDisposed);
        Assert.Throws<InvalidOperationException>(() => other.CreateEntity());
        other.Dispose();
    }

    [Fact]
    public void ADisposedQueryIsRefused()
    {
        var query = _world.Query(_position);
        query.Dispose();
        Assert.Throws<InvalidOperationException>(() => query.EntityCount);
    }

    [Fact]
    public void TickAccumulatesElapsedTime()
    {
        _world.Tick(0.25);
        _world.Tick(0.25);
        Assert.Equal(0.5, _world.Elapsed, 1e-9);
    }
}

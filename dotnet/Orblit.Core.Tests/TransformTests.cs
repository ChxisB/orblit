using System.Runtime.InteropServices;

namespace Orblit.Core.Tests;

public sealed class TransformTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly World _world = new();
    private readonly TransformComponents _t;

    public TransformTests(ITestOutputHelper output)
    {
        _output = output;
        _t = _world.RegisterTransforms();
    }

    public void Dispose() => _world.Dispose();

    [Fact]
    public void RegisteringTwiceReturnsTheSameComponents()
    {
        var again = _world.RegisterTransforms();
        Assert.Equal(_t.Local.Id, again.Local.Id);
        Assert.Equal(_t.World.Id, again.World.Id);
        Assert.Equal(_t.Parent.Id, again.Parent.Id);
    }

    [Fact]
    public void ARootTakesItsLocalTransformAsItsWorldTransform()
    {
        var entity = Spawn(Transforms.Local(x: 1, y: 2, z: 3));
        Assert.Equal(1, _world.PropagateTransforms());

        var matrix = _world.Float32Of(entity, _t.World)!;
        Assert.Equal([1f, 2f, 3f], TranslationOf(matrix));
        // No rotation and unit scale, so the basis is the identity.
        Assert.Equal(1f, matrix[0]);
        Assert.Equal(1f, matrix[5]);
        Assert.Equal(1f, matrix[10]);
        Assert.Equal(1f, matrix[15]);
    }

    [Fact]
    public void ScaleReachesTheBasisNotTheTranslation()
    {
        var entity = Spawn(Transforms.Local(x: 5, scaleX: 2, scaleY: 3, scaleZ: 4));
        _world.PropagateTransforms();

        var matrix = _world.Float32Of(entity, _t.World)!;
        Assert.Equal(2f, matrix[0]);
        Assert.Equal(3f, matrix[5]);
        Assert.Equal(4f, matrix[10]);
        Assert.Equal([5f, 0f, 0f], TranslationOf(matrix));
    }

    [Fact]
    public void AChildIsPlacedInItsParentsSpace()
    {
        var parent = Spawn(Transforms.Local(x: 10));
        var child = Spawn(Transforms.Local(x: 1), parent);

        Assert.Equal(2, _world.PropagateTransforms());
        Assert.Equal([11f, 0f, 0f], TranslationOf(_world.Float32Of(child, _t.World)!));
    }

    [Fact]
    public void AParentsRotationTurnsWhereItsChildEndsUp()
    {
        // A quarter turn about Y sends local +X to world -Z.
        var parent = Spawn(TurnY(MathF.PI / 2));
        var child = Spawn(Transforms.Local(x: 1), parent);
        _world.PropagateTransforms();

        var position = TranslationOf(_world.Float32Of(child, _t.World)!);
        Assert.Equal(0f, position[0], 1e-5f);
        Assert.Equal(-1f, position[2], 1e-5f);
    }

    [Fact]
    public void AChainComposesDownItsWholeDepth()
    {
        var previous = Spawn(Transforms.Local(x: 1));
        for (var i = 0; i < 9; i++)
        {
            previous = Spawn(Transforms.Local(x: 1), previous);
        }

        Assert.Equal(10, _world.PropagateTransforms());
        Assert.Equal(10f, TranslationOf(_world.Float32Of(previous, _t.World)!)[0]);
    }

    [Fact]
    public void ReparentingIsAComponentWriteNotARebuild()
    {
        var left = Spawn(Transforms.Local(x: 100));
        var right = Spawn(Transforms.Local(x: 200));
        var child = Spawn(Transforms.Local(x: 1), left);

        _world.PropagateTransforms();
        Assert.Equal(101f, TranslationOf(_world.Float32Of(child, _t.World)!)[0]);

        Assert.True(_world.TryGetBytes(child, _t.Parent, out var bytes));
        MemoryMarshal.Cast<byte, long>(bytes)[0] = (long)right;
        _world.PropagateTransforms();
        Assert.Equal(201f, TranslationOf(_world.Float32Of(child, _t.World)!)[0]);
    }

    [Fact]
    public void AnEntityWithNoParentComponentIsARoot()
    {
        var entity = Spawn(Transforms.Local(y: 7));
        Assert.False(_world.Has(entity, _t.Parent));
        _world.PropagateTransforms();
        Assert.Equal([0f, 7f, 0f], TranslationOf(_world.Float32Of(entity, _t.World)!));
    }

    [Fact]
    public void AParentThatHasBeenDestroyedIsIgnoredRatherThanFollowed()
    {
        var parent = Spawn(Transforms.Local(x: 10));
        var child = Spawn(Transforms.Local(x: 1), parent);
        _world.DestroyEntity(parent);

        _world.PropagateTransforms();
        Assert.Equal(
            1f,
            TranslationOf(_world.Float32Of(child, _t.World)!)[0]);
    }

    [Fact]
    public void ACycleTerminatesInsteadOfHanging()
    {
        var a = Spawn(Transforms.Local(x: 1));
        var b = Spawn(Transforms.Local(x: 1), a);
        // Close the loop: a's parent becomes b.
        _world.Add(a, _t.Parent, [(long)b]);

        _world.PropagateTransforms();
    }

    [Fact]
    public void ADeepHierarchyResolvesEachAncestorOnce()
    {
        // One chain of 200, plus 200 leaves hanging off its end. A naive walk would
        // re-resolve the chain per leaf; memoisation makes it one pass.
        var trunk = Spawn(Transforms.Local(x: 1));
        for (var i = 0; i < 199; i++)
        {
            trunk = Spawn(Transforms.Local(x: 1), trunk);
        }

        for (var i = 0; i < 200; i++)
        {
            Spawn(Transforms.Local(y: 1), trunk);
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Assert.Equal(400, _world.PropagateTransforms());
        stopwatch.Stop();

        _output.WriteLine($"400 transforms over a 200-deep chain in {stopwatch.Elapsed.TotalMicroseconds:F0}us");
        Assert.Equal(200f, TranslationOf(_world.Float32Of(trunk, _t.World)!)[0]);
    }

    [Fact]
    public void TransformsAreAQueryLikeAnythingElse()
    {
        for (var i = 0; i < 5; i++)
        {
            Spawn(Transforms.Local(x: i));
        }

        _world.PropagateTransforms();

        using var query = _world.Query(_t.World);

        var total = 0f;
        foreach (var chunk in query.Chunks)
        {
            var matrices = chunk.Float32(0);
            for (var row = 0; row < chunk.Length; row++)
            {
                total += matrices[(row * 16) + 12];
            }
        }

        Assert.Equal(0f + 1 + 2 + 3 + 4, total);
    }

    /// <summary>
    /// A quaternion for a rotation about Y, so a test can express a turn without
    /// a maths dependency.
    /// </summary>
    private static float[] TurnY(float radians) =>
        Transforms.Local(rotationY: MathF.Sin(radians / 2), rotationW: MathF.Cos(radians / 2));

    /// <summary>The translation column of a column-major 4x4.</summary>
    private static float[] TranslationOf(float[] matrix) => [matrix[12], matrix[13], matrix[14]];

    private ulong Spawn(float[] local, ulong? parent = null)
    {
        var entity = _world.CreateEntity();
        _world.Add(entity, _t.Local, local);
        _world.Add(entity, _t.World);
        if (parent is { } p)
        {
            _world.Add(entity, _t.Parent, [(long)p]);
        }

        return entity;
    }
}

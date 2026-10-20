namespace Orblit.Codegen.Tests;

/// <summary>
/// Dart parses what it generates. Here the generated C# goes through the compiler, which reads more than a
/// parser does: a name that does not resolve fails too.
/// </summary>
public class GeneratedCodeTests
{
    private static string Registry(string source) => Compiled.Of(source).Source("OrblitComponents.g.cs");

    [Fact]
    public void RegistersEachComponentWithItsInferredLayout()
    {
        var code = Registry("""
            [OrblitComponent(Replicated = true, OwnerWritable = true)]
            public class Velocity { public float X, Y, Z; }
            """);

        Assert.Contains("public sealed class GameComponents", code);
        Assert.Contains("Velocity = world.RegisterComponent(\"Velocity\", ComponentKind.Float32, 3);", code);
        Assert.Contains("new HashSet<string> { \"Velocity\" }", code);
        Assert.Contains("public readonly ref struct VelocityColumn", code);
    }

    [Fact]
    public void OffsetsAColumnAccessorByTheFieldPosition()
    {
        var code = Registry("""
            [OrblitComponent]
            public class Position { public float X, Y, Z; }
            """);

        Assert.Contains("public float X(int row) => _data[row * 3];", code);
        Assert.Contains("public float Z(int row) => _data[row * 3 + 2];", code);
        Assert.Contains("public void SetZ(int row, float value) => _data[row * 3 + 2] = value;", code);
    }

    [Fact]
    public void ParsesIncludingTheInitialiserListWhichSubstringsMiss()
    {
        var compiled = Compiled.Of("""
            [OrblitComponent(Replicated = true, OwnerWritable = true)]
            public class Velocity { public float X, Y, Z; }
            [OrblitComponent]
            public class Health { public int Current; }
            [OrblitComponent(Kind = ComponentKind.Int64)]
            public class NetworkId { public long Value; }
            """);

        Assert.Empty(compiled.Errors);
        Assert.Contains("OrblitComponents.g.cs", compiled.Generated);
    }

    [Fact]
    public void ParsesWithASingleComponent()
    {
        var compiled = Compiled.Of("[OrblitComponent] public class Only { public float A; }");
        Assert.Empty(compiled.Errors);
        Assert.Contains("OrblitComponents.g.cs", compiled.Generated);
    }

    [Fact]
    public void ParsesWithNoComponentsAtAll() =>
        Assert.Empty(Compiled.Raw(ComponentEmitter.Registry("Game", "C", "game", [])).Errors);

    [Fact]
    public void APackageWithNoComponentsStillGeneratesSomethingUsable()
    {
        var code = ComponentEmitter.Registry("Game", "Empty", "game", []);
        Assert.Contains("public Empty(World world)", code);
        Assert.Contains("All => [];", code);
    }
}

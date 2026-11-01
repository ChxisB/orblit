using Microsoft.CodeAnalysis;
using Orblit.Core;

namespace Orblit.Codegen.Tests;

/// <summary>What no fixture covers: the build pipeline, and running the code it writes.</summary>
public class AddedTests
{
    private const string Pair = """
        [OrblitComponent(Replicated = true, OwnerWritable = true)]
        public class Velocity { public float X, Y, Z; }
        [OrblitComponent(Name = "Hp", Kind = ComponentKind.Int64)]
        public class Health { public long Current; }
        """;

    private static T Read<T>(object registry, string property) =>
        (T)registry.GetType().GetProperty(property)!.GetValue(registry)!;

    [Fact]
    public void RegistrationRunsAgainstAWorld()
    {
        var type = Compiled.Of(Pair).TypeOf("Game.GameComponents");
        using var world = new World();
        var registry = Activator.CreateInstance(type, world)!;

        var velocity = Read<ComponentType>(registry, "Velocity");
        Assert.Equal(("Velocity", ComponentKind.Float32, 3), (velocity.Name, velocity.Kind, velocity.Arity));
        var health = Read<ComponentType>(registry, "Health");
        Assert.Equal(("Hp", ComponentKind.Int64, 1), (health.Name, health.Kind, health.Arity));

        Assert.Equal(new[] { "Hp", "Velocity" }, Read<IReadOnlyList<ComponentType>>(registry, "All").Select(c => c.Name));
        Assert.Equal(new[] { "Velocity" }, Read<IReadOnlyList<ComponentType>>(registry, "Replicated").Select(c => c.Name));
        Assert.Equal(new[] { "Velocity" }, (IReadOnlySet<string>)type.GetProperty("OwnerWritable")!.GetValue(null)!);
    }

    [Fact]
    public void TheManifestConstantIsWhatTheEmitterWrites()
    {
        var compiled = Compiled.Of(Pair);
        var written = (string)compiled.TypeOf("Game.GameComponents").GetField("Manifest")!.GetRawConstantValue()!;
        Assert.Equal(ComponentEmitter.Manifest("Game", compiled.Scan().Components), written);
    }

    [Fact]
    public void TheManifestRecordsPathsFromTheProjectFolder()
    {
        var compiled = Compiled.Of(Pair, projectDir: "/work/game/", path: "/work/game/lib/Position.cs");
        var written = (string)compiled.TypeOf("Game.GameComponents").GetField("Manifest")!.GetRawConstantValue()!;
        Assert.Contains("\"source\": \"lib/Position.cs\"", written);
        Assert.DoesNotContain("/work/game", written);
    }

    [Fact]
    public void ARefusedDeclarationIsReportedAndNothingIsWritten()
    {
        var compiled = Compiled.Of("""
            [OrblitComponent]
            public class Body { public float Mass; public int Layer; }
            [OrblitComponent]
            public class Fine { public float A; }
            """);

        var report = Assert.Single(compiled.Reported);
        Assert.Equal("ORBLIT001", report.Id);
        Assert.Equal(DiagnosticSeverity.Error, report.Severity);
        Assert.Contains("Body", report.GetMessage());
        Assert.Contains("Split it into two", report.GetMessage());
        Assert.DoesNotContain("OrblitComponents.g.cs", compiled.Generated);
    }

    [Fact]
    public void AnAssemblyWithNoComponentsWritesOnlyTheAttribute()
    {
        var compiled = Compiled.Of("public class Plain { public float X; }");
        Assert.Equal(new[] { "OrblitComponentAttribute.g.cs" }, compiled.Generated);
        Assert.Empty(compiled.Errors);
    }

    [Theory]
    [InlineData("Float32", "float32")]
    [InlineData("Float64", "float64")]
    [InlineData("Int32", "int32")]
    [InlineData("UInt32", "uint32")]
    [InlineData("Int64", "int64")]
    [InlineData("UInt8", "uint8")]
    public void AnExplicitKindIsReadBackByItsManifestName(string member, string kind)
    {
        var result = Compiled.Of($"[OrblitComponent(Kind = ComponentKind.{member})] public class C {{ public string A = \"\"; }}").Scan();
        Assert.Equal(kind, Assert.Single(result.Components).Kind);
    }

    [Theory]
    [InlineData("float", "float32")]
    [InlineData("double", "float64")]
    [InlineData("int", "int32")]
    [InlineData("uint", "uint32")]
    [InlineData("long", "int64")]
    [InlineData("byte", "uint8")]
    [InlineData("bool", "uint8")]
    public void EveryDefaultKindIsInferredFromTheFieldType(string type, string kind)
    {
        var result = Compiled.Of($"[OrblitComponent] public class C {{ public {type} A; }}").Scan();
        Assert.Equal(kind, Assert.Single(result.Components).Kind);
    }

    [Fact]
    public void AutoPropertiesAreNotFields()
    {
        var result = Compiled.Of("""
            [OrblitComponent]
            public class Position { public float X { get; set; } }
            """).Scan();
        Assert.Contains("no instance fields", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void AStructAndANestedTypeAreComponentsToo()
    {
        var compiled = Compiled.Of("""
            [OrblitComponent]
            public struct Spin { public float Angle; }
            public static class Outer
            {
                [OrblitComponent]
                public class Inner { public int A; }
            }
            """);
        Assert.Empty(compiled.Errors);
        var written = (string)compiled.TypeOf("Game.GameComponents").GetField("Manifest")!.GetRawConstantValue()!;
        Assert.Contains("\"name\": \"Inner\"", written);
        Assert.Contains("\"name\": \"Spin\"", written);
    }

    [Fact]
    public void AColumnReadsAndWritesAtTheOffset()
    {
        var compiled = Compiled.Of("""
            [OrblitComponent]
            public class Position { public float X, Y, Z; }
            namespace Game
            {
                public static class Probe
                {
                    public static float Run()
                    {
                        var data = new float[6];
                        var column = new PositionColumn(data);
                        column.SetZ(1, 9f);
                        column.SetX(0, 2f);
                        return column.Length * 100 + data[5] * 10 + data[0];
                    }
                }
            }
            """);
        Assert.Empty(compiled.Errors);
        Assert.Equal(292f, compiled.TypeOf("Game.Probe").GetMethod("Run")!.Invoke(null, null));
    }

    [Fact]
    public void ReplicatedAloneIsNotOwnerWritable()
    {
        var compiled = Compiled.Of("[OrblitComponent(Replicated = true)] public class Seen { public float A; }");
        var written = (string)compiled.TypeOf("Game.GameComponents").GetField("Manifest")!.GetRawConstantValue()!;
        Assert.Contains("\"ownerWritable\": false", written);
        Assert.Empty((IReadOnlySet<string>)compiled.TypeOf("Game.GameComponents").GetProperty("OwnerWritable")!.GetValue(null)!);
    }

    [Fact]
    public void OwnerWritableWithoutReplicatedStaysOutOfTheSet()
    {
        var compiled = Compiled.Of("[OrblitComponent(OwnerWritable = true)] public class Local { public float A; }");
        var type = compiled.TypeOf("Game.GameComponents");
        Assert.Empty((IReadOnlySet<string>)type.GetProperty("OwnerWritable")!.GetValue(null)!);
        Assert.DoesNotContain("ownerWritable", (string)type.GetField("Manifest")!.GetRawConstantValue()!);
    }

    [Fact]
    public void AnExplicitKindBeatsTheOneTheFieldTypeGives()
    {
        var result = Compiled.Of("[OrblitComponent(Kind = ComponentKind.Float64)] public class C { public float A; }").Scan();
        Assert.Equal("float64", Assert.Single(result.Components).Kind);
    }

    [Fact]
    public void ASetterIsNamedFromAFieldWithALowerCaseFirstLetter()
    {
        var compiled = Compiled.Of("""
            [OrblitComponent]
            public class Motion { public float speed; }
            namespace Game
            {
                public static class Probe
                {
                    public static float Run()
                    {
                        var data = new float[2];
                        var column = new MotionColumn(data);
                        column.SetSpeed(1, 4f);
                        return column.speed(1);
                    }
                }
            }
            """);
        Assert.Empty(compiled.Errors);
        Assert.Equal(4f, compiled.TypeOf("Game.Probe").GetMethod("Run")!.Invoke(null, null));
    }

    [Theory]
    [InlineData("Float32", "float")]
    [InlineData("Float64", "double")]
    [InlineData("Int32", "int")]
    [InlineData("UInt32", "uint")]
    [InlineData("Int64", "long")]
    [InlineData("UInt8", "byte")]
    public void EveryKindIsRegisteredAndHasAColumnOverItsOwnType(string member, string element)
    {
        var compiled = Compiled.Of($$"""
            [OrblitComponent(Kind = ComponentKind.{{member}})]
            public class Cell { public {{element}} speed; }
            namespace Game
            {
                public static class Probe
                {
                    public static int Run() => new CellColumn(new {{element}}[4]).Length;
                }
            }
            """);
        Assert.Empty(compiled.Errors);

        var assembly = compiled.Load();
        using var world = new World();
        var registry = Activator.CreateInstance(assembly.GetType("Game.GameComponents")!, world)!;
        Assert.Equal(Enum.Parse<ComponentKind>(member), Read<ComponentType>(registry, "Cell").Kind);
        Assert.Equal(4, assembly.GetType("Game.Probe")!.GetMethod("Run")!.Invoke(null, null));
    }
}

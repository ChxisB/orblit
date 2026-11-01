namespace Orblit.Codegen.Tests;

public class ScanningTests
{
    [Fact]
    public void InfersFloat32FromDoubleFields()
    {
        // The C# type that stores as float32 is float. A C# double stores as float64.
        var result = Compiled.Of("""
            [OrblitComponent]
            public class Position
            {
                public float X, Y, Z;
            }
            """).Scan();

        Assert.Empty(result.Errors);
        var component = Assert.Single(result.Components);
        Assert.Equal("Position", component.Name);
        Assert.Equal("float32", component.Kind);
        Assert.Equal(3, component.Arity);
        Assert.Equal(new[] { "X", "Y", "Z" }, component.Fields);
    }

    [Fact]
    public void ReadsSeveralVariablesFromOneDeclaration()
    {
        var result = Compiled.Of("""
            [OrblitComponent]
            public class Velocity { public float X, Y, Z; }
            """).Scan();
        Assert.Equal(new[] { "X", "Y", "Z" }, Assert.Single(result.Components).Fields);
    }

    [Fact]
    public void InfersInt32FromIntAndUint8FromBool()
    {
        var result = Compiled.Of("""
            [OrblitComponent]
            public class Health { public int Current; public int Max; }

            [OrblitComponent]
            public class Flags { public bool Visible; }
            """).Scan();
        Assert.Equal(new[] { "uint8", "int32" }, result.Components.Select(c => c.Kind));
    }

    [Fact]
    public void AnExplicitNameAndKindWinOverWhatIsInferred()
    {
        var result = Compiled.Of("""
            [OrblitComponent(Name = "WorldMatrix", Kind = ComponentKind.Float64)]
            public class Matrix { public double A, B; }
            """).Scan();
        var component = Assert.Single(result.Components);
        Assert.Equal("WorldMatrix", component.Name);
        Assert.Equal("Matrix", component.TypeName);
        Assert.Equal("float64", component.Kind);
    }

    [Fact]
    public void CarriesReplicationFlagsThrough()
    {
        var result = Compiled.Of("""
            [OrblitComponent(Replicated = true, OwnerWritable = true)]
            public class Input { public float Throttle; }
            """).Scan();
        var component = Assert.Single(result.Components);
        Assert.True(component.Replicated);
        Assert.True(component.OwnerWritable);
    }

    [Fact]
    public void IgnoresClassesWithoutTheAnnotation()
    {
        var result = Compiled.Of("public class Plain { public float X; }").Scan();
        Assert.Empty(result.Components);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void IgnoresStaticFields()
    {
        // A const is static too.
        var result = Compiled.Of("""
            [OrblitComponent]
            public class Position
            {
                public const int Stride = 3;
                public static readonly int Other = 4;
                public float X, Y, Z;
            }
            """).Scan();
        Assert.Equal(new[] { "X", "Y", "Z" }, Assert.Single(result.Components).Fields);
    }

    [Fact]
    public void ComponentsComeBackSortedSoAManifestIsReproducible()
    {
        var result = Compiled.Of("""
            [OrblitComponent]
            public class Zeta { public float A; }
            [OrblitComponent]
            public class Alpha { public float A; }
            """).Scan();
        Assert.Equal(new[] { "Alpha", "Zeta" }, result.Components.Select(c => c.Name));
    }
}

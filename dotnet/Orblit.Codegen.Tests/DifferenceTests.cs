using Orblit.Core;

namespace Orblit.Codegen.Tests;

/// <summary>Where the C# generator gives a different answer from the Dart one on purpose. PORTING.md has the reasons.</summary>
public class DifferenceTests
{
    [Fact]
    public void ADoubleIsFloat64AndNotFloat32()
    {
        var result = Compiled.Of("[OrblitComponent] public class C { public double A; }").Scan();
        Assert.Equal("float64", Assert.Single(result.Components).Kind);
    }

    [Fact]
    public void ANameWithAQuoteStillCompiles()
    {
        // The Dart emitter writes the name between single quotes unescaped, so it breaks the generated file.
        var compiled = Compiled.Of("""
            [OrblitComponent(Name = "It's \"odd\"")]
            public class Odd { public float A; }
            """);
        Assert.Empty(compiled.Errors);

        var type = compiled.TypeOf("Game.GameComponents");
        using var world = new World();
        var registry = Activator.CreateInstance(type, world)!;
        Assert.Equal("It's \"odd\"", ((ComponentType)type.GetProperty("Odd")!.GetValue(registry)!).Name);
    }

    [Fact]
    public void ComponentsWithOneNameKeepTheirOrderPastThirtyTwo()
    {
        // Dart's List.sort is stable up to 32 items. Past that it may reorder equal names (decision 75).
        var found = Enumerable.Range(0, 40)
            .Select(i => new ComponentDeclaration("Same", $"C{i}", "float32", ["a"], replicated: false, ownerWritable: false, "lib/a.cs"))
            .ToList();
        Assert.Equal(found.Select(c => c.TypeName), new ScanResult(found, []).Components.Select(c => c.TypeName));
    }

    [Fact]
    public void PathsInTheManifestUseForwardSlashesOnEverySystem() =>
        Assert.Equal("lib/sub/A.cs", ComponentScanner.Relative("C:\\work\\game\\", "C:\\work\\game\\lib\\sub\\A.cs"));
}

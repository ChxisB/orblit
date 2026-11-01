namespace Orblit.Codegen.Tests;

public class RefusalsTests
{
    [Fact]
    public void MixedFieldTypesAreRefusedWithAWayForward()
    {
        var result = Compiled.Of("""
            [OrblitComponent]
            public class Body { public float Mass; public int Layer; }
            """).Scan();
        Assert.Empty(result.Components);
        Assert.Contains("Split it into two", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void AComponentWithNoFieldsIsRefused()
    {
        var result = Compiled.Of("""
            [OrblitComponent]
            public class Marker { }
            """).Scan();
        Assert.Contains("no instance fields", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void ATypeWithNoDefaultStorageIsRefused()
    {
        var result = Compiled.Of("""
            [OrblitComponent]
            public class Name { public string Value = ""; }
            """).Scan();
        Assert.Contains("explicit kind", Assert.Single(result.Errors).Message);
    }

    [Fact]
    public void AnUntypedFieldIsRefusedRatherThanGuessedAt()
    {
        // C# has no field without a written type. The nearest fault is a type the compiler cannot find.
        var result = Compiled.Of("""
            [OrblitComponent]
            public class Loose { public Missing X; }
            """).Scan();
        Assert.Contains("no resolved type", Assert.Single(result.Errors).Message);
    }
}

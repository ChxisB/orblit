using System.Text.Json;

namespace Orblit.Codegen.Tests;

public class ManifestTests
{
    [Fact]
    public void IsPlainDataAnotherToolCanRead()
    {
        var result = Compiled.Of("""
            [OrblitComponent(Replicated = true, OwnerWritable = true)]
            public class Velocity { public float X, Y, Z; }
            [OrblitComponent]
            public class Health { public int Current; }
            """).Scan();

        using var document = JsonDocument.Parse(ComponentEmitter.Manifest("my_game", result.Components));
        var json = document.RootElement;

        Assert.Equal(1, json.GetProperty("formatVersion").GetInt32());
        Assert.Equal("my_game", json.GetProperty("package").GetString());

        var components = json.GetProperty("components");
        Assert.Equal(2, components.GetArrayLength());

        var health = components[0];
        Assert.Equal("Health", health.GetProperty("name").GetString());
        Assert.Equal("int32", health.GetProperty("kind").GetString());
        Assert.Equal(1, health.GetProperty("arity").GetInt32());
        Assert.False(health.GetProperty("replicated").GetBoolean());
        // Owner-writability is meaningless without replication and should not imply otherwise.
        Assert.False(health.TryGetProperty("ownerWritable", out _));

        var velocity = components[1];
        Assert.True(velocity.GetProperty("ownerWritable").GetBoolean());
        Assert.Equal(new[] { "X", "Y", "Z" }, velocity.GetProperty("fields").EnumerateArray().Select(f => f.GetString()));
    }
}

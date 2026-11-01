using Orblit.Scene;

namespace Orblit.Test.Tests;

public sealed class SceneFindersTests
{
    private readonly SceneDocument _scene = new(entities:
    [
        Things.Make("floor", name: "Floor"),
        Things.Make("crate1", name: "Crate"),
        Things.Make("crate2", name: "Crate"),
        Things.Make("street1/lamp3/bulb", name: "Bulb"),
    ]);

    [Fact]
    public void FindsTheOneEntityWithAName()
    {
        Assert.Equal("floor", _scene.EntityNamed("Floor").Id);
    }

    [Fact]
    public void SaysWhatTheNamesAreWhenNoneMatches()
    {
        var error = Assert.Throws<InvalidOperationException>(() => _scene.EntityNamed("Roof"));

        Assert.Contains("\"Roof\"", error.Message);
        Assert.Contains("\"Floor\"", error.Message);
        Assert.Contains("\"Bulb\"", error.Message);
    }

    [Fact]
    public void SaysWhichIdsShareANameWhenTwoDo()
    {
        var error = Assert.Throws<InvalidOperationException>(() => _scene.EntityNamed("Crate"));

        Assert.Contains("crate1", error.Message);
        Assert.Contains("crate2", error.Message);
    }

    [Fact]
    public void FindsAnEntityInsideAPrefabInstanceByItsPath()
    {
        Assert.Equal("Bulb", _scene.EntityAt("street1/lamp3/bulb").Name);
        Assert.Throws<InvalidOperationException>(() => _scene.EntityAt("street1/lamp9/bulb"));
    }
}

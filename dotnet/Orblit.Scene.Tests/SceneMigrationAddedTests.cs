using static Orblit.Scene.Tests.Tree;

namespace Orblit.Scene.Tests;

public class SceneMigrationAddedTests
{
    [Fact]
    public void AnObjectWithNoKindIsNamedAsHavingNoneRatherThanAsAKindCalledNull()
    {
        var notes = new List<string>();

        Assert.Null(SceneMigrations.Entity(Map(("id", "old")), 3, notes));
        Assert.Equal("Object \"old\" has no kind this editor can read. It was left out.", Assert.Single(notes));
    }
}

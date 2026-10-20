namespace Orblit.Codegen.Tests;

public class NamesTests
{
    [Theory]
    [InlineData("my_game", "MyGameComponents")]
    [InlineData("My.Game-Core", "MyGameCoreComponents")]
    [InlineData("3d.game", "_3dGameComponents")]
    [InlineData("--", "Components")]
    public void ARegistryIsNamedFromTheAssembly(string assembly, string registry) =>
        Assert.Equal(registry, Names.Registry(assembly));

    [Theory]
    [InlineData("Game", "Game")]
    [InlineData("My.Game", "My.Game")]
    [InlineData("My-Game.Core", "My_Game.Core")]
    public void ANamespaceHoldsOnlyWhatANamespaceCan(string assembly, string @namespace) =>
        Assert.Equal(@namespace, Names.Namespace(assembly));

    [Theory]
    [InlineData("/work/game/", "/work/game/lib/A.cs", "lib/A.cs")]
    [InlineData("/work/game", "/work/game/lib/A.cs", "lib/A.cs")]
    [InlineData("", "/work/game/lib/A.cs", "/work/game/lib/A.cs")]
    [InlineData("/elsewhere/", "/work/game/lib/A.cs", "/work/game/lib/A.cs")]
    [InlineData("C:\\other\\", "C:\\work\\A.cs", "C:/work/A.cs")]
    [InlineData("C:\\work\\", "C:\\work\\lib\\A.cs", "lib/A.cs")]
    public void APathIsRelativeToTheProjectFolderWhenItIsInside(string projectDir, string path, string relative) =>
        Assert.Equal(relative, ComponentScanner.Relative(projectDir, path));
}

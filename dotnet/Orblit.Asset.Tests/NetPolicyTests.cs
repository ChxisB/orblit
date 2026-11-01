namespace Orblit.Asset.Tests;

public class NetPolicyTests
{
    private static AssetOrigin Origin(FetchPolicy? policy = null) =>
        AssetOrigin.Parse("https://assets.example.com/game/v3", policy ?? new FetchPolicy());

    private static string Refusal(Action run)
    {
        try
        {
            run();
        }
        catch (FetchRefusedException refused)
        {
            return refused.Reason;
        }

        throw new Xunit.Sdk.XunitException("that should have been refused");
    }

    private static readonly WebUrl Model = WebUrl.Parse("https://assets.example.com/game/v3/models/robot.gltf");

    // where a base may be
    [Fact]
    public void ABaseIsADirectoryWhetherOrNotItSaysSo()
    {
        Assert.Equal("https://e.example.com/a/", AssetOrigin.Parse("https://e.example.com/a").Base.ToString());
        Assert.Equal("https://e.example.com/a/", AssetOrigin.Parse("https://e.example.com/a/").Base.ToString());
    }

    [Fact]
    public void PlainHTTPIsRefusedAndSaysHowToAllowIt()
    {
        var why = Refusal(() => AssetOrigin.Parse("http://e.example.com/a"));
        Assert.Contains("plain HTTP", why);
        Assert.Contains("AllowInsecure", why);
    }

    [Fact]
    public void ALocalTestServerCanBeAllowedOnPurpose() =>
        Assert.Equal(
            "localhost",
            AssetOrigin.Parse("http://localhost:8080/assets", new FetchPolicy(allowInsecure: true)).Base.Host);

    [Fact]
    public void ASchemeThatIsNotTheWebAtAllIsRefused()
    {
        Assert.Contains("file", Refusal(() => AssetOrigin.Parse("file:///etc/passwd")));
        Assert.Contains("no scheme", Refusal(() => AssetOrigin.Parse("/just/a/path")));
    }

    // where an asset may be
    [Fact]
    public void AnIdLandsUnderTheBase() =>
        Assert.Equal(
            "https://assets.example.com/game/v3/models/robot.glb",
            Origin().UrlOf(AssetId.Parse("models/robot.glb")).ToString());

    [Fact]
    public void ANameWithASpaceOrAHashInItStaysOneName()
    {
        var url = Origin().UrlOf(AssetId.Parse("t/metal plate#2.png"));
        Assert.EndsWith("/t/metal%20plate%232.png", url.ToString());

        // Not a fragment: the server is being asked for a file with a hash in its name, and a bare # would
        // ask for a different file entirely.
        Assert.Null(url.Fragment);
        Assert.Equal("metal plate#2.png", Uri.UnescapeDataString(url.Path.Split('/')[^1]));
    }

    // what a downloaded file may ask for
    [Fact]
    public void ARelativeReferenceResolvesBesideTheFileThatMadeIt() =>
        Assert.Equal(
            "https://assets.example.com/game/v3/textures/metal.png",
            Origin().Beside(Model, "../textures/metal.png").ToString());

    [Fact]
    public void AReferenceCannotClimbOutOfTheBase() =>
        Assert.Contains("outside /game/v3/", Refusal(() => Origin().Beside(Model, "../../../secrets/keys.json")));

    [Fact]
    public void AReferenceCannotReachTheLocalMachine() =>
        Assert.Contains("file", Refusal(() => Origin().Beside(Model, "file:///etc/passwd")));

    [Fact]
    public void AReferenceCannotWanderToAnotherHost() =>
        Assert.Contains("evil.example.net", Refusal(() => Origin().Beside(Model, "https://evil.example.net/x.png")));

    [Fact]
    public void AHostThatMerelyEndsWithAnAllowedOneIsNotAllowed()
    {
        var listed = Origin(new FetchPolicy(["cdn.example.com"]));
        Assert.True(listed.Allows(WebUrl.Parse("https://cdn.example.com/x.png")));
        Assert.False(listed.Allows(WebUrl.Parse("https://evil.cdn.example.com/x.png")));
        Assert.False(listed.Allows(WebUrl.Parse("https://cdn.example.com.evil.net/x.png")));
    }

    [Fact]
    public void AListedHostIsAllowedAnywhereOnItBeingNobodyElsesBase()
    {
        // The base's own host is confined to the base's path, because a CDN serves more than one project. A
        // separately listed host was named on purpose and has no path to be inside of.
        var listed = Origin(new FetchPolicy(["cdn.example.com"]));
        Assert.Equal(
            "https://cdn.example.com/anywhere/x.png",
            listed.Beside(Model, "https://cdn.example.com/anywhere/x.png").ToString());
    }

    [Fact]
    public void ASchemeRelativeReferenceIsStillHeldToTheRules() =>
        Assert.Contains("evil.example.net", Refusal(() => Origin().Beside(Model, "//evil.example.net/x.png")));

    [Fact]
    public void AllowingAddsHostsWithoutLosingTheOtherLimits()
    {
        var strict = new FetchPolicy(maxBytes: 5, maxPixels: 7, allowInsecure: true);
        var more = strict.Allowing(["a.example.com"]);
        Assert.Equal(["a.example.com"], more.Hosts);
        Assert.Equal(5, more.MaxBytes);
        Assert.Equal(7, more.MaxPixels);
        Assert.True(more.AllowInsecure);
    }
}

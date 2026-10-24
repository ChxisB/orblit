using Avalonia.Controls;

namespace Orblit.Ui.Avalonia.Tests;

public class LiveInterfaceTests
{
    private static UiStrings Menu()
    {
        var strings = new UiStrings();
        strings.Add("en", new Dictionary<string, string>
        {
            ["play"] = "Play",
            ["greet"] = "Hello, {name}",
            ["items.one"] = "{count} item",
            ["items.other"] = "{count} items",
        });
        strings.Add("fr", new Dictionary<string, string> { ["play"] = "Jouer", ["greet"] = "Bonjour, {name}" });
        strings.Add("fr-ca", new Dictionary<string, string> { ["play"] = "Joue" });
        return strings;
    }

    private static UiNode Keyed(string key, string? name = null) => new()
    {
        Type = "text",
        Props = name is null
            ? new Dictionary<string, object?> { ["textKey"] = key }
            : new Dictionary<string, object?> { ["textKey"] = key, ["textArgs"] = new Dictionary<string, object?> { ["name"] = name } },
    };

    [Fact]
    public Task ChangesLanguageWithTheSameDescription() => Headless.Run(() =>
    {
        var strings = Menu();
        var tree = new UiNode
        {
            Type = "column",
            Children = [Keyed("greet", "Ann"), new UiNode { Type = "button", Props = new Dictionary<string, object?> { ["textKey"] = "play" } }],
        };
        var window = Headless.Show(new UiSurface { Description = tree, Strings = strings });
        Assert.Equal("Hello, Ann", Probe.Words(window, "Hello, Ann").Text);
        Assert.Equal("Play", Probe.Words(window, "Play").Text);

        strings.Use("fr");
        window.UpdateLayout();
        Assert.Equal("Bonjour, Ann", Probe.Words(window, "Bonjour, Ann").Text);
        Assert.Equal("Jouer", Probe.Words(window, "Jouer").Text);
    });

    [Fact]
    public Task ShowsAMissingKeyNotABlank() => Headless.Run(() =>
    {
        var surface = new UiSurface { Description = Keyed("quit"), Strings = Menu() };
        var window = Headless.Show(surface);
        Assert.Equal("quit", Probe.Words(window, "quit").Text);

        surface.Strings = null;
        window.UpdateLayout();
        Assert.Equal("quit", Probe.Words(window, "quit").Text);
    });

    [Fact]
    public Task ShowsItsOwnTextBeforeAKeyNobodyHas() => Headless.Run(() =>
    {
        var tree = new UiNode { Type = "text", Text = "Quit game", Props = new Dictionary<string, object?> { ["textKey"] = "quit" } };
        var surface = new UiSurface { Description = tree, Strings = Menu() };
        var window = Headless.Show(surface);
        Assert.Equal("Quit game", Probe.Words(window, "Quit game").Text);

        surface.Strings = null;
        window.UpdateLayout();
        Assert.Equal("Quit game", Probe.Words(window, "Quit game").Text);
    });

    [Fact]
    public Task SetsTextInTheFallbackFontsItWasGiven() => Headless.Run(() =>
    {
        var window = Headless.Show(new UiSurface
        {
            Description = new UiNode { Type = "text", Text = "Hi" },
            FontFamily = "Main",
            FontFallback = ["Noto Sans JP"],
        });
        Assert.Equal(["Main", "Noto Sans JP"], Probe.Words(window, "Hi").FontFamily.FamilyNames);
    });

    [Fact]
    public Task ALanguageAddedLaterReachesAnInterfaceAlreadyShown() => Headless.Run(() =>
    {
        var strings = new UiStrings();
        var window = Headless.Show(new UiSurface { Description = Keyed("play"), Strings = strings });
        Assert.Equal("play", Probe.Words(window, "play").Text);

        strings.Add("en", new Dictionary<string, string> { ["play"] = "Play" });
        window.UpdateLayout();
        Assert.Equal("Play", Probe.Words(window, "Play").Text);
    });
}

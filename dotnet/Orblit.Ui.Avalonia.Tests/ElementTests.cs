using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Orblit.Ui.Avalonia.Tests;

public class ElementTests
{
    private static readonly UiTheme Vocabulary = new();

    private static Control Built(UiNode node, UiBuilderOptions? options = null) => new UiBuilder(options ?? new UiBuilderOptions()).Build(node);

    private static UiNode Text(string words, string classes = "") => new() { Type = "text", Text = words, Classes = classes };

    private static UiNode Handled(string type, string handler, string name, string? words = null, params UiNode[] children) =>
        new() { Type = type, Text = words, Children = children, Props = new Dictionary<string, object?> { [name] = handler } };

    private static WriteableBitmap Picture() => new WriteableBitmap(new PixelSize(2, 2), new Vector(96, 96), PixelFormat.Bgra8888);

    [Fact]
    public Task TextTakesTheStyleOfItsOwnClassesAndNobodyElses() => Headless.Run(() =>
    {
        var block = Assert.IsType<TextBlock>(Built(Text("hello", "uppercase italic font-bold text-center text-2xl text-slate-300")));

        Assert.Equal("HELLO", block.Text);
        Assert.Equal(TextAlignment.Center, block.TextAlignment);
        Assert.Equal(FontStyle.Italic, block.FontStyle);
        Assert.Equal(FontWeight.Bold, block.FontWeight);
        Assert.Equal(Vocabulary.Text["2xl"], block.FontSize);
        Assert.Equal(Color.FromUInt32(Vocabulary.Colour("slate-300")!.Value.Argb), Assert.IsAssignableFrom<ISolidColorBrush>(block.Foreground).Color);
    });

    [Fact]
    public Task AStyleOnAColumnIsNotHandedToTheWordsInIt() => Headless.Run(() =>
    {
        var column = new UiNode { Type = "column", Classes = "text-2xl italic", Children = [Text("a"), Text("b")] };
        var window = Probe.Show(column);
        Assert.All(Probe.All<TextBlock>(window), block => Assert.NotEqual(FontStyle.Italic, block.FontStyle));
    });

    [Fact]
    public Task AnUppercaseTextDoesNotExpandTheSharpS() => Headless.Run(() =>
    {
        // The original upper-cases with the language's rules, which turn the sharp s into two.
        Assert.Equal("STRAßE", Assert.IsType<TextBlock>(Built(Text("straße", "uppercase"))).Text);
    });

    [Fact]
    public Task ALineHeightIsAMultipleOfTheSizeItIsSetAt() => Headless.Run(() =>
    {
        var block = Assert.IsType<TextBlock>(Built(Text("tall", "text-2xl leading-2")));
        Assert.Equal(Vocabulary.Text["2xl"] * 2, block.LineHeight);

        var plain = Assert.IsType<TextBlock>(Built(Text("tall", "leading-2")));
        Assert.Equal(Vocabulary.Text["base"] * 2, plain.LineHeight);
    });

    [Fact]
    public Task TheHostsFontIsUsedWithItsFallbacksBehindIt() => Headless.Run(() =>
    {
        var block = Assert.IsType<TextBlock>(Built(Text("x"), new UiBuilderOptions { FontFamily = "Brand Sans", FontFallback = ["Noto Sans JP", "Noto Emoji"] }));
        Assert.Equal(["Brand Sans", "Noto Sans JP", "Noto Emoji"], block.FontFamily.FamilyNames.Select(name => name));

        var none = Assert.IsType<TextBlock>(Built(Text("x")));
        Assert.Equal(new TextBlock().FontFamily, none.FontFamily);
    });

    [Fact]
    public Task FallbacksAloneFollowTheDefaultFont() => Headless.Run(() =>
    {
        var family = UiBuilder.FamilyOf(null, ["Noto Emoji"])!;
        Assert.Equal([FontFamily.DefaultFontFamilyName, "Noto Emoji"], family.FamilyNames);
        Assert.Null(UiBuilder.FamilyOf("", []));
    });

    [Fact]
    public Task KeyedTextFindsItsWordsAndFallsBackToTheKey() => Headless.Run(() =>
    {
        var strings = new UiStrings();
        strings.Add("en", new Dictionary<string, string> { ["menu.play"] = "Play" });
        var options = new UiBuilderOptions { Strings = strings };

        Assert.Equal("Play", Assert.IsType<TextBlock>(Built(new UiNode { Type = "text", Props = new Dictionary<string, object?> { ["textKey"] = "menu.play" } }, options)).Text);
        Assert.Equal("menu.gone", Assert.IsType<TextBlock>(Built(new UiNode { Type = "text", Props = new Dictionary<string, object?> { ["textKey"] = "menu.gone" } }, options)).Text);
    });

    [Fact]
    public Task AKeyBecomesTheTagOfTheControl() => Headless.Run(() =>
    {
        Assert.Equal("score", Built(new UiNode { Type = "text", Text = "1", Key = "score" }).Tag);
        Assert.Null(Built(Text("1")).Tag);
    });

    [Fact]
    public Task EverythingTheVocabularyDoesNotKnowIsListedForTheEditor() => Headless.Run(() =>
    {
        var node = new UiNode { Type = "column", Classes = "p-4 wobble", Children = [new UiNode { Type = "text", Classes = "flub", Text = "x" }] };
        var unknown = new UiBuilder(new UiBuilderOptions()).UnknownIn(node);
        Assert.Contains("wobble", unknown);
        Assert.Contains("flub", unknown);
        Assert.DoesNotContain("p-4", unknown);
    });

    [Fact]
    public Task AFieldShowsItsTextAndSaysWhatWasTyped() => Headless.Run(() =>
    {
        List<(string, object?)> fired = [];
        var field = new UiNode
        {
            Type = "field",
            Text = "ab",
            Props = new Dictionary<string, object?> { ["onChanged"] = "typed", ["placeholder"] = "Name" },
        };
        var window = Probe.Show(field, (handler, payload) => fired.Add((handler, payload)));

        var box = Probe.All<TextBox>(window).Single();
        Assert.Equal(("ab", "Name"), (box.Text, box.PlaceholderText));
        Assert.Empty(fired);

        box.Text = "abc";
        Assert.Equal([("typed", (object?)"abc")], fired);
    });

    [Fact]
    public Task AnImageIsLoadedFromWhatItsSourceNames() => Headless.Run(() =>
    {
        List<string> asked = [];
        var picture = Picture();
        var options = new UiBuilderOptions { Images = source => { asked.Add(source); return Task.FromResult<IImage?>(picture); } };
        var image = Assert.IsType<Image>(Built(new UiNode { Type = "image", Props = new Dictionary<string, object?> { ["src"] = "https://example.test/a.png" } }, options));

        Assert.Same(picture, image.Source);
        Assert.Equal(["https://example.test/a.png"], asked);
        Assert.Equal(Stretch.UniformToFill, image.Stretch);
    });

    [Fact]
    public Task AnImageWithNoSourceAsksForNothing() => Headless.Run(() =>
    {
        var asked = 0;
        var options = new UiBuilderOptions { Images = _ => { asked++; return Task.FromResult<IImage?>(null); } };

        Assert.IsType<Border>(Built(new UiNode { Type = "image" }, options));
        Assert.IsType<Border>(Built(new UiNode { Type = "image", Props = new Dictionary<string, object?> { ["src"] = "" } }, options));
        Assert.IsType<Border>(Built(new UiNode { Type = "image", Props = new Dictionary<string, object?> { ["src"] = 3 } }, options));
        Assert.Equal(0, asked);
    });

    [Fact]
    public Task APictureThatWillNotLoadShowsNothing() => Headless.Run(() =>
    {
        var node = new UiNode { Type = "image", Props = new Dictionary<string, object?> { ["src"] = "https://example.test/gone.png" } };

        var broken = new UiBuilderOptions { Images = _ => Task.FromException<IImage?>(new IOException("gone")) };
        Assert.Null(Assert.IsType<Image>(Built(node, broken)).Source);

        var thrown = new UiBuilderOptions { Images = _ => throw new InvalidOperationException("gone") };
        Assert.Null(Assert.IsType<Image>(Built(node, thrown)).Source);

        Assert.Null(Assert.IsType<Image>(Built(node)).Source);
    });

    [Fact]
    public Task ATouchFaceHasASizeWhenTheStyleGivesNone() => Headless.Run(() =>
    {
        var bare = Assert.IsType<Border>(Assert.IsType<TouchStick>(Built(new UiNode { Type = "stick" })).Children[0]);
        Assert.Equal((120, 120), (bare.Width, bare.Height));

        var sized = Assert.IsType<Border>(Assert.Single(Assert.IsType<TouchSwipe>(Built(new UiNode { Type = "swipe-pad", Classes = "w-24 h-6" })).Children));
        var face = Assert.IsType<Border>(sized.Child);
        Assert.Equal((96, 24), (face.Width, face.Height));
    });

    [Fact]
    public Task AButtonWithChildrenTakesThePressOnWhatIsInsideIt() => Headless.Run(() =>
    {
        List<string> fired = [];
        var button = Handled("button", "go", "onPressed", null, Text("Go"));
        var window = Probe.Show(button, (handler, _) => fired.Add(handler));

        Probe.Click(window, Probe.Words(window, "Go"));
        Assert.Equal(["go"], fired);
    });

    [Fact]
    public Task AButtonFallsBackToOnTapWhenItHasNoOnPressed() => Headless.Run(() =>
    {
        List<string> fired = [];
        var window = Probe.Show(Handled("button", "tapped", "onTap", "Tap"), (handler, _) => fired.Add(handler));

        Probe.Click(window, Probe.Words(window, "Tap"));
        Assert.Equal(["tapped"], fired);
    });
}

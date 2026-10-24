namespace Orblit.Stage.Tests;

public class CookedSurfaceTests
{
    private static readonly object?[] Declared =
    [
        Entry("speed", "float", instance: true, @default: 1.5),
        Entry(
            "tint",
            "vec4",
            instance: true,
            hints: new OrderedDictionary<string, object?> { ["source_color"] = new List<object?>() },
            @default: new List<object?> { 1L, 0.5, 0.25, 1L }),
        Entry("glow", "bool", instance: false),
        Entry(
            "noise",
            "sampler2D",
            instance: false,
            hints: new OrderedDictionary<string, object?> { ["filter_nearest"] = new List<object?>() }),
    ];

    private static readonly CookedSurface Cooked = CookedSurface.FromParams("cook/pulse.filamat", Declared);

    private static OrderedDictionary<string, object?> Entry(
        string name,
        string type,
        bool instance,
        OrderedDictionary<string, object?>? hints = null,
        object? @default = null)
    {
        var entry = new OrderedDictionary<string, object?> { ["name"] = name, ["type"] = type, ["instance"] = instance };
        if (hints is not null)
        {
            entry["hints"] = hints;
        }

        if (@default is not null)
        {
            entry["default"] = @default;
        }

        return entry;
    }

    private static string? Locate(string path) => $"/p/{path}";

    private static OrderedDictionary<string, object> Given(params (string Name, object Value)[] values) =>
        new OrderedDictionary<string, object>(values.Select(one => KeyValuePair.Create(one.Name, one.Value)));

    [Fact]
    public void WidthsDefaultsAndPicturesComeFromTheList()
    {
        Assert.Equal([("speed", 1), ("tint", 4), ("glow", 1)], Cooked.Widths.Select(one => (one.Key, one.Value)));
        Assert.Equal(["speed", "tint"], Cooked.Defaults.Keys);
        Assert.Equal([1.5], Cooked.Defaults["speed"]);
        Assert.Equal([1.0, 0.5, 0.25, 1.0], Cooked.Defaults["tint"]);
        Assert.Equal([("noise", false)], Cooked.Pictures.Select(one => (one.Key, one.Value)));
    }

    [Fact]
    public void APictureThatHoldsAColourIsReadAsSrgb()
    {
        var surface = CookedSurface.FromParams(
            "p",
            new List<object?>
            {
                Entry(
                    "albedo",
                    "sampler2D",
                    instance: false,
                    hints: new OrderedDictionary<string, object?> { ["source_color"] = new List<object?>() }),
            });

        Assert.Equal([("albedo", true)], surface.Pictures.Select(one => (one.Key, one.Value)));
    }

    [Fact]
    public void TheDeclarationsKeepWhatAnInspectorShows()
    {
        var surface = CookedSurface.FromParams(
            "p",
            new List<object?>
            {
                Entry(
                    "tint",
                    "vec4",
                    instance: true,
                    hints: new OrderedDictionary<string, object?> { ["source_color"] = new List<object?>() }),
                Entry(
                    "speed",
                    "float",
                    instance: false,
                    hints: new OrderedDictionary<string, object?>
                    {
                        ["hint_range"] = new List<object?> { "0.0", "10.0", "0.1" },
                    }),
                Entry(
                    "odd",
                    "float",
                    instance: false,
                    hints: new OrderedDictionary<string, object?> { ["hint_range"] = new List<object?> { "5", "1" } }),
            });

        var tint = surface.Params[0];
        var speed = surface.Params[1];
        var odd = surface.Params[2];
        Assert.Equal(3, surface.Params.Count);
        Assert.Equal(("tint", true, true), (tint.Name, tint.Instance, tint.IsColour));
        Assert.False(speed.Instance);
        Assert.Equal((0.0, 10.0, 0.1), speed.Range);
        Assert.Null(odd.Range);
    }

    [Fact]
    public void ARangeWithoutAStepHasAStepOfNought()
    {
        var surface = CookedSurface.FromParams(
            "p",
            new List<object?>
            {
                Entry(
                    "a",
                    "float",
                    instance: false,
                    hints: new OrderedDictionary<string, object?> { ["hint_range"] = new List<object?> { "0", "1" } }),
                Entry(
                    "b",
                    "float",
                    instance: false,
                    hints: new OrderedDictionary<string, object?>
                    {
                        ["hint_range"] = new List<object?> { "0", "1", "soon" },
                    }),
                Entry(
                    "c",
                    "float",
                    instance: false,
                    hints: new OrderedDictionary<string, object?> { ["hint_range"] = new List<object?> { "0", "x" } }),
            });

        Assert.Equal((0.0, 1.0, 0.0), surface.Params[0].Range);
        Assert.Equal((0.0, 1.0, 0.0), surface.Params[1].Range);
        Assert.Null(surface.Params[2].Range);
    }

    [Fact]
    public void AHintArgumentThatIsNotTextIsKeptAsText()
    {
        var surface = CookedSurface.FromParams(
            "p",
            new List<object?>
            {
                Entry(
                    "a",
                    "float",
                    instance: false,
                    hints: new OrderedDictionary<string, object?> { ["odd"] = new List<object?> { 2L, true, "x" } }),
            });

        Assert.Equal(["2", "true", "x"], surface.Params[0].Hints["odd"]);
    }

    [Fact]
    public void AValueIsWhatTheObjectStatesElseTheDefault()
    {
        Assert.Equal([1.5], Cooked.ValueOf("speed", Given()));
        Assert.Equal([4.0], Cooked.ValueOf("speed", Given(("speed", 4L))));
        Assert.Equal(
            [0.0, 1.0, 0.0, 1.0],
            Cooked.ValueOf("tint", Given(("tint", new List<object?> { 0L, 1L, 0L, 1L }))));
        Assert.Equal([1.0, 0.5, 0.25, 1.0], Cooked.ValueOf("tint", Given(("tint", 3L))));
        Assert.Equal([0.0], Cooked.ValueOf("glow", Given()));
    }

    [Fact]
    public void AnEntryItCannotReadIsLeftOut()
    {
        var surface = CookedSurface.FromParams(
            "p",
            new List<object?>
            {
                Entry("a", "mat4", instance: false),
                new OrderedDictionary<string, object?> { ["type"] = "float" },
                "nonsense",
                Entry("b", "vec2", instance: false, @default: new List<object?> { 1L, 2L, 3L }),
            });

        Assert.Equal([("b", 2)], surface.Widths.Select(one => (one.Key, one.Value)));
        Assert.Empty(surface.Defaults);
        Assert.Empty(CookedSurface.FromParams("p", "nonsense").Widths);
        Assert.Empty(CookedSurface.FromParams("p", null).Params);
    }

    [Fact]
    public void ADefaultIsReadFromAFlagToo()
    {
        var surface = CookedSurface.FromParams(
            "p",
            new List<object?> { Entry("on", "bool", instance: false, @default: true) });

        Assert.Equal([1.0], surface.Defaults["on"]);
    }

    [Fact]
    public void AListWithAWordInItIsNotADefault()
    {
        var surface = CookedSurface.FromParams(
            "p",
            new List<object?> { Entry("at", "vec2", instance: false, @default: new List<object?> { 1L, "two" }) });

        Assert.Empty(surface.Defaults);
    }

    [Fact]
    public void ASurfaceIsTheDefaultsThenWhatTheObjectStates()
    {
        var surface = Cooked.SurfaceFor(Given(("speed", 4L)), Locate)!;

        Assert.Equal("/p/cook/pulse.filamat", surface.Package);
        Assert.Equal([4.0], surface.Values["speed"]);
        Assert.Equal([1.0, 0.5, 0.25, 1.0], surface.Values["tint"]);
        Assert.Empty(surface.Textures);
    }

    [Fact]
    public void AValueForANameNothingDeclaresOrTheWrongWidthIsDropped()
    {
        var surface = Cooked.SurfaceFor(
            Given(("speed", new List<object?> { 1L, 2L }), ("missing", 3L)),
            Locate)!;

        Assert.Equal(["speed", "tint"], surface.Values.Keys.Order());
        Assert.Equal([1.5], surface.Values["speed"]);
    }

    [Fact]
    public void AFlagAndAPictureAreSentAsWhatTheShaderDeclares()
    {
        var surface = Cooked.SurfaceFor(Given(("glow", true), ("noise", "art/noise.png")), Locate)!;

        Assert.Equal([1.0], surface.Values["glow"]);
        Assert.Equal("/p/art/noise.png", surface.Textures["noise"].Path);
        Assert.False(surface.Textures["noise"].Srgb);
    }

    [Fact]
    public void APictureThatCannotBePlacedIsLeftOut()
    {
        var surface = Cooked.SurfaceFor(
            Given(("noise", "art/noise.png")),
            path => path == "art/noise.png" ? null : Locate(path))!;

        Assert.Empty(surface.Textures);
        Assert.Empty(Cooked.SurfaceFor(Given(("noise", 4L)), Locate)!.Textures);
    }

    [Fact]
    public void APackageThatCannotBePlacedGivesNoSurface()
    {
        Assert.Null(Cooked.SurfaceFor(Given(), _ => null));
    }

    [Fact]
    public void ASurfaceDoesNotShareTheDefaultsWithTheNextOne()
    {
        var first = Cooked.SurfaceFor(Given(("speed", 9L)), Locate)!;
        var second = Cooked.SurfaceFor(Given(), Locate)!;

        Assert.Equal([9.0], first.Values["speed"]);
        Assert.Equal([1.5], second.Values["speed"]);
        Assert.Equal([1.5], Cooked.Defaults["speed"]);
    }
}

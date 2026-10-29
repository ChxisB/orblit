namespace Orblit.Examples.Tests;

/// <summary>
/// The controls each example shows, against the fixture. The Dart exporter
/// moves every control the way <see cref="Moves(ExampleSetting)"/> does and
/// digests the scene a second in, so a port is checked away from its
/// defaults too.
/// </summary>
public sealed class SettingsTests
{
    public static TheoryData<string> Names() => ExampleFixture.Names();

    public static TheoryData<string, int, int> Moves()
    {
        var moves = new TheoryData<string, int, int>();
        foreach (var example in ExampleFixture.All())
        {
            var settings = Controls(example);
            for (var control = 0; control < settings.Count; control++)
            {
                for (var move = 0; move < Moves(settings[control]).Count; move++)
                {
                    moves.Add(example.Name, control, move);
                }
            }
        }

        return moves;
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void TheControlsAreTheOnesTheFixtureShows(string name)
    {
        var wanted = ExampleFixture.Case(name).GetProperty("controls").EnumerateArray()
            .Select(control => control.GetProperty("label").GetString());

        Assert.Equal(wanted, Controls(ExampleFixture.Example(name)).Select(setting => setting.Label));
    }

    [Theory]
    [MemberData(nameof(Moves))]
    public void EachMoveDrawsTheFixtureSceneAndShowsItsNewValue(string name, int control, int move)
    {
        var example = ExampleFixture.Example(name);

        var setting = Controls(example)[control];
        var moved = Moves(setting)[move]();

        // The exporter flips a disabled switch too. What it shows then is the
        // example's business, and its own tests say.
        if (setting is not Toggle { Enabled: false })
        {
            Assert.Equal(moved, Shown(Controls(example)[control]));
        }

        var seeded = ExampleFixture.Seeded.ContainsKey(name);
        var message = SceneDigest.Message(example, 1);
        var wanted = ExampleFixture.Case(name).GetProperty("controls")[control]
            .GetProperty(seeded ? "shapes" : "values")[move].GetString();
        Assert.Equal(wanted, seeded ? SceneDigest.ShapeOf(message) : SceneDigest.Of(message));
    }

    /// <summary>
    /// Each move of a seeded example, on every key but the seeded ones. The
    /// fixture keeps only a seeded scene's shape, so these digests are from a
    /// one-off run of the Dart exporter with those keys left out.
    /// </summary>
    [Theory]
    [InlineData("Gaussian splats", 0, 0, "68f11b059452379b")]
    [InlineData("Gaussian splats", 1, 0, "e9e8429c972d5f00")]
    [InlineData("Gaussian splats", 2, 0, "b0b59c4e69e56328")]
    [InlineData("Gaussian splats", 3, 0, "b0b59c4e69e56328")]
    [InlineData("Gaussian splats", 4, 0, "b0efe0518911cfba")]
    [InlineData("Gaussian splats", 5, 0, "30b1a04b49bec0a6")]
    [InlineData("Gaussian splats", 5, 1, "8ead3cf80bc7d05c")]
    [InlineData("Gaussian splats", 7, 0, "f256d662f92672c3")]
    [InlineData("Gaussian splats", 8, 0, "12e76fe3c41805b3")]
    [InlineData("A hundred thousand", 0, 0, "8ee46a4594427414")]
    [InlineData("A hundred thousand", 1, 0, "ceefbca4068ee61e")]
    [InlineData("A hundred thousand", 2, 0, "45fbf8326e9bae6d")]
    [InlineData("A hundred thousand", 3, 0, "d6d77d7928be81ff")]
    [InlineData("Benchmark", 0, 0, "9d870cf8468e7902")]
    [InlineData("Benchmark", 1, 0, "8b57973f9f348789")]
    [InlineData("Benchmark", 2, 0, "307bc0dc00e64718")]
    [InlineData("Benchmark", 2, 1, "f093a3e930df7529")]
    [InlineData("Benchmark", 3, 0, "608acdda807f58c5")]
    [InlineData("Benchmark", 4, 0, "3599cee696ff863d")]
    public void EachSeededMoveMatchesDartAroundItsSeededKeys(string name, int control, int move, string rest)
    {
        var example = ExampleFixture.Example(name);
        Moves(Controls(example)[control])[move]();

        var message = SceneDigest.Message(example, 1);
        foreach (var key in ExampleFixture.Seeded[name])
        {
            message.Remove(key);
        }

        Assert.Equal(rest, SceneDigest.Of(message));
    }

    /// <summary>The settings that can be moved, as the fixture lists them. It leaves out captions and text.</summary>
    private static List<ExampleSetting> Controls(Example example) => ExampleFixture.Unlisted.Contains(example.Name)
        ? []
        : [.. example.Settings().Where(setting => setting is Setting or Toggle or Choice)];

    /// <summary>
    /// Each way a control is moved, giving where it went. A slider goes to its
    /// far end, or to its near end when it is already at the far one, and
    /// settles there. A switch flips. A choice goes to each option it is not on.
    /// </summary>
    private static IReadOnlyList<Func<object>> Moves(ExampleSetting setting) => setting switch
    {
        Setting slider =>
        [
            () =>
            {
                var to = slider.Value == slider.Max ? slider.Min : slider.Max;
                slider.Change(to);
                slider.Settle?.Invoke(to);
                return to;
            },
        ],
        Toggle toggle =>
        [
            () =>
            {
                toggle.Change(!toggle.Value);
                return !toggle.Value;
            },
        ],
        Choice { Select: { } select } choice =>
        [
            .. choice.Options.Where(option => option != choice.Selected).Select(option => (Func<object>)(() =>
            {
                select(option);
                return option;
            })),
        ],
        _ => [],
    };

    private static object Shown(ExampleSetting setting) => setting switch
    {
        Setting slider => slider.Value,
        Toggle toggle => toggle.Value,
        Choice choice => choice.Selected,
        _ => throw new InvalidOperationException($"Nothing shown for {setting.GetType()}."),
    };
}

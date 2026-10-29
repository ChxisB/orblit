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
        // example's business, and its own tests say. A press is an event, so a
        // button shows nothing afterwards.
        if (setting is not (Toggle { Enabled: false } or Buttons))
        {
            Assert.Equal(moved, Shown(Controls(example)[control]));
        }

        // A grown example's scene is checked by its rest digest instead.
        if (ExampleFixture.Grown.Contains(name))
        {
            return;
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
    [InlineData("Terrain", 0, 0, "5e7e92a6d6039c68")]
    [InlineData("Terrain", 1, 0, "bb4c91b88e730f33")]
    [InlineData("Terrain", 2, 0, "2582f68d265a579d")]
    [InlineData("Terrain", 3, 0, "2a6138373fb67aa1")]
    [InlineData("Terrain", 4, 0, "3ea600504be57d57")]
    [InlineData("Terrain", 5, 0, "11b9ab086ab06686")]
    [InlineData("Terrain", 6, 0, "e70f3868f93bd5d2")]
    [InlineData("Terrain", 7, 0, "36c2fcb151fdca78")]
    [InlineData("Terrain", 8, 0, "bc6a1788d4536098")]
    [InlineData("Terrain", 8, 1, "d30b74458223dacd")]
    [InlineData("Terrain", 8, 2, "e64831e1c65805cf")]
    [InlineData("Blocks", 0, 0, "32b5420cc2cb95e7")]
    [InlineData("Blocks", 1, 0, "63ea50e3b4d7e0c7")]
    [InlineData("Blocks", 2, 0, "d345e4f13e3ecf71")]
    [InlineData("Blocks", 3, 0, "9a26fbfbaf39ed0c")]
    [InlineData("Blocks", 4, 0, "4355f6295ec55527")]
    [InlineData("Blocks", 5, 0, "77d952304fe35712")]
    // Worlds 2 to 4 are left out. Their trees stand where the body lands,
    // so VoxelTests matches them with the trees off.
    [InlineData("Blocks", 6, 0, "a53e5bda87250765")]
    [InlineData("Runner", 0, 0, "67a0a8eb3f495bc9")]
    [InlineData("Runner", 1, 0, "67a0a8eb3f495bc9")]
    [InlineData("Runner", 2, 0, "67a0a8eb3f495bc9")]
    [InlineData("Runner", 3, 0, "c98d59eea8b37fc5")]
    [InlineData("Bistro exterior", 0, 0, "22debabd0d1275a8")]
    [InlineData("Bistro exterior", 1, 0, "9df289cc8c4fe6bf")]
    [InlineData("Bistro exterior", 2, 0, "c568c4da6b63976e")]
    [InlineData("Bistro interior", 0, 0, "1716b06f22fbe851")]
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
        : [.. example.Settings().Where(setting => setting is Setting or Toggle or Choice or Buttons
            && !ExampleFixture.Plain.GetValueOrDefault(example.Name, []).Contains(setting.Label))];

    /// <summary>
    /// Each way a control is moved, giving where it went. A slider goes to its
    /// far end, or to its near end when it is already at the far one, and
    /// settles there. A switch flips. A choice goes to each option it is not on.
    /// A row of buttons has each one pressed.
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
        Buttons buttons =>
        [
            .. buttons.Options.Select(option => (Func<object>)(() =>
            {
                buttons.Press(option);
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

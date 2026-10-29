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

        var moved = Moves(Controls(example)[control])[move]();

        Assert.Equal(moved, Shown(Controls(example)[control]));
        var wanted = ExampleFixture.Case(name).GetProperty("controls")[control].GetProperty("values")[move].GetString();
        Assert.Equal(wanted, SceneDigest.Of(SceneDigest.Message(example, 1)));
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

using Orblit.Json;
using static Orblit.Motion.Tests.BlendSamples;

namespace Orblit.Motion.Tests;

public class ConditionTests
{
    private static bool Holds(BlendCondition condition, Dictionary<string, double> values, double lap = 0) =>
        condition.Holds(Reading(values), lap);

    [Fact]
    public void AlwaysHolds() => Assert.True(Holds(BlendCondition.Always, Inputs()));

    [Fact]
    public void ComparesAnInputWithANumberStrictly()
    {
        var fast = BlendCondition.Above("speed", 1);
        var slow = BlendCondition.Below("speed", 1);
        Assert.True(Holds(fast, Inputs(("speed", 2))));
        Assert.False(Holds(fast, Inputs(("speed", 1))));
        Assert.True(Holds(slow, Inputs(("speed", 0.5))));
        Assert.False(Holds(slow, Inputs(("speed", 1))));
    }

    [Fact]
    public void TakesAnInputThatIsAnythingButNoughtAsOn()
    {
        var jumping = BlendCondition.On("jump");
        Assert.True(Holds(jumping, Inputs(("jump", 1))));
        Assert.True(Holds(jumping, Inputs(("jump", -1))));
        Assert.False(Holds(jumping, Inputs()));
    }

    [Fact]
    public void WaitsForTheStateBeingLeftToPlayThrough()
    {
        var done = BlendCondition.Through(1);
        Assert.False(Holds(done, Inputs(), lap: 0.99));
        Assert.True(Holds(done, Inputs(), lap: 1));
        Assert.True(Holds(done, Inputs(), lap: 3.5));
    }

    [Fact]
    public void PutsConditionsTogether()
    {
        var landing = BlendCondition.AllOf([
            BlendCondition.On("grounded"),
            BlendCondition.Not(BlendCondition.Above("fall", 3)),
        ]);
        Assert.True(Holds(landing, Inputs(("grounded", 1), ("fall", 1))));
        Assert.False(Holds(landing, Inputs(("grounded", 1), ("fall", 5))));
        Assert.False(Holds(landing, Inputs(("fall", 1))));

        var either = BlendCondition.AnyOf([BlendCondition.On("hit"), BlendCondition.Through(2)]);
        Assert.True(Holds(either, Inputs(("hit", 1))));
        Assert.True(Holds(either, Inputs(), lap: 2));
        Assert.False(Holds(either, Inputs(), lap: 1));
        Assert.True(Holds(BlendCondition.AllOf([]), Inputs()));
        Assert.False(Holds(BlendCondition.AnyOf([]), Inputs()));
    }

    [Fact]
    public void ListsTheInputsItReadsOnceEach()
    {
        var condition = BlendCondition.AllOf([
            BlendCondition.On("grounded"),
            BlendCondition.AnyOf([BlendCondition.Above("speed", 1), BlendCondition.Below("speed", -1)]),
            BlendCondition.Through(1),
        ]);
        Assert.Equal(["grounded", "speed"], condition.Inputs.ToList());
    }

    [Fact]
    public void InAFileReadsBackWhatItWrites()
    {
        var condition = BlendCondition.AnyOf([
            BlendCondition.AllOf([BlendCondition.On("grounded"), BlendCondition.Above("speed", 0.1)]),
            BlendCondition.Not(BlendCondition.Below("height", 2)),
            BlendCondition.Through(0.8),
        ]);
        var text = JsonText.Encode(condition.ToJson());
        var problems = new List<string>();
        var back = BlendCondition.FromJson(JsonText.Decode(text), "The change", problems);
        Assert.Empty(problems);
        Assert.Equal(text, JsonText.Encode(back!.ToJson()));
    }

    [Fact]
    public void InAFileWritesAlwaysAsNothingAndReadsNothingAsAlways()
    {
        Assert.Empty(BlendCondition.Always.ToJson());
        Assert.Same(BlendCondition.Always, BlendCondition.FromJson(Tree.Map(), "The change", []));
    }

    [Fact]
    public void InAFileIsNothingWithANoteWhenItCannotBeRead()
    {
        object?[] unreadable =
        [
            "speed > 1",
            Tree.Map(("input", "speed"), ("above", "fast")),
            Tree.Map(("through", "twice")),
            Tree.Map(("all", "of them")),
            Tree.Map(("any", Tree.List(Tree.Map(("input", "jump")), Tree.Map(("wobble", 1L))))),
            Tree.Map(("not", Tree.Map(("below", 1L)))),
        ];
        foreach (var raw in unreadable)
        {
            var problems = new List<string>();
            Assert.Null(BlendCondition.FromJson(raw, "The change", problems));
            var problem = Assert.Single(problems);
            Assert.StartsWith("The change", problem, StringComparison.Ordinal);
        }
    }
}

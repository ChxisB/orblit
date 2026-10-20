using Orblit.Progress;
using static Orblit.Dialogue.Tests.Support;

namespace Orblit.Dialogue.Tests;

public class RunnerTests
{
    private static string Saying(DialogueRunner runner) => ((LineNode)runner.Current!).Text;

    private static DialogueDefinition OneLine(string key, Variant variant, params Node[] nodes) =>
        new(key, [variant], nodes);

    // the variants

    [Fact]
    public void GiveAPlayerAtStage0TheFirstConversation()
    {
        var runner = Book().Start("guide", At());

        Assert.Equal("Hello.", Saying(runner));
    }

    [Fact]
    public void GiveAPlayerAtStage1TheOtherOne()
    {
        var runner = Book().Start("guide", At(stage: 1));

        Assert.Equal("Back so soon?", Saying(runner));
    }

    [Fact]
    public void StartNothingWhenNoneHolds()
    {
        var only = new DialogueDefinition(
            "quiet",
            [new Variant("a", [StageAtLeast(2)])],
            [new LineNode("a", "X", "Hi")]);

        var runner = Book(only).Start("quiet", At());

        Assert.True(runner.IsOver);
        Assert.Null(runner.Current);
    }

    // startFor

    [Fact]
    public void NamesTheStartForATableAndRunsNothing()
    {
        var (_, seen) = Hooks();
        var d = new DialogueDefinition(
            "k",
            [new Variant("b", [StageAtLeast(1)]), new Variant("a")],
            [new LineNode("a", "X", "A", before: "take"), new LineNode("b", "X", "B", before: "take")]);

        Assert.Equal("a", d.StartFor(At()));
        Assert.Equal("b", d.StartFor(At(stage: 1)));
        Assert.Empty(seen);
    }

    [Fact]
    public void IsNullWhenNoVariantHolds()
    {
        var d = new DialogueDefinition(
            "k",
            [new Variant("a", [StageAtLeast(2)])],
            [new LineNode("a", "X", "A")]);

        Assert.Null(d.StartFor(At()));
    }

    // a line

    [Fact]
    public void GoesOnWithNextAndEndsWhereItHasNoNext()
    {
        var runner = Book().Start("guide", At(stage: 1));

        Assert.False(runner.IsOver);
        runner.Next();

        Assert.True(runner.IsOver);
    }

    [Fact]
    public void IsTheOnlyNodeThatGoesOnWithNext()
    {
        var runner = Book().Start("guide", At());
        runner.Next();

        Assert.IsType<ChoiceNode>(runner.Current);
        Assert.Throws<InvalidOperationException>(runner.Next);
    }

    [Fact]
    public void IsTheOnlyNodeAChoiceIsRefusedAt()
    {
        var runner = Book().Start("guide", At());

        Assert.Throws<InvalidOperationException>(() => runner.Choose(0));
    }

    // a choice

    private static DialogueRunner AtAsk(long gold = 0)
    {
        var runner = Book().Start("guide", At(gold: gold));
        runner.Next();
        return runner;
    }

    private static string Texts(DialogueRunner r) => string.Join('|', r.Options.Select(o => o.Text));

    [Fact]
    public void OffersTheOptionsWhoseConditionsHoldInFileOrder()
    {
        Assert.Equal("The lamp?|Goodbye.", Texts(AtAsk()));
        Assert.Equal("The lamp?|I have gold.|Goodbye.", Texts(AtAsk(gold: 5)));
    }

    [Fact]
    public void GivesAListTheCallerCannotChange()
    {
        var options = (IList<ChoiceOption>)AtAsk().Options;

        Assert.Throws<NotSupportedException>(() => options.RemoveAt(0));
    }

    [Fact]
    public void IsPickedByAnIndexIntoWhatIsOffered()
    {
        var runner = AtAsk();
        runner.Choose(1);

        Assert.True(runner.IsOver);
    }

    [Fact]
    public void RefusesAnIndexThatIsNotOffered()
    {
        var runner = AtAsk();

        Assert.Throws<ArgumentOutOfRangeException>(() => runner.Choose(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => runner.Choose(-1));
        Assert.IsType<ChoiceNode>(runner.Current);
    }

    [Fact]
    public void EndsTheConversationWhenNothingIsOffered()
    {
        var stuck = OneLine(
            "stuck",
            new Variant("a"),
            new ChoiceNode("a", [new ChoiceOption("No", null, [StageAtLeast(3)])]));

        var runner = Book(stuck).Start("stuck", At());

        Assert.True(runner.IsOver);
        Assert.Empty(runner.Options);
    }

    [Fact]
    public void HasNoOptionsWhenTheNodeIsALine()
    {
        Assert.Empty(Book().Start("guide", At()).Options);
    }

    [Fact]
    public void LeavesNoOptionsBehindWhenItGoesOnToALine()
    {
        var runner = AtAsk();
        runner.Choose(0);

        Assert.Equal("In the cave.", Saying(runner));
        Assert.Empty(runner.Options);
    }

    // a set

    [Fact]
    public void WritesTheTableAndTheNextConditionSeesIt()
    {
        var runner = Book().Start("guide", At());
        runner.Next();
        runner.Choose(0);
        Assert.Equal("In the cave.", Saying(runner));

        runner.Next();

        Assert.Equal("Go and find it.", Saying(runner));
        Assert.Equal(1, runner.Table.Get("quest.lamp.stage"));
    }

    [Fact]
    public void IsSeenByAChoiceThatComesAfterIt()
    {
        var after = OneLine(
            "after",
            new Variant("w"),
            new SetNode("w", [new Write("gold", 9)], next: "c"),
            new ChoiceNode("c", [new ChoiceOption("Rich", null, [GoldAtLeast(9)])]));

        var runner = Book(after).Start("after", At());

        Assert.Equal("Rich", Assert.Single(runner.Options).Text);
    }

    [Fact]
    public void GivesTheChangesToSendAndLeavesTheTableItWasGiven()
    {
        var start = At();
        var runner = Book().Start("guide", start);
        runner.Next();
        runner.Choose(0);
        runner.Next();

        Assert.Equal(0, start.Get("quest.lamp.stage"));
        Assert.False(runner.Table.ChangesSince(start).IsEmpty);
        Assert.Equal(1, start.Apply(runner.Table.ChangesSince(start)).Get("quest.lamp.stage"));
    }

    [Fact]
    public void WritesInOrderSoTheLastWriteToANameWins()
    {
        var twice = OneLine(
            "twice",
            new Variant("w"),
            new SetNode("w", [new Write("gold", 1), new Write("gold", 2)], next: "l"),
            new LineNode("l", "X", "Hi"));

        Assert.Equal(2, Book(twice).Start("twice", At()).Table.Get("gold"));
    }

    // a jump

    [Fact]
    public void MovesOnWithoutShowingAnything()
    {
        var jumping = OneLine(
            "jumping",
            new Variant("j"),
            new JumpNode("j", "l"),
            new LineNode("l", "X", "Landed"));

        Assert.Equal("Landed", Saying(Book(jumping).Start("jumping", At())));
    }

    // before

    [Fact]
    public void RunsBeforeTheNodeIsShownAndItsTableGoesOn()
    {
        var (hooks, seen) = Hooks();
        var b = new DialogueBook(Schema, hooks, [Guide()]);
        var runner = b.Start("guide", At(gold: 7));
        runner.Next();
        runner.Choose(1);

        Assert.Equal([7L], seen);
        Assert.Equal("Thanks.", Saying(runner));
        Assert.Equal(0, runner.Table.Get("gold"));
    }

    [Fact]
    public void ThatThrowsLeavesTheRunnerWhereItWas()
    {
        var d = OneLine(
            "k",
            new Variant("a"),
            new LineNode("a", "X", "One", next: "w"),
            new SetNode("w", [], next: "b", before: "bump"),
            new LineNode("b", "X", "Two", before: "boom"));
        var b = new DialogueBook(
            Schema,
            new Dictionary<string, Before>
            {
                ["bump"] = table => table.Set("gold", 5).Table,
                ["boom"] = _ => throw new InvalidOperationException("no"),
            },
            [d]);
        var runner = b.Start("k", At());

        Assert.Throws<InvalidOperationException>(runner.Next);

        Assert.Equal("One", Saying(runner));
        Assert.False(runner.IsOver);
        Assert.Equal(0, runner.Table.Get("gold"));
    }

    [Fact]
    public void DoesNotRunForANodeNeverReached()
    {
        var (hooks, seen) = Hooks();
        var b = new DialogueBook(Schema, hooks, [Guide()]);

        b.Start("guide", At(gold: 7)).Next();

        Assert.Empty(seen);
    }

    [Fact]
    public void RunsForASetAndAJumpToo()
    {
        var calls = new List<string>();
        var d = OneLine(
            "quiet",
            new Variant("j"),
            new JumpNode("j", "s", before: "a"),
            new SetNode("s", [], next: "l", before: "b"),
            new LineNode("l", "X", "Hi", before: "c"));
        Before Note(string name) => table =>
        {
            calls.Add(name);
            return table;
        };
        var b = new DialogueBook(
            Schema,
            new Dictionary<string, Before> { ["a"] = Note("a"), ["b"] = Note("b"), ["c"] = Note("c") },
            [d]);

        b.Start("quiet", At());

        Assert.Equal(["a", "b", "c"], calls);
    }
}

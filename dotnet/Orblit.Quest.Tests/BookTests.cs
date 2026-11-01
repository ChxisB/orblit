using Orblit.Progress;
using Orblit.Save;
using static Orblit.Quest.Tests.Support;

namespace Orblit.Quest.Tests;

public class BookTests
{
    private static Moved Moved(QuestMove move) => (Moved)move;

    private static QuestDefinition Quest(string key, int stages) =>
        new(key, key, [.. Enumerable.Range(0, stages).Select(i => new Stage($"s{i}", ""))]);

    private static QuestDefinition Gated(string key, string title, Condition condition) =>
        new(key, title, [new Stage("a", ""), new Stage("b", "", [condition])]);

    // the journal

    [Fact]
    public void FollowsTheStage()
    {
        var b = Book();

        Assert.Equal("", b.Journal(At(0), "lamp"));
        Assert.Equal("Find the lamp.", b.Journal(At(1), "lamp"));
        Assert.Equal("Light the lamp.", b.Journal(At(2), "lamp"));
    }

    [Fact]
    public void SaysWhetherTheQuestIsBegunAndWhetherItIsComplete()
    {
        var b = Book();

        Assert.Equal([false, true, true], Enumerable.Range(0, 3).Select(s => b.IsStarted(At(s), "lamp")));
        Assert.Equal([false, false, true], Enumerable.Range(0, 3).Select(s => b.IsComplete(At(s), "lamp")));
    }

    [Fact]
    public void IsRefusedForAQuestTheBookDoesNotHave()
    {
        Assert.ThrowsAny<ArgumentException>(() => Book().Journal(At(0), "nope"));
        Assert.ThrowsAny<ArgumentException>(() => Book().Quest("nope"));
    }

    // advance

    [Fact]
    public void MovesForwardAndSaysWhatChanged()
    {
        var move = Moved(Book().Advance(At(0), "lamp", 1));

        Assert.Equal(1L, Book().StageOf(move.Table, "lamp"));
        Assert.False(move.Completed);
        Assert.False(move.Back);
        Assert.False(move.Changes.IsEmpty);
    }

    [Fact]
    public void RefusesTheStageThePlayerIsInAndAnyBehindIt()
    {
        foreach (var to in new[] { 1, 0 })
        {
            var move = Book().Advance(At(1), "lamp", to);

            Assert.Equal(QuestRefusal.NotForward, Assert.IsType<Refused>(move).Reason);
        }
    }

    [Fact]
    public void GoesBackOnlyWhenAllowedAndSaysSo()
    {
        var move = Moved(Book().Advance(At(2, 9), "lamp", 1, allowBack: true));

        Assert.Equal(1L, Book().StageOf(move.Table, "lamp"));
        Assert.True(move.Back);
        Assert.False(move.Completed);
    }

    [Fact]
    public void GoingBackDoesNotAskForTheRequirementsOfThatStage()
    {
        var well = new QuestDefinition(
            "well",
            "The Well",
            [new Stage("a", ""), new Stage("b", "", [new Condition("gold", Compare.Greater, 0)]), new Stage("c", "")]);
        var b = new QuestBook(Schema, [well]);
        var start = new ProgressTable(Schema).Set("quest.well.stage", 2).Table;

        var move = b.Advance(start, "well", 1, allowBack: true);

        Assert.IsType<Moved>(move);
    }

    [Fact]
    public void AllowBackDoesNotLetAQuestStayOrSkipARequirement()
    {
        Assert.IsType<Refused>(Book().Advance(At(1), "lamp", 1, allowBack: true));
        Assert.IsType<Refused>(Book().Advance(At(0), "lamp", 2, allowBack: true));
    }

    [Fact]
    public void IsRefusedUntilTheRequirementsOfTheStageHold()
    {
        var refused = Book().Advance(At(1, 4), "lamp", 2);
        var allowed = Book().Advance(At(1, 5), "lamp", 2);

        Assert.Equal(QuestRefusal.Requirement, Assert.IsType<Refused>(refused).Reason);
        Assert.IsType<Moved>(allowed);
    }

    [Fact]
    public void MaySkipAStage()
    {
        var move = Moved(Book().Advance(At(0, 5), "lamp", 2));

        Assert.True(move.Completed);
        Assert.Equal(2L, Book().StageOf(move.Table, "lamp"));
    }

    [Fact]
    public void DoesNotLetASkipPassARequirementOnTheWay()
    {
        var gated = new QuestBook(
            Schema,
            [
                new QuestDefinition(
                    "lamp",
                    "L",
                    [
                        new Stage("a", ""),
                        new Stage("b", "", [new Condition("gold", Compare.GreaterOrEqual, 5)]),
                        new Stage("c", ""),
                    ]),
            ]);

        var refused = gated.Advance(At(0, 4), "lamp", 2);
        var allowed = gated.Advance(At(0, 5), "lamp", 2);

        Assert.Equal(QuestRefusal.Requirement, Assert.IsType<Refused>(refused).Reason);
        Assert.IsType<Moved>(allowed);
    }

    [Fact]
    public void CompletesOnceOnEnteringTheLastStage()
    {
        var b = Book();
        var first = Moved(b.Advance(At(1, 5), "lamp", 2));
        var again = b.Advance(first.Table, "lamp", 2);

        Assert.True(first.Completed);
        Assert.IsType<Refused>(again);
    }

    [Fact]
    public void CopesWithAStageTheQuestHasNotGotFromALongerQuest()
    {
        var b = Book();
        var stray = At(3);

        Assert.Equal("", b.Journal(stray, "lamp"));
        Assert.True(b.IsComplete(stray, "lamp"));
        var back = Moved(b.Advance(stray, "lamp", 2, allowBack: true));
        Assert.True(back.Back);
        Assert.False(back.Completed);
    }

    [Fact]
    public void ThrowsForAStageTheQuestDoesNotHave()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Book().Advance(At(0), "lamp", 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => Book().Advance(At(0), "lamp", -1));
    }

    [Fact]
    public void LeavesTheTableItWasGivenAsItWas()
    {
        var start = At(0);

        Book().Advance(start, "lamp", 1);

        Assert.Equal(0L, Book().StageOf(start, "lamp"));
    }

    [Fact]
    public void ChangesOnlyTheStageOfItsOwnQuest()
    {
        var start = new ProgressTable(Schema).Set("quest.well.stage", 5).Table;

        var move = Moved(Book().Advance(start, "lamp", 1));

        Assert.Equal(5L, move.Table.Get("quest.well.stage"));
    }

    // a book

    [Fact]
    public void IsRefusedForAQuestTheSchemaHasNoNameFor()
    {
        Assert.ThrowsAny<ArgumentException>(() => new QuestBook(Schema, [Quest("ghost", 2)]));
    }

    [Fact]
    public void IsRefusedWhenTheStageBitsAreTooFew()
    {
        Assert.ThrowsAny<ArgumentException>(() => new QuestBook(Schema, [Quest("lamp", 5)]));
        Assert.Single(new QuestBook(Schema, [Quest("lamp", 4)]).Quests);
    }

    [Fact]
    public void IsRefusedWhenARequirementNamesAnUnknownValue()
    {
        var bad = Gated("lamp", "L", new Condition("nope", Compare.Equal, 1));

        Assert.ThrowsAny<ArgumentException>(() => new QuestBook(Schema, [bad]));
    }

    [Fact]
    public void IsRefusedWhenARequirementIsTooBigForItsValue()
    {
        var bad = Gated("lamp", "L", new Condition("quest.well.stage", Compare.Equal, 8));

        Assert.ThrowsAny<ArgumentException>(() => new QuestBook(Schema, [bad]));
    }

    [Fact]
    public void IsRefusedWhenTwoQuestsShareAKey()
    {
        Assert.ThrowsAny<ArgumentException>(() => new QuestBook(Schema, [Quest("lamp", 2), Quest("lamp", 2)]));
    }

    // a stage

    [Fact]
    public async Task SurvivesASaveAndALoadThroughTheStore()
    {
        var store = new MemorySaveStore();
        var queue = SaveQueue.Inline(store, () => new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), (_, _) => { });
        var reached = Moved(Book().Advance(At(0), "lamp", 1)).Table;
        queue.Save("player", reached.Encode(), new Leave());
        await queue.FlushAsync();

        var record = await queue.ReadAsync("player");
        Assert.NotNull(record);
        var loaded = ProgressTable.Decode(Schema, record.Bytes.Span);

        Assert.Equal(1L, Book().StageOf(loaded, "lamp"));
        Assert.Equal("Find the lamp.", Book().Journal(loaded, "lamp"));
    }

    [Fact]
    public void ReachesTheOwnerAsADeltaThatRebuildsTheTable()
    {
        var move = Moved(Book().Advance(At(0), "lamp", 1));

        var seen = Changes.TryDecode(Schema, move.Changes.Encode());

        Assert.NotNull(seen);
        Assert.Equal(1L, Book().StageOf(At(0).Apply(seen), "lamp"));
    }
}

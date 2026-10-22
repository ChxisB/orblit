using Orblit.Json;
using Orblit.Progress;

namespace Orblit.Quest.Tests;

public class QuestTests
{
    private static Dictionary<string, object?> QuestFile() => new()
    {
        ["kind"] = "orblit.quest",
        ["formatVersion"] = 1,
        ["key"] = "lamp",
        ["title"] = "The Lamp",
        ["stages"] = Stages(Obj(("id", "unknown"), ("journal", "")), Obj(("id", "find"), ("journal", "Find the lamp."))),
    };

    private static Dictionary<string, object?> Obj(params (string Key, object? Value)[] pairs) =>
        pairs.ToDictionary(p => p.Key, p => p.Value);

    private static List<object?> Stages(params object?[] stages) => [.. stages];

    private static Dictionary<string, object?> Changed(Action<Dictionary<string, object?>> change)
    {
        var file = QuestFile();
        change(file);
        return file;
    }

    private static QuestDefinition Parse(Dictionary<string, object?> json) => QuestDefinition.Parse(JsonText.Encode(json));

    private static Stage Stage(string id) => new(id, "");

    // a quest

    [Fact]
    public void RoundTripsThroughItsFile()
    {
        var again = QuestDefinition.Parse(Support.Lamp().Encode());

        Assert.Equal("lamp", again.Key);
        Assert.Equal("The Lamp", again.Title);
        Assert.Equal(["unknown", "find", "light"], again.Stages.Select(s => s.Id));
        Assert.Equal("Find the lamp.", again.Stages[1].Journal);
        var need = Assert.Single(again.Stages[2].Requires);
        Assert.Equal(("gold", Compare.GreaterOrEqual, 5L), (need.Name, need.Compare, need.Value));
        Assert.Equal(Support.Lamp().Encode(), again.Encode());
    }

    [Fact]
    public void IsRefusedWhenItBreaksARule()
    {
        Assert.ThrowsAny<ArgumentException>(() => new Stage("", ""));
        Assert.ThrowsAny<ArgumentException>(() => new QuestDefinition("bad key", "T", [Stage("a"), Stage("b")]));
        Assert.ThrowsAny<ArgumentException>(() => new QuestDefinition("a.b", "T", [Stage("a"), Stage("b")]));
        Assert.ThrowsAny<ArgumentException>(() => new QuestDefinition("k", "", [Stage("a"), Stage("b")]));
        Assert.ThrowsAny<ArgumentException>(() => new QuestDefinition("k", "T", [Stage("a")]));
        Assert.ThrowsAny<ArgumentException>(() => new QuestDefinition("k", "T", [Stage("a"), Stage("a")]));
    }

    [Fact]
    public void KeepsAStageListThatTheCallerCannotChange()
    {
        var stages = new List<Stage> { Stage("a"), Stage("b") };
        var quest = new QuestDefinition("k", "T", stages);

        stages.Clear();

        Assert.Equal(2, quest.Stages.Count);
        Assert.Throws<NotSupportedException>(() => ((ICollection<Stage>)quest.Stages).Clear());
    }

    // a file

    [Fact]
    public void IsRead()
    {
        Assert.Equal(2, Parse(QuestFile()).Stages.Count);
    }

    [Fact]
    public void IsRefusedWhenItIsNotAQuest()
    {
        Assert.Throws<QuestFormatException>(() => QuestDefinition.Parse("not json"));
        Assert.Throws<QuestFormatException>(() => QuestDefinition.Parse("[]"));
        Assert.Throws<QuestFormatException>(() => Parse(Changed(f => f["kind"] = "orblit.clip")));
    }

    [Fact]
    public void IsRefusedFromANewerOrblitAndWithoutAVersion()
    {
        var newer = Assert.Throws<QuestFormatException>(() => Parse(Changed(f => f["formatVersion"] = 2)));
        Assert.Contains("newer", newer.Message);
        Assert.Throws<QuestFormatException>(() => Parse(Changed(f => f.Remove("formatVersion"))));
        Assert.Throws<QuestFormatException>(() => Parse(Changed(f => f["formatVersion"] = 0)));
    }

    [Fact]
    public void IsRefusedWhenAPartIsMissingOrTheWrongKind()
    {
        var broken = new[]
        {
            Changed(f => f.Remove("key")),
            Changed(f => f["title"] = 3),
            Changed(f => f["stages"] = "none"),
            Changed(f => f["stages"] = Stages(1)),
            Changed(f => f["stages"] = Stages(Obj(("id", "a")))),
            Changed(f => f["stages"] = Stages(
                Obj(("id", "a"), ("journal", ""), ("requires", "gold")),
                Obj(("id", "b"), ("journal", "")))),
            Changed(f => f["stages"] = Stages(Obj(("id", "a"), ("journal", "")))),
            Changed(f => f["key"] = "a b"),
        };
        foreach (var file in broken)
        {
            Assert.True(Throws(() => Parse(file)), JsonText.Encode(file));
        }
    }

    [Fact]
    public void SaysWhatWasWrongWithARuleItBroke()
    {
        var error = Assert.Throws<QuestFormatException>(() => Parse(Changed(f => f["key"] = "a b")));

        Assert.Contains("a b", error.Message);
    }

    [Fact]
    public void NamesTheStageIdItFoundTwice()
    {
        var twice = Changed(f => f["stages"] = Stages(
            Obj(("id", "same"), ("journal", "")),
            Obj(("id", "same"), ("journal", "x"))));

        var error = Assert.Throws<QuestFormatException>(() => Parse(twice));

        Assert.Contains("same", error.Message);
    }

    private static bool Throws(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (QuestFormatException)
        {
            return true;
        }
    }
}

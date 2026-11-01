using Orblit.Json;
using Orblit.Progress;
using static Orblit.Dialogue.Tests.Support;

namespace Orblit.Dialogue.Tests;

public class DialogueTests
{
    private static Dictionary<string, object?> File() => new()
    {
        ["kind"] = "orblit.dialogue",
        ["formatVersion"] = 1,
        ["key"] = "guide",
        ["variants"] = L(Obj(("start", "a"))),
        ["nodes"] = Obj(("a", Obj(("type", "line"), ("speaker", "Guide"), ("text", "Hi")))),
    };

    private static Dictionary<string, object?> Obj(params (string Key, object? Value)[] pairs) =>
        pairs.ToDictionary(p => p.Key, p => p.Value);

    private static List<object?> L(params object?[] items) => [.. items];

    private static Dictionary<string, object?> Changed(Action<Dictionary<string, object?>> change)
    {
        var file = File();
        change(file);
        return file;
    }

    private static DialogueDefinition Parse(Dictionary<string, object?> json) =>
        DialogueDefinition.Parse(JsonText.Encode(json));

    private static DialogueFormatException FailsToLoad(Action load, string? containing = null)
    {
        var error = Assert.Throws<DialogueFormatException>(load);
        if (containing is not null)
        {
            Assert.Contains(containing, error.Message);
        }

        return error;
    }

    private static DialogueDefinition One(Node node, params Condition[] when) =>
        new("one", [new Variant(node.Id, when)], [node]);

    private static DialogueBook Make(DialogueDefinition d, Dictionary<string, Before>? hooks = null) =>
        new(Schema, hooks ?? [], [d]);

    // a dialogue

    [Fact]
    public void RoundTripsThroughItsFile()
    {
        var again = DialogueDefinition.Parse(Guide().Encode());

        Assert.Equal(Guide().Encode(), again.Encode());
        Assert.Equal(2, again.Variants.Count);
        Assert.Equal(1, Assert.Single(again.Variants[0].When).Value);
        Assert.Equal(Guide().Nodes.Keys, again.Nodes.Keys);
        Assert.Equal("take", ((LineNode)again.Nodes["pay"]).Before);
        Assert.Single(((ChoiceNode)again.Nodes["ask"]).Options[1].When);
        Assert.Equal("quest.lamp.stage", Assert.Single(((SetNode)again.Nodes["begin"]).Writes).Name);
    }

    [Fact]
    public void NamesTheFunctionsItsNodesAskFor()
    {
        Assert.Equal(["take"], Guide().BeforeNames);
    }

    [Fact]
    public void IsRefusedWhenItBreaksARule()
    {
        var a = new LineNode("a", "X", "Hi");
        Node[] nodes = [a];
        Variant[] variants = [new Variant("a")];

        Assert.ThrowsAny<ArgumentException>(() => new DialogueDefinition("", variants, nodes));
        Assert.ThrowsAny<ArgumentException>(() => new DialogueDefinition("k", [], nodes));
        Assert.ThrowsAny<ArgumentException>(() => new DialogueDefinition("k", variants, [a, a]));
        Assert.ThrowsAny<ArgumentException>(() => new DialogueDefinition("k", [new Variant("zz")], nodes));
        Assert.ThrowsAny<ArgumentException>(() =>
            new DialogueDefinition("k", variants, [new LineNode("a", "X", "Hi", next: "zz")]));
        Assert.ThrowsAny<ArgumentException>(() => new DialogueDefinition(
            "k",
            variants,
            [a, new ChoiceNode("c", [new ChoiceOption("x", "zz")])]));
    }

    [Fact]
    public void IsRefusedWhenAVariantCanNeverStart()
    {
        Node[] nodes = [new LineNode("a", "X", "Hi")];

        Assert.ThrowsAny<ArgumentException>(() => new DialogueDefinition(
            "k",
            [new Variant("a"), new Variant("a", [StageAtLeast(1)])],
            nodes));
        Assert.Equal(
            2,
            new DialogueDefinition("k", [new Variant("a", [StageAtLeast(1)]), new Variant("a")], nodes).Variants.Count);
    }

    [Fact]
    public void IsRefusedWhenNodesGoRoundWithNothingToShow()
    {
        Assert.ThrowsAny<ArgumentException>(() => new DialogueDefinition(
            "k",
            [new Variant("a")],
            [new JumpNode("a", "b"), new SetNode("b", [], next: "a")]));
        Assert.ThrowsAny<ArgumentException>(() =>
            new DialogueDefinition("k", [new Variant("a")], [new JumpNode("a", "a")]));
    }

    [Fact]
    public void MayGoRoundWhenALineIsOnTheWay()
    {
        var loop = new DialogueDefinition(
            "k",
            [new Variant("a")],
            [new LineNode("a", "X", "Hi", next: "b"), new JumpNode("b", "a")]);

        Assert.Equal(2, loop.Nodes.Count);
    }

    [Fact]
    public void IsRefusedForAnEmptyNodeOptionOrWrite()
    {
        Assert.ThrowsAny<ArgumentException>(() => new LineNode("", "X", "Hi"));
        Assert.ThrowsAny<ArgumentException>(() => new LineNode("a", "X", "Hi", before: ""));
        Assert.ThrowsAny<ArgumentException>(() => new ChoiceNode("a", []));
        Assert.ThrowsAny<ArgumentException>(() => new Write("", 1));
        Assert.ThrowsAny<ArgumentException>(() => new Write("gold", -1));
        Assert.ThrowsAny<ArgumentException>(() => new Write("gold", 0x100000000));
    }

    // a file

    [Fact]
    public void IsRead()
    {
        Assert.Single(Parse(File()).Nodes);
    }

    [Fact]
    public void IsRefusedWhenItIsNotADialogue()
    {
        FailsToLoad(() => DialogueDefinition.Parse("nope"));
        FailsToLoad(() => DialogueDefinition.Parse("[]"));
        FailsToLoad(() => Parse(Changed(f => f["kind"] = "orblit.quest")));
    }

    [Fact]
    public void IsRefusedFromANewerOrblitAndWithoutAVersion()
    {
        FailsToLoad(() => Parse(Changed(f => f["formatVersion"] = 2)), "newer");
        FailsToLoad(() => Parse(Changed(f => f.Remove("formatVersion"))));
        FailsToLoad(() => Parse(Changed(f => f["formatVersion"] = 0)));
    }

    [Fact]
    public void IsRefusedWhenAPartIsMissingOrTheWrongKind()
    {
        Dictionary<string, object?> Nodes(object? node) => Changed(f => f["nodes"] = Obj(("a", node)));
        Dictionary<string, object?> Line(params (string, object?)[] pairs) => Nodes(Obj([("type", "line"), .. pairs]));

        var bad = new List<Dictionary<string, object?>>
        {
            Changed(f => f.Remove("key")),
            Changed(f => f["variants"] = "x"),
            Changed(f => f["variants"] = L(1)),
            Changed(f => f["variants"] = L(Obj(("when", L())))),
            Changed(f => f["nodes"] = L(1)),
            Nodes(1),
            Nodes(Obj(("type", "wave"))),
            Line(("speaker", "X")),
            Line(("speaker", "X"), ("text", 1)),
            Line(("speaker", "X"), ("text", "Hi"), ("next", 4)),
            Nodes(Obj(("type", "choice"), ("options", "x"))),
            Nodes(Obj(("type", "choice"), ("options", L(1)))),
            Nodes(Obj(("type", "choice"), ("options", L()))),
            Nodes(Obj(("type", "set"), ("writes", L(Obj(("name", "g")))))),
            Nodes(Obj(("type", "set"), ("writes", L(1)))),
            Nodes(Obj(("type", "set"), ("writes", L(Obj(("name", "g"), ("value", -1)))))),
            Nodes(Obj(("type", "jump"))),
            Changed(f => f["variants"] = L(Obj(("start", "a"), ("when", "x")))),
            Changed(f => f["variants"] = L(Obj(("start", "zz")))),
        };
        for (var i = 0; i < bad.Count; i++)
        {
            var json = bad[i];
            Assert.True(
                Throws(() => Parse(json)),
                $"case {i} {JsonText.Encode(json)}");
        }

        static bool Throws(Action load)
        {
            try
            {
                load();
                return false;
            }
            catch (DialogueFormatException)
            {
                return true;
            }
        }
    }

    [Fact]
    public void NamesTheNodeIdItFoundTwice()
    {
        var error = Assert.ThrowsAny<ArgumentException>(() => new DialogueDefinition(
            "k",
            [new Variant("a")],
            [new LineNode("a", "X", "Hi"), new LineNode("a", "X", "Yo")]));

        Assert.Equal("nodes repeats an id (a).", error.Message);
    }

    [Fact]
    public void SaysWhatWasWrongWithARuleItBroke()
    {
        FailsToLoad(() => Parse(Changed(f => f["variants"] = L(Obj(("start", "zz"))))), "zz");
    }

    // a book

    [Fact]
    public void IsRefusedForABeforeNobodyRegistered()
    {
        var d = One(new LineNode("a", "X", "Hi", before: "ghost"));

        Assert.ThrowsAny<ArgumentException>(() => Make(d));
        Assert.ThrowsAny<ArgumentException>(() => Make(d, new Dictionary<string, Before> { ["take"] = t => t }));
        Assert.Single(Make(d, new Dictionary<string, Before> { ["ghost"] = t => t }).Dialogues);
    }

    [Fact]
    public void IsRefusedForANameAConditionUsesThatTheSchemaLacks()
    {
        var variant = One(new LineNode("a", "X", "Hi"), new Condition("ghost", Compare.Equal, 1));
        var option = One(new ChoiceNode(
            "a",
            [new ChoiceOption("x", null, [new Condition("ghost", Compare.Equal, 1)])]));

        Assert.ThrowsAny<ArgumentException>(() => Make(variant));
        Assert.ThrowsAny<ArgumentException>(() => Make(option));
    }

    [Fact]
    public void IsRefusedForAWriteToANameTheSchemaLacks()
    {
        var d = One(new SetNode("a", [new Write("ghost", 1)]));

        var error = Assert.ThrowsAny<ArgumentException>(() => Make(d));

        Assert.Contains("not in the schema", error.Message);
    }

    [Fact]
    public void IsRefusedForAConditionValueTooBigForItsName()
    {
        var d = One(new ChoiceNode(
            "a",
            [new ChoiceOption("x", null, [new Condition("met", Compare.Equal, 2)])]));

        Assert.ThrowsAny<ArgumentException>(() => Make(d));
    }

    [Fact]
    public void IsRefusedForAValueThatDoesNotFitItsName()
    {
        var tooBig = One(new SetNode("a", [new Write("met", 2)]));
        var fits = One(new SetNode("a", [new Write("met", 1)]));
        var whole = One(new SetNode("a", [new Write("gold", 0xFFFFFFFF)]));

        Assert.ThrowsAny<ArgumentException>(() => Make(tooBig));
        Assert.Single(Make(fits).Dialogues);
        Assert.Single(Make(whole).Dialogues);
    }

    [Fact]
    public void IsRefusedWhenTwoDialoguesShareAKey()
    {
        Assert.ThrowsAny<ArgumentException>(() => new DialogueBook(Schema, Hooks().Hooks, [Guide(), Guide()]));
    }

    [Fact]
    public void IsRefusedForADialogueItDoesNotHave()
    {
        Assert.ThrowsAny<ArgumentException>(() => Book().Start("ghost", At()));
        Assert.ThrowsAny<ArgumentException>(() => Book().Dialogue("ghost"));
    }
}

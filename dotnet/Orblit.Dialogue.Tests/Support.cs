using Orblit.Progress;

namespace Orblit.Dialogue.Tests;

internal static class Support
{
    internal static readonly ProgressSchema Schema = new(
        1,
        [
            new Whole("flags"),
            new Whole("gold"),
            new Slice("quest.lamp.stage", "flags", 0, 2),
            new Slice("met", "flags", 2, 1),
        ]);

    internal static ProgressTable At(long stage = 0, long gold = 0) =>
        new ProgressTable(Schema).Set("quest.lamp.stage", stage).Table.Set("gold", gold).Table;

    internal static Condition StageAtLeast(long n) => new("quest.lamp.stage", Compare.GreaterOrEqual, n);

    internal static Condition GoldAtLeast(long n) => new("gold", Compare.GreaterOrEqual, n);

    /// <summary>
    /// Two variants. A player at stage 1 or more hears "again", others "hello".
    /// "hello" leads to a choice, one option of which sets the stage.
    /// </summary>
    internal static DialogueDefinition Guide() => new(
        "guide",
        [
            new Variant("again", [StageAtLeast(1)]),
            new Variant("hello"),
        ],
        [
            new LineNode("hello", "Guide", "Hello.", next: "ask"),
            new ChoiceNode(
                "ask",
                [
                    new ChoiceOption("The lamp?", "lamp"),
                    new ChoiceOption("I have gold.", "pay", [GoldAtLeast(5)]),
                    new ChoiceOption("Goodbye.", null),
                ]),
            new LineNode("lamp", "Guide", "In the cave.", next: "begin"),
            new SetNode("begin", [new Write("quest.lamp.stage", 1)], next: "told"),
            new LineNode("told", "Guide", "Go and find it."),
            new LineNode("pay", "Guide", "Thanks.", before: "take"),
            new LineNode("again", "Guide", "Back so soon?"),
        ]);

    /// <summary>Registers the hook <see cref="Guide"/> names, and counts its calls.</summary>
    internal static (Dictionary<string, Before> Hooks, List<long> Seen) Hooks()
    {
        var seen = new List<long>();
        var hooks = new Dictionary<string, Before>
        {
            ["take"] = table =>
            {
                seen.Add(table.Get("gold"));
                return table.Set("gold", 0).Table;
            },
        };
        return (hooks, seen);
    }

    internal static DialogueBook Book(params DialogueDefinition[] dialogues) =>
        new(Schema, Hooks().Hooks, dialogues.Length == 0 ? [Guide()] : dialogues);
}

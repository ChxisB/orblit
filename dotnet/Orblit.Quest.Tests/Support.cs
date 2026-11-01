using Orblit.Progress;

namespace Orblit.Quest.Tests;

internal static class Support
{
    internal static readonly ProgressSchema Schema = new(
        1,
        [
            new Whole("flags"),
            new Whole("gold"),
            new Slice("quest.lamp.stage", "flags", 0, 2),
            new Slice("quest.well.stage", "flags", 2, 3),
        ]);

    /// <summary>Three stages, so the middle one is neither the start nor the end.</summary>
    internal static QuestDefinition Lamp() => new(
        "lamp",
        "The Lamp",
        [
            new Stage("unknown", ""),
            new Stage("find", "Find the lamp."),
            new Stage("light", "Light the lamp.", [new Condition("gold", Compare.GreaterOrEqual, 5)]),
        ]);

    internal static QuestBook Book() => new(Schema, [Lamp()]);

    internal static ProgressTable At(long stage, long gold = 0) =>
        new ProgressTable(Schema).Set("quest.lamp.stage", stage).Table.Set("gold", gold).Table;
}

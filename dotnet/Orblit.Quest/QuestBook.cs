using System.Diagnostics.CodeAnalysis;
using Orblit.Progress;

namespace Orblit.Quest;

/// <summary>Why a quest did not move.</summary>
public enum QuestRefusal
{
    /// <summary>The stage asked for is the one the player is in, or one behind it.</summary>
    NotForward,

    /// <summary>A condition of the stage asked for does not hold.</summary>
    Requirement,
}

/// <summary>What <see cref="QuestBook.Advance"/> did.</summary>
public abstract record QuestMove
{
    private protected QuestMove()
    {
    }
}

/// <summary>
/// The quest moved. Write <see cref="Table"/> over the player's, send <see cref="Changes"/> to the player, and give
/// the reward when <see cref="Completed"/>.
/// </summary>
/// <param name="Table">The player's table with the new stage.</param>
/// <param name="Changes">What changed, for the owning client.</param>
/// <param name="Completed">The quest entered its last stage from an earlier one.</param>
/// <param name="Back">The quest went back a stage, which only <c>allowBack</c> permits. Record why.</param>
public sealed record Moved(ProgressTable Table, Changes Changes, bool Completed, bool Back) : QuestMove;

/// <summary>The quest stayed where it was.</summary>
/// <param name="Reason">Why it did not move.</param>
public sealed record Refused(QuestRefusal Reason) : QuestMove;

/// <summary>
/// The quests a game has, and the rules for moving a player through them.
/// The book holds no player. Each question and each move takes the player's
/// <see cref="ProgressTable"/> and, for a move, gives a new one back, so the game decides
/// what to save, to send and to reward.
/// </summary>
public sealed class QuestBook
{
    private readonly OrderedDictionary<string, QuestDefinition> _quests = [];

    /// <summary>
    /// Checks every quest against <paramref name="schema"/> when the game starts, so a typo
    /// fails here and not when a player reaches it: the stage name must be in
    /// the schema with room for every stage, and every condition must name a
    /// value the schema has.
    /// </summary>
    public QuestBook(ProgressSchema schema, IReadOnlyList<QuestDefinition> quests)
    {
        foreach (var quest in quests)
        {
            if (!_quests.TryAdd(quest.Key, quest))
            {
                throw new RuleException("quests", "repeat a key", quest.Key);
            }
        }

        foreach (var quest in quests)
        {
            Check(schema, quest);
        }
    }

    public IEnumerable<QuestDefinition> Quests => _quests.Values;

    /// <summary>Throws <see cref="ArgumentException"/> for a key that is not a quest.</summary>
    public QuestDefinition Quest(string key) =>
        _quests.TryGetValue(key, out var quest) ? quest : throw new RuleException("key", "is unknown", key);

    /// <summary>The index of the stage <paramref name="table"/> has the player in. Zero is not begun.</summary>
    [SuppressMessage("Performance", "CA1822", Justification = "A port keeps the Dart shape: callers ask the book.")]
    public long StageOf(ProgressTable table, string key) => table.Get(QuestNames.StageName(key));

    public bool IsStarted(ProgressTable table, string key) => StageOf(table, key) > 0;

    public bool IsComplete(ProgressTable table, string key) => StageOf(table, key) >= Quest(key).Last;

    /// <summary>
    /// The text for the stage the player is in, or empty for a stage the quest
    /// no longer has, such as one in a save from before it was shortened.
    /// </summary>
    public string Journal(ProgressTable table, string key)
    {
        var stages = Quest(key).Stages;
        var at = StageOf(table, key);
        return at < stages.Count ? stages[(int)at].Journal : "";
    }

    /// <summary>
    /// Moves the player to stage <paramref name="to"/>. A quest goes forward: a stage that is
    /// not ahead is refused unless <paramref name="allowBack"/> is set and <paramref name="to"/> is behind. A
    /// move ahead is refused when a condition of any stage it enters does not
    /// hold, so a skip is no way round one. <paramref name="to"/> comes from the game and never
    /// from a client. Throws <see cref="ArgumentOutOfRangeException"/> for a stage the quest does not have.
    /// </summary>
    public QuestMove Advance(ProgressTable table, string key, long to, bool allowBack = false)
    {
        var quest = Quest(key);
        if (to < 0 || to > quest.Last)
        {
            throw new ArgumentOutOfRangeException(nameof(to), to, $"Not in the range 0 to {quest.Last}.");
        }

        var from = StageOf(table, key);
        var back = to < from;
        if (to == from || (back && !allowBack))
        {
            return new Refused(QuestRefusal.NotForward);
        }

        if (!back && !MayEnter(quest, (int)from, (int)to, table))
        {
            return new Refused(QuestRefusal.Requirement);
        }

        var written = table.Set(QuestNames.StageName(key), to);
        return new Moved(written.Table, written.Changes, !back && to == quest.Last, back);
    }

    private static bool MayEnter(QuestDefinition quest, int from, int to, ProgressTable table) => quest
        .Stages
        .Skip(from + 1)
        .Take(to - from)
        .All(stage => Condition.AllHold(stage.Requires, table));

    private static void Check(ProgressSchema schema, QuestDefinition quest)
    {
        var name = QuestNames.StageName(quest.Key);
        if (!schema.Has(name))
        {
            throw new RuleException(quest.Key, "is not in the schema", name);
        }

        if (!schema.Fits(name, quest.Last))
        {
            throw new RuleException(quest.Key, $"has too few bits for {quest.Stages.Count} stages", name);
        }

        foreach (var stage in quest.Stages)
        {
            foreach (var condition in stage.Requires)
            {
                CheckCondition(schema, $"{quest.Key}/{stage.Id}", condition);
            }
        }
    }

    private static void CheckCondition(ProgressSchema schema, string where, Condition condition)
    {
        if (!schema.Has(condition.Name))
        {
            throw new RuleException(where, "is not in the schema", condition.Name);
        }

        if (!schema.Fits(condition.Name, condition.Value))
        {
            throw new RuleException(where, "is too big for it", condition.Value);
        }
    }
}

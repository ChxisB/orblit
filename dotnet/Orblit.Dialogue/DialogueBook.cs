using System.Collections.ObjectModel;
using Orblit.Progress;

namespace Orblit.Dialogue;

/// <summary>The conversations a game has, checked against its progress schema and the functions it registered.</summary>
public sealed class DialogueBook
{
    private readonly IReadOnlyDictionary<string, Before> _hooks;
    private readonly OrderedDictionary<string, DialogueDefinition> _dialogues = [];

    /// <summary>
    /// Checks every dialogue when the game starts, so a typo fails here and not
    /// when a player talks: every name a condition or a write uses must be in
    /// <paramref name="schema"/>, a written value must fit its name's bits, and every <c>before</c>
    /// must be one of <paramref name="hooks"/>.
    /// </summary>
    public DialogueBook(
        ProgressSchema schema,
        IReadOnlyDictionary<string, Before> hooks,
        IReadOnlyList<DialogueDefinition> dialogues)
    {
        _hooks = new ReadOnlyDictionary<string, Before>(hooks.ToDictionary(e => e.Key, e => e.Value));
        foreach (var dialogue in dialogues)
        {
            if (!_dialogues.TryAdd(dialogue.Key, dialogue))
            {
                throw new RuleException("dialogues", "repeat a key", dialogue.Key);
            }
        }

        foreach (var dialogue in dialogues)
        {
            Check(schema, hooks, dialogue);
        }
    }

    public IEnumerable<DialogueDefinition> Dialogues => _dialogues.Values;

    /// <summary>Throws <see cref="ArgumentException"/> for a key that is not a dialogue.</summary>
    public DialogueDefinition Dialogue(string key) =>
        _dialogues.TryGetValue(key, out var dialogue) ? dialogue : throw new RuleException("key", "is unknown", key);

    /// <summary>Starts the conversation <paramref name="key"/> for the player whose table is <paramref name="table"/>.</summary>
    public DialogueRunner Start(string key, ProgressTable table) => new(Dialogue(key), table, _hooks);

    private static void Check(ProgressSchema schema, IReadOnlyDictionary<string, Before> hooks, DialogueDefinition dialogue)
    {
        foreach (var name in dialogue.BeforeNames.Where(name => !hooks.ContainsKey(name)))
        {
            throw new RuleException(dialogue.Key, "is not a registered hook", name);
        }

        foreach (var (where, condition) in Conditions(dialogue))
        {
            CheckName(schema, where, condition.Name, condition.Value);
        }

        foreach (var node in dialogue.Nodes.Values.OfType<SetNode>())
        {
            foreach (var write in node.Writes)
            {
                CheckName(schema, $"{dialogue.Key}/{node.Id}", write.Name, write.Value);
            }
        }
    }

    private static IEnumerable<(string Where, Condition Condition)> Conditions(DialogueDefinition dialogue)
    {
        foreach (var variant in dialogue.Variants)
        {
            foreach (var condition in variant.When)
            {
                yield return ($"{dialogue.Key} variant", condition);
            }
        }

        foreach (var node in dialogue.Nodes.Values.OfType<ChoiceNode>())
        {
            foreach (var condition in node.Options.SelectMany(option => option.When))
            {
                yield return ($"{dialogue.Key}/{node.Id}", condition);
            }
        }
    }

    private static void CheckName(ProgressSchema schema, string where, string name, long value)
    {
        if (!schema.Has(name))
        {
            throw new RuleException(where, "is not in the schema", name);
        }

        if (!schema.Fits(name, value))
        {
            throw new RuleException(where, $"{name} has too few bits for it", value);
        }
    }
}

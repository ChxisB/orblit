using System.Collections.ObjectModel;
using Orblit.Json;
using Orblit.Progress;

namespace Orblit.Dialogue;

/// <summary>Names a dialogue file carries.</summary>
public static class DialogueNames
{
    /// <summary>The extension a dialogue file carries.</summary>
    public const string Extension = ".odialogue";
}

/// <summary>A file that is not a dialogue, or is one from a newer Orblit.</summary>
public sealed class DialogueFormatException(string message) : Exception(message);

/// <summary>Where a conversation starts when its conditions hold.</summary>
public sealed class Variant
{
    public Variant(string start, IReadOnlyList<Condition>? when = null)
    {
        Start = start;
        When = new ReadOnlyCollection<Condition>([.. when ?? []]);
    }

    public string Start { get; }

    public IReadOnlyList<Condition> When { get; }
}

/// <summary>
/// A conversation: variants, and the nodes they lead into.
/// The first variant whose conditions hold is the one that starts, so the
/// same character says something new as the table changes. A variant with no
/// conditions always holds, and goes last as the default.
/// </summary>
public sealed class DialogueDefinition
{
    public const string Marker = "orblit.dialogue";

    public const int FormatVersion = 1;

    public DialogueDefinition(string key, IReadOnlyList<Variant> variants, IReadOnlyList<Node> nodes)
    {
        if (key.Length == 0)
        {
            throw new RuleException("key", "is empty", key);
        }

        if (variants.Count == 0)
        {
            throw new RuleException("variants", "is empty", "[]");
        }

        var byId = new OrderedDictionary<string, Node>();
        foreach (var node in nodes)
        {
            if (!byId.TryAdd(node.Id, node))
            {
                throw new RuleException("nodes", "repeats an id", node.Id);
            }
        }

        Key = key;
        Variants = new ReadOnlyCollection<Variant>([.. variants]);
        Nodes = new ReadOnlyDictionary<string, Node>(byId);
        CheckVariants();
        CheckTargets();
        CheckNoSilentLoop();
    }

    public string Key { get; }

    public IReadOnlyList<Variant> Variants { get; }

    public IReadOnlyDictionary<string, Node> Nodes { get; }

    /// <summary>The names of the functions nodes ask for, in the order they first appear.</summary>
    public IReadOnlyList<string> BeforeNames => [.. Nodes.Values.Select(n => n.Before).OfType<string>().Distinct()];

    /// <summary>
    /// Reads the text of a <c>.odialogue</c> file. Throws <see cref="DialogueFormatException"/> for text that is not
    /// one, for one from a newer Orblit and for one that breaks a rule of <see cref="DialogueDefinition"/>,
    /// <see cref="Node"/> or <see cref="Condition"/>.
    /// </summary>
    public static DialogueDefinition Parse(string text)
    {
        object? json;
        try
        {
            json = JsonText.Decode(text);
        }
        catch (FormatException error)
        {
            throw new DialogueFormatException($"This is not a dialogue file: {error.Message}");
        }

        if (json is not IReadOnlyDictionary<string, object?> map || map.GetValueOrDefault("kind") is not Marker)
        {
            throw new DialogueFormatException("This is not a dialogue file.");
        }

        // A whole number reads as a long. 1.0 is a double, so it is no version.
        var written = map.GetValueOrDefault("formatVersion") is long whole ? whole : 0L;
        if (written < 1)
        {
            throw new DialogueFormatException("This file has no format version.");
        }

        return written > FormatVersion
            ? throw new DialogueFormatException("This is from a newer Orblit.")
            : Build(map);
    }

    /// <summary>
    /// The node a conversation starts at for a player whose table is <paramref name="table"/>,
    /// or null when no variant holds. It changes nothing and runs no hook, so a
    /// game can ask it to see whether a character has something to say.
    /// </summary>
    public string? StartFor(ProgressTable table) =>
        Variants.FirstOrDefault(variant => Condition.AllHold(variant.When, table))?.Start;

    public string Encode() => JsonText.Encode(
        new OrderedDictionary<string, object?>
        {
            ["kind"] = Marker,
            ["formatVersion"] = FormatVersion,
            ["key"] = Key,
            ["variants"] = Variants.Select(VariantJson).ToList(),
            ["nodes"] = NodesJson(),
        },
        "  ");

    private OrderedDictionary<string, object?> NodesJson()
    {
        var json = new OrderedDictionary<string, object?>();
        foreach (var node in Nodes.Values)
        {
            json[node.Id] = node.ToJson();
        }

        return json;
    }

    private static object? VariantJson(Variant variant)
    {
        var json = new OrderedDictionary<string, object?> { ["start"] = variant.Start };
        if (variant.When.Count > 0)
        {
            json["when"] = variant.When.Select(c => (object?)c.ToJson()).ToList();
        }

        return json;
    }

    /// <summary>A variant with no conditions always holds, so any after it is dead.</summary>
    private void CheckVariants()
    {
        for (var i = 0; i < Variants.Count - 1; i++)
        {
            if (Variants[i].When.Count == 0)
            {
                throw new RuleException("variants", "has no conditions, so the variants after it never start", Variants[i].Start);
            }
        }
    }

    private void CheckTargets()
    {
        if (Variants.FirstOrDefault(v => !Nodes.ContainsKey(v.Start)) is { } lost)
        {
            throw new RuleException("variants", "is not a node", lost.Start);
        }

        foreach (var node in Nodes.Values)
        {
            if (node.LeadsTo.FirstOrDefault(t => !Nodes.ContainsKey(t)) is { } target)
            {
                throw new RuleException(node.Id, "leads to no node", target);
            }
        }
    }

    /// <summary>A set or a jump shows nothing, so a ring of them would never stop.</summary>
    private void CheckNoSilentLoop()
    {
        foreach (var node in Nodes.Values)
        {
            var seen = new HashSet<string>();
            Node? at = node;
            while (at is SetNode or JumpNode)
            {
                if (!seen.Add(at.Id))
                {
                    throw new RuleException("nodes", "loop without a line", at.Id);
                }

                var next = at.LeadsTo.FirstOrDefault();
                at = next is null ? null : Nodes[next];
            }
        }
    }

    private static DialogueDefinition Build(IReadOnlyDictionary<string, object?> json)
    {
        if (json.GetValueOrDefault("key") is not string key
            || json.GetValueOrDefault("variants") is not IReadOnlyList<object?> variants
            || json.GetValueOrDefault("nodes") is not IReadOnlyDictionary<string, object?> nodes)
        {
            throw new DialogueFormatException("A dialogue has a key, a list of variants and a map of nodes.");
        }

        try
        {
            return new DialogueDefinition(
                key,
                [.. variants.Select(DialogueReader.VariantFrom)],
                [.. nodes.Select(entry => DialogueReader.NodeFrom(entry.Key, entry.Value))]);
        }
        catch (FormatException error)
        {
            throw new DialogueFormatException(error.Message);
        }
        catch (RuleException error)
        {
            throw new DialogueFormatException(error.Message);
        }
    }
}

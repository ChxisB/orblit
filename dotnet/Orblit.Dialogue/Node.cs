using System.Collections.ObjectModel;
using Orblit.Progress;

namespace Orblit.Dialogue;

/// <summary>
/// One step of a conversation. <see cref="Before"/> names a function the game registered,
/// which the runner calls before it shows or runs the node.
/// </summary>
public abstract class Node
{
    private protected Node(string id, string? before)
    {
        if (id.Length == 0)
        {
            throw new RuleException("id", "is empty", id);
        }

        if (before is { Length: 0 })
        {
            throw new RuleException("before", "is empty", before);
        }

        Id = id;
        Before = before;
    }

    public string Id { get; }

    public string? Before { get; }

    /// <summary>The ids of the nodes this one can lead to.</summary>
    public abstract IEnumerable<string> LeadsTo { get; }

    public abstract IReadOnlyDictionary<string, object?> ToJson();

    private protected static OrderedDictionary<string, object?> Typed(string type) => new() { ["type"] = type };

    private protected void AddTail(OrderedDictionary<string, object?> json, string? next)
    {
        if (next is not null)
        {
            json["next"] = next;
        }

        if (Before is not null)
        {
            json["before"] = Before;
        }
    }
}

/// <summary>
/// Someone says <see cref="Text"/>. The conversation moves on when the player does.
/// <see cref="Next"/> is null when the conversation ends here.
/// </summary>
public sealed class LineNode : Node
{
    // F2: five parameters, three of them required and all called by name, as Dart's required named parameters were.
    public LineNode(string id, string speaker, string text, string? next = null, string? before = null)
        : base(id, before)
    {
        Speaker = speaker;
        Text = text;
        Next = next;
    }

    public string Speaker { get; }

    public string Text { get; }

    public string? Next { get; }

    public override IEnumerable<string> LeadsTo => Next is null ? [] : [Next];

    public override IReadOnlyDictionary<string, object?> ToJson()
    {
        var json = Typed("line");
        json["speaker"] = Speaker;
        json["text"] = Text;
        AddTail(json, Next);
        return json;
    }
}

/// <summary>
/// What a player can pick. It is offered only while every condition in <see cref="When"/>
/// holds. <see cref="Next"/> is null when picking it ends the conversation.
/// </summary>
public sealed class ChoiceOption
{
    public ChoiceOption(string text, string? next, IReadOnlyList<Condition>? when = null)
    {
        Text = text;
        Next = next;
        When = new ReadOnlyCollection<Condition>([.. when ?? []]);
    }

    public string Text { get; }

    public string? Next { get; }

    public IReadOnlyList<Condition> When { get; }

    public IReadOnlyDictionary<string, object?> ToJson()
    {
        var json = new OrderedDictionary<string, object?> { ["text"] = Text };
        if (Next is not null)
        {
            json["next"] = Next;
        }

        if (When.Count > 0)
        {
            json["when"] = When.Select(c => (object?)c.ToJson()).ToList();
        }

        return json;
    }
}

/// <summary>The player picks one of <see cref="Options"/>. One with nothing to offer ends the conversation.</summary>
public sealed class ChoiceNode : Node
{
    public ChoiceNode(string id, IReadOnlyList<ChoiceOption> options, string? before = null)
        : base(id, before)
    {
        Options = new ReadOnlyCollection<ChoiceOption>([.. options]);
        if (options.Count == 0)
        {
            throw new RuleException("id", "has no options", id);
        }
    }

    public IReadOnlyList<ChoiceOption> Options { get; }

    public override IEnumerable<string> LeadsTo => Options.Select(o => o.Next).OfType<string>();

    public override IReadOnlyDictionary<string, object?> ToJson()
    {
        var json = Typed("choice");
        json["options"] = Options.Select(o => (object?)o.ToJson()).ToList();
        AddTail(json, null);
        return json;
    }
}

/// <summary>A value to write: <see cref="Name"/> becomes <see cref="Value"/>.</summary>
public sealed class Write
{
    public Write(string name, long value)
    {
        if (name.Length == 0)
        {
            throw new RuleException("name", "is empty", name);
        }

        if (value is < 0 or > 0xFFFFFFFF)
        {
            throw new RuleException("value", "cannot be stored", value);
        }

        Name = name;
        Value = value;
    }

    public string Name { get; }

    public long Value { get; }
}

/// <summary>Writes values in the player's table and moves on without showing anything.</summary>
public sealed class SetNode : Node
{
    public SetNode(string id, IReadOnlyList<Write> writes, string? next = null, string? before = null)
        : base(id, before)
    {
        Writes = new ReadOnlyCollection<Write>([.. writes]);
        Next = next;
    }

    public IReadOnlyList<Write> Writes { get; }

    public string? Next { get; }

    public override IEnumerable<string> LeadsTo => Next is null ? [] : [Next];

    public override IReadOnlyDictionary<string, object?> ToJson()
    {
        var json = Typed("set");
        json["writes"] = Writes.Select(w => (object?)new OrderedDictionary<string, object?> { ["name"] = w.Name, ["value"] = w.Value }).ToList();
        AddTail(json, Next);
        return json;
    }
}

/// <summary>Moves on to <see cref="Next"/> without showing anything.</summary>
public sealed class JumpNode(string id, string next, string? before = null) : Node(id, before)
{
    public string Next { get; } = next;

    public override IEnumerable<string> LeadsTo => [Next];

    public override IReadOnlyDictionary<string, object?> ToJson()
    {
        var json = Typed("jump");
        AddTail(json, Next);
        return json;
    }
}

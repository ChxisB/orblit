using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Orblit.Json;
using Orblit.Progress;

namespace Orblit.Quest;

/// <summary>Names a quest file and the progress table carry.</summary>
public static class QuestNames
{
    /// <summary>The extension a quest file carries.</summary>
    public const string Extension = ".oquest";

    /// <summary>The name a quest's stage has in the progress table.</summary>
    public static string StageName(string key) => $"quest.{key}.stage";
}

/// <summary>A file that is not a quest, or is one from a newer Orblit.</summary>
public sealed class QuestFormatException(string message) : Exception(message);

/// <summary>
/// One step of a quest: the text the journal shows while the player is in it,
/// and what must hold before the player may enter it.
/// </summary>
public sealed class Stage
{
    public Stage(string id, string journal, IReadOnlyList<Condition>? requires = null)
    {
        if (id.Length == 0)
        {
            throw new RuleException("id", "is empty", id);
        }

        Id = id;
        Journal = journal;
        Requires = new ReadOnlyCollection<Condition>([.. requires ?? []]);
    }

    public string Id { get; }

    public string Journal { get; }

    public IReadOnlyList<Condition> Requires { get; }

    public IReadOnlyDictionary<string, object?> ToJson()
    {
        var json = new OrderedDictionary<string, object?> { ["id"] = Id, ["journal"] = Journal };
        if (Requires.Count > 0)
        {
            json["requires"] = Requires.Select(c => (object?)c.ToJson()).ToList();
        }

        return json;
    }
}

/// <summary>
/// A quest: stages in order, from the one a player has not begun to the one
/// that is complete.
/// A player's place is one integer, the index of a stage. A quest goes
/// forward only. One that branches uses more than one name in the table.
/// </summary>
public sealed partial class QuestDefinition
{
    public const string Marker = "orblit.quest";

    public const int FormatVersion = 1;

    public QuestDefinition(string key, string title, IReadOnlyList<Stage> stages)
    {
        if (!KeyPattern().IsMatch(key))
        {
            throw new RuleException("key", "is not letters, digits, _ or -", key);
        }

        if (title.Length == 0)
        {
            throw new RuleException("title", "is empty", title);
        }

        if (stages.Count < 2)
        {
            throw new RuleException("stages", "needs a start and an end", stages.Count);
        }

        var ids = new HashSet<string>();
        foreach (var stage in stages)
        {
            if (!ids.Add(stage.Id))
            {
                throw new RuleException("stages", "repeats a stage id", stage.Id);
            }
        }

        Key = key;
        Title = title;
        Stages = new ReadOnlyCollection<Stage>([.. stages]);
    }

    public string Key { get; }

    public string Title { get; }

    public IReadOnlyList<Stage> Stages { get; }

    /// <summary>The index of the stage that is complete.</summary>
    public int Last => Stages.Count - 1;

    /// <summary>
    /// Reads the text of a <c>.oquest</c> file. Throws <see cref="QuestFormatException"/> for text that is not
    /// one, for one written by a newer Orblit, and for one that breaks a rule of
    /// <see cref="QuestDefinition"/> or <see cref="Condition"/>.
    /// </summary>
    public static QuestDefinition Parse(string text)
    {
        var map = ReadHeader(text);
        if (map.GetValueOrDefault("key") is not string key
            || map.GetValueOrDefault("title") is not string title
            || map.GetValueOrDefault("stages") is not IReadOnlyList<object?> stages)
        {
            throw new QuestFormatException("A quest has a key, a title and a list of stages.");
        }

        try
        {
            return new QuestDefinition(key, title, [.. stages.Select(StageFromJson)]);
        }
        catch (FormatException error)
        {
            throw new QuestFormatException(error.Message);
        }
        catch (RuleException error)
        {
            throw new QuestFormatException(error.Message);
        }
    }

    public string Encode() => JsonText.Encode(
        new OrderedDictionary<string, object?>
        {
            ["kind"] = Marker,
            ["formatVersion"] = FormatVersion,
            ["key"] = Key,
            ["title"] = Title,
            ["stages"] = Stages.Select(stage => (object?)stage.ToJson()).ToList(),
        },
        "  ");

    [GeneratedRegex(@"^[A-Za-z0-9_-]+\z")]
    private static partial Regex KeyPattern();

    private static IReadOnlyDictionary<string, object?> ReadHeader(string text)
    {
        object? json;
        try
        {
            json = JsonText.Decode(text);
        }
        catch (FormatException error)
        {
            throw new QuestFormatException($"This is not a quest file: {error.Message}");
        }

        if (json is not IReadOnlyDictionary<string, object?> map || map.GetValueOrDefault("kind") is not Marker)
        {
            throw new QuestFormatException("This is not a quest file.");
        }

        // A whole number reads as a long. 1.0 is a double, so it is no version.
        var written = map.GetValueOrDefault("formatVersion") is long whole ? whole : 0L;
        if (written < 1)
        {
            throw new QuestFormatException("This quest has no format version.");
        }

        if (written > FormatVersion)
        {
            throw new QuestFormatException("This quest is from a newer Orblit.");
        }

        return map;
    }

    private static Stage StageFromJson(object? json)
    {
        if (json is not IReadOnlyDictionary<string, object?> map)
        {
            throw new FormatException("A stage is an object.");
        }

        if (map.GetValueOrDefault("id") is not string id || map.GetValueOrDefault("journal") is not string journal)
        {
            throw new FormatException("A stage has an id and journal text.");
        }

        return new Stage(id, journal, Condition.ListFromJson(map.GetValueOrDefault("requires")));
    }
}

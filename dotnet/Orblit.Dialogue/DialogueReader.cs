using Orblit.Progress;

namespace Orblit.Dialogue;

/// <summary>Reads the parts of a <c>.odialogue</c> file. A part that is the wrong shape is a <see cref="FormatException"/>.</summary>
internal static class DialogueReader
{
    internal static Variant VariantFrom(object? json)
    {
        var map = Object(json, "A variant");
        return new Variant(Text(map, "start"), Condition.ListFromJson(map.GetValueOrDefault("when")));
    }

    internal static Node NodeFrom(string id, object? json)
    {
        var map = Object(json, $"Node \"{id}\"");
        var before = MaybeText(map, "before");
        var type = map.GetValueOrDefault("type");
        return type switch
        {
            "line" => new LineNode(id, Text(map, "speaker"), Text(map, "text"), MaybeText(map, "next"), before),
            "choice" => new ChoiceNode(id, [.. List(map, "options").Select(OptionFrom)], before),
            "set" => new SetNode(id, [.. List(map, "writes").Select(WriteFrom)], MaybeText(map, "next"), before),
            "jump" => new JumpNode(id, Text(map, "next"), before),
            _ => throw new FormatException($"Node \"{id}\" has type {Printed.Text(type)}, not line, choice, set or jump."),
        };
    }

    private static ChoiceOption OptionFrom(object? json)
    {
        var map = Object(json, "An option");
        return new ChoiceOption(Text(map, "text"), MaybeText(map, "next"), Condition.ListFromJson(map.GetValueOrDefault("when")));
    }

    private static Write WriteFrom(object? json)
    {
        var map = Object(json, "A write");
        return map.GetValueOrDefault("value") is long value
            ? new Write(Text(map, "name"), value)
            : throw new FormatException("A write needs a whole number.");
    }

    private static IReadOnlyDictionary<string, object?> Object(object? json, string what) =>
        json as IReadOnlyDictionary<string, object?>
        ?? throw new FormatException($"{what} is an object, not {Printed.Text(json)}.");

    private static string Text(IReadOnlyDictionary<string, object?> json, string field) =>
        json.GetValueOrDefault(field) as string
        ?? throw new FormatException($"\"{field}\" is text, not {Printed.Text(json.GetValueOrDefault(field))}.");

    private static string? MaybeText(IReadOnlyDictionary<string, object?> json, string field)
    {
        var value = json.GetValueOrDefault(field);
        return value is null or string
            ? (string?)value
            : throw new FormatException($"\"{field}\" is text, not {Printed.Text(value)}.");
    }

    private static IReadOnlyList<object?> List(IReadOnlyDictionary<string, object?> json, string field) =>
        json.GetValueOrDefault(field) as IReadOnlyList<object?>
        ?? throw new FormatException($"\"{field}\" is a list, not {Printed.Text(json.GetValueOrDefault(field))}.");
}

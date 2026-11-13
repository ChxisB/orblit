using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Orblit.Editor.Bridge;

/// <summary>Whether a port carries the order steps run in or a value.</summary>
public enum GraphPortSort
{
    Flow,
    Data,
}

/// <summary>One input or output of a step. Type is the engine's Variant type number.</summary>
public sealed record GraphPort(string Name, GraphPortSort Sort, int Type, string Parameter, bool Optional);

/// <summary>The side on which a step takes more pins.</summary>
public enum GraphGrowthSide
{
    None,
    Input,
    Output,
}

/// <summary>Whether a step takes more pins on one side, and how many it has now. A step that does not grow has side None.</summary>
public sealed record GraphGrowth(GraphGrowthSide Side, int Minimum, int Count, int Maximum);

/// <summary>
/// One step of a function. Parameters are Variant text, the same text a parameter is set with.
/// A step of a kind the engine does not know has no ports and Known is false.
/// </summary>
public sealed record GraphStep(
    int Id,
    string Kind,
    bool Known,
    string Category,
    double X,
    double Y,
    IReadOnlyDictionary<string, string> Parameters,
    string Problem,
    IReadOnlyList<GraphPort> Inputs,
    IReadOnlyList<GraphPort> Outputs,
    GraphGrowth Growth);

public sealed record GraphWire(int FromStep, int FromPort, int ToStep, int ToPort);

/// <summary>A labeled rectangle behind steps. It does not change how the function runs.</summary>
public sealed record GraphFrame(int Id, string Title, double X, double Y, double Width, double Height, bool Collapsed);

/// <summary>A function with the error the engine found when it compiled it. The error is empty when it compiles.</summary>
public sealed record GraphFunction(string Name, string Error, IReadOnlyList<GraphStep> Steps, IReadOnlyList<GraphWire> Wires, IReadOnlyList<GraphFrame> Frames);

/// <summary>A variable. Type is a Variant type number, Hint a PropertyHint number, and Default Variant text.</summary>
public sealed record GraphVariable(string Name, int Type, string Default, bool Exported, int Hint, string HintString, string Category, string Description);

/// <summary>A name with a Variant type number: an argument, a local, or a field of a signal.</summary>
public sealed record GraphTyped(string Name, int Type)
{
    /// <summary>The Variant text the engine takes for a list of these, such as <c>[{"name":"x","type":3}]</c>.</summary>
    public static string WriteList(IReadOnlyList<GraphTyped> list) => GraphJson.Write(list);

    /// <summary>
    /// Reads the text the engine stores for a list of these. Text that is not a list of names and types reads as
    /// an empty list, because a step the engine refuses is shown as it is and not fixed here.
    /// </summary>
    public static IReadOnlyList<GraphTyped> ReadList(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        try
        {
            return GraphJson.Read<List<GraphTyped>>(text);
        }
        catch (InvalidDataException)
        {
            return [];
        }
    }
}

/// <summary>Variant text for a string, and the string out of Variant text.</summary>
public static class VariantText
{
    public static string Quote(string text) => GraphJson.Write(text);

    /// <summary>The string a text holds. Text that is not a quoted string comes back as it is.</summary>
    public static string Unquote(string? text)
    {
        if (text is null || !text.StartsWith('"'))
        {
            return text ?? "";
        }

        try
        {
            return GraphJson.Read<string>(text);
        }
        catch (InvalidDataException)
        {
            return text;
        }
    }
}

public sealed record GraphSignal(string Name, IReadOnlyList<GraphTyped> Arguments);

/// <summary>Something the engine found that is not an error, such as a variable nothing reads.</summary>
public sealed record GraphWarning(string Kind, string Function, int Step, string Name, string Text);

/// <summary>What the engine holds for one graph script, read from the text <c>get_document</c> returns.</summary>
public sealed record GraphDocument(
    string BaseType,
    string Error,
    IReadOnlyList<GraphVariable> Variables,
    IReadOnlyList<GraphSignal> Signals,
    IReadOnlyList<GraphFunction> Functions,
    IReadOnlyList<GraphWarning> Warnings)
{
    /// <exception cref="InvalidDataException">The text is not a document the engine writes.</exception>
    public static GraphDocument Parse(string json) => GraphJson.Read<GraphDocument>(json);
}

/// <summary>A step the palette offers. Parameters are the values the new step starts with, as Variant text.</summary>
public sealed record GraphStepEntry(string Kind, string Group, string Label, IReadOnlyDictionary<string, string> Parameters)
{
    /// <exception cref="InvalidDataException">The text is not a list the engine writes.</exception>
    public static IReadOnlyList<GraphStepEntry> ParseList(string json) => GraphJson.Read<List<GraphStepEntry>>(json);
}

static class GraphJson
{
    static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        // The engine reads what is written here as Variant text, so letters stay as they are and are not escaped.
        // shortcut: a character outside the basic plane, such as an emoji, is still escaped as two halves, and
        // the engine reads two broken characters. Parse real JSON in the engine if frame titles need emoji.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    internal static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);

    // An empty or broken text is the engine refusing a call, so it is reported as data that cannot be read.
    internal static T Read<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Options) ?? throw new InvalidDataException("The engine gave no document.");
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("The engine gave text that is not a graph document.", error);
        }
    }
}

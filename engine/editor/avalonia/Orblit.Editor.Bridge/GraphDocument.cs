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
    IReadOnlyList<GraphPort> Outputs);

public sealed record GraphWire(int FromStep, int FromPort, int ToStep, int ToPort);

/// <summary>A function with the error the engine found when it compiled it. The error is empty when it compiles.</summary>
public sealed record GraphFunction(string Name, string Error, IReadOnlyList<GraphStep> Steps, IReadOnlyList<GraphWire> Wires);

public sealed record GraphVariable(string Name, int Type, string Default, bool Exported, string Hint, string HintString, string Category, string Description);

public sealed record GraphSignalArgument(string Name, int Type);

public sealed record GraphSignal(string Name, IReadOnlyList<GraphSignalArgument> Arguments);

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
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

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

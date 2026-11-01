using Orblit.Json;

namespace Orblit.Motion;

/// <summary>One step between two formats of a file: decoded JSON at <see cref="From"/> in, at <see cref="To"/> out.</summary>
internal interface IFormatStep
{
    int From { get; }

    int To { get; }

    /// <summary>The file moved on one version, with a note in <paramref name="notes"/> for anything somebody should hear about.</summary>
    JsonMap Apply(JsonMap json, List<string> notes);
}

/// <summary>
/// How to open one kind of Orblit file. Clips, blends and cutscenes are opened the same way: the
/// text must be JSON that says which kind of file it is, and a file from a newer Orblit is refused
/// rather than half read.
/// </summary>
/// <param name="Marker">What the file's <c>kind</c> says it is.</param>
/// <param name="Noun">What the file is called in a sentence, such as <c>clip</c>.</param>
/// <param name="Version">The format this Orblit writes.</param>
/// <param name="Fail">What is thrown for a file that cannot be opened, made from what somebody is told.</param>
/// <param name="Steps">Every step up from an older format, oldest first.</param>
internal sealed record FileFormat(
    string Marker,
    string Noun,
    int Version,
    Func<string, Exception> Fail,
    IReadOnlyList<IFormatStep> Steps)
{
    /// <summary>The decoded JSON of <paramref name="text"/>, brought up to <see cref="Version"/>, with a note in <paramref name="problems"/> for anything a step changed.</summary>
    internal JsonMap Open(string text, List<string> problems)
    {
        object? parsed;
        try
        {
            parsed = JsonText.Decode(text);
        }
        catch (FormatException error)
        {
            throw Fail($"This is not a {Noun} file: {error.Message}");
        }

        if (parsed is not JsonMap json || json.GetValueOrDefault("kind") as string != Marker)
        {
            throw Fail($"This is not a {Noun} file.");
        }

        if (json.GetValueOrDefault("formatVersion") is not long written)
        {
            return json;
        }

        if (written > Version)
        {
            throw Fail($"This {Noun} was written by a newer Orblit.");
        }

        foreach (var step in Steps.Where(step => step.From >= written))
        {
            json = step.Apply(json, problems);
        }

        return json;
    }
}

namespace Orblit.Examples;

/// <summary>
/// One control in an example's panel. The example says what it is and what a
/// change does. The host draws it and redraws the panel after a change.
/// </summary>
public abstract record ExampleSetting
{
    private protected ExampleSetting(string label) => Label = label;

    public string Label { get; }
}

/// <summary>A named slider.</summary>
public sealed record Setting(string Label, double Value, double Min, double Max, Action<double> Change)
    : ExampleSetting(Label)
{
    /// <summary>
    /// Called once, when the handle is let go. For a setting whose answer is
    /// expensive, because a slider reports every pixel of a drag.
    /// </summary>
    public Action<double>? Settle { get; init; }

    public string Unit { get; init; } = "";

    public int Decimals { get; init; } = 2;
}

/// <summary>
/// A row of things to pick between. A null <see cref="Select"/> is a row with
/// nothing to act on yet, shown greyed rather than removed, so the reader still
/// sees that it is there.
/// </summary>
public sealed record Choice(string Label, IReadOnlyList<string> Options, string Selected, Action<string>? Select)
    : ExampleSetting(Label);

/// <summary>A switch, for a setting that is one thing or the other.</summary>
public sealed record Toggle(string Label, bool Value, Action<bool> Change) : ExampleSetting(Label)
{
    /// <summary>What the setting means now, for a switch whose two states are not equally obvious.</summary>
    public string? Note { get; init; }

    /// <summary>False for a setting with nothing to act on yet. Shown rather than hidden.</summary>
    public bool Enabled { get; init; } = true;
}

/// <summary>A line to type into, such as a path. Each edit is a change.</summary>
public sealed record TextEntry(string Label, string Value, Action<string> Change) : ExampleSetting(Label)
{
    /// <summary>An example of what goes in, shown while it is empty.</summary>
    public string Hint { get; init; } = "";

    /// <summary>A button beside the line that picks a file. Its path is a change, as typing one is.</summary>
    public FilePick? Pick { get; init; }
}

/// <summary>What a file picker says and which files it offers.</summary>
/// <param name="Button">The button's text.</param>
/// <param name="Extensions">The extensions it offers, without the dot.</param>
public sealed record FilePick(string Button, IReadOnlyList<string> Extensions);

/// <summary>A row of buttons. A press is an event rather than a state, so no button stays chosen.</summary>
public sealed record Buttons(string Label, IReadOnlyList<string> Options, Action<string> Press) : ExampleSetting(Label);

/// <summary>A line of text between the controls, for what the controls do not say. The label is the text.</summary>
public sealed record Caption(string Label) : ExampleSetting(Label);

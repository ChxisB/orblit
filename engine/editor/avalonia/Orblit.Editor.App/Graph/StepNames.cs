using Orblit.Editor.App.Shell;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>
/// A kind of step in plain words: its name, a line on what it does, more on how it works, and what it is good for.
/// <see cref="Engine"/> is the label the engine gives the step.
/// </summary>
internal sealed record StepName(string Kind, string Engine, string Name, string Line, string About, string GoodFor)
{
    /// <summary>The engine label to show beside the plain name, or none when the two are the same.</summary>
    public string? EngineBeside => Name == Engine ? null : Engine;
}

/// <summary>
/// The plain words of the steps the Add a step list shows before any search. A step with no row goes by its engine
/// label. The engine keeps no description of a step kind, so the rows hold their own.
/// </summary>
internal static class StepNames
{
    // A row matches the kind and the label, so a step found by search keeps its own label. A list of cases, so it is data.
    static readonly StepName[] s_rows =
    [
        new("branch", "Branch", "If", "Goes one way or the other.", "If the check is true, go one way. If not, go the other.", "doors that need a key, enemies that only chase the player."),
        new("while", "While", "Repeat while", "Keeps going while a check is true.", "Runs the steps on its loop again and again, as long as the check stays true.", "searching a list, counting down to zero."),
        new("count_loop", "Count loop", "Repeat a number of times", "Runs its steps a set number of times.", "Runs the steps on its loop once for each number, then carries on.", "spawning five enemies, a row of coins."),
        new("each", "Each", "For each item", "Runs its steps once for every item in a list.", "Goes through a list and runs the steps on its loop with each item in turn.", "every enemy in a group, every item in a bag."),
        new("sequence", "Sequence", "Do in order", "Runs several chains one after another.", "Runs the chain on each Then pin in turn, top to bottom.", "keeping a long graph tidy, setup in parts."),
        new("chance", "Chance", "Maybe", "Goes one way by chance.", "Rolls the dice. The chance you set picks the way it goes.", "loot drops, a lucky hit, a random line."),
        new("pick_random", "Pick random", "Pick one at random", "Gives back one item of a list by chance.", "Picks one item of the list joined to it. Each item is as likely as the others.", "a random sound, a random spawn point."),
        new("select", "Select", "Choose a value", "Gives back one of two values by a check.", "If the check is true, it gives back the first value. If not, the second.", "speed when running or walking, a word for on or off."),
        new("switch", "Switch", "Match a value", "Goes the way that matches a value.", "Compares a value with each case and runs the one that matches, or Default.", "states like idle, run and jump, menu choices."),
        new("break", "Break", "Stop the loop", "Leaves the loop it is in.", "Stops the loop it sits in at once. The step after the loop runs next.", "stopping a search once it finds what it wants."),
        new("return", "Return", "Finish", "Ends this function here.", "Stops this function and gives back a value, if it has one.", "leaving early when there is nothing to do."),
        new("wait", "Wait", "Wait", "Waits some seconds, then carries on.", "Pauses this chain for the seconds you set. The rest of the game keeps going.", "a delay before a door shuts, a timed hint."),
        new("await_signal", "Await signal", "Wait for a signal", "Waits until something happens, like a door opening.", "Pauses this chain until the thing you pick sends the signal you name.", "waiting for an animation to end, a timer to run out."),
        new("constant", "Constant", "Fixed value", "A value that never changes.", "Gives back the number, text or other value you type.", "speeds, names, how many lives."),
        new("self", "Self", "This thing", "The thing this script is on.", "Gives back the thing that runs this script.", "moving itself, reading its own settings."),
        new("entity_at_path", "Entity at path", "Find a thing by path", "Finds a thing in the scene by its path.", "Gives back the thing at a path such as Player/Camera.", "reaching a child, finding the player."),
        new("instantiate_prefab", "Instantiate prefab", "Add a copy of a scene", "Makes a copy of a saved scene.", "Makes a new copy of the scene joined to it, ready to add to the game.", "bullets, enemies that spawn, pickups."),
        new("print", "Print", "Print text", "Shows text in Output.", "Writes the text to the Output panel while the game runs.", "checking a value, finding out if a step ran."),
        new("free", "Free", "Remove", "Deletes a thing from the game.", "Removes the thing and frees its memory. Nothing can use it after this.", "picked-up coins, enemies that die, used bullets."),
    ];

    // The engine's groups in plain words. A group missing here is a class name, or reads well as it is.
    static readonly Dictionary<string, string> s_groups = new()
    {
        ["Flow"] = "Choices and loops",
        ["Variables"] = "Remembered values",
        ["Input"] = "Keys and clicks",
        ["Operators"] = "Math and compare",
        ["Calls"] = "Do something to a thing",
        ["Debug"] = "Checking",
        ["Objects"] = "Things",
    };

    /// <summary>The row for this step, or none.</summary>
    internal static StepName? Of(GraphStepEntry entry) => s_rows.FirstOrDefault(row => row.Kind == entry.Kind && row.Engine == entry.Label);

    /// <summary>The row for a step on the graph, which has a kind and no label. Each kind has one row at most.</summary>
    internal static StepName? OfKind(string kind) => s_rows.FirstOrDefault(row => row.Kind == kind);

    /// <summary>The plain name of a step on the graph. A kind with no row goes by the kind, with its underscores as spaces.</summary>
    internal static string NameOf(string kind) => OfKind(kind)?.Name ?? char.ToUpperInvariant(kind[0]) + kind[1..].Replace('_', ' ');

    /// <summary>The plain name of a group. A group named after a class takes the class's plain name.</summary>
    internal static string GroupOf(string group) => s_groups.GetValueOrDefault(group) ?? PlainNames.NameOf(group);
}

namespace Orblit.Editor.App.Graph;

/// <summary>
/// The steps a person wants the game to stop on, for every script of the editor. A breakpoint is named the way
/// the engine names the place it stops: a source, which is the script path and the function, and a step id.
/// </summary>
// shortcut: breakpoints live in memory and are lost when the editor closes. Save them in the project's editor
// data when the editor has such a file (decision 278).
internal sealed class GraphBreakpoints
{
    readonly Dictionary<string, HashSet<int>> _steps = [];

    /// <summary>Raised with the source, the step and whether the breakpoint is set after the change.</summary>
    public event Action<string, int, bool>? Changed;

    /// <summary>The source of a function, as the engine names it in a stop.</summary>
    public static string SourceOf(string script, string function) => $"{script}#{function}";

    /// <summary>The script and the function of a source, or null for a source that is not a graph function.</summary>
    public static (string Script, string Function)? Split(string source)
    {
        var at = source.LastIndexOf('#');
        return at < 0 ? null : (source[..at], source[(at + 1)..]);
    }

    /// <summary>Every breakpoint of every script, for a game that starts with them set.</summary>
    public IEnumerable<(string Source, int Step)> All => _steps.SelectMany(pair => pair.Value.Select(step => (pair.Key, step)));

    public bool Has(string source, int step) => _steps.TryGetValue(source, out var set) && set.Contains(step);

    public IReadOnlySet<int> Steps(string source) => _steps.GetValueOrDefault(source) ?? [];

    /// <summary>Sets the breakpoint or takes it off. True when it is set after the call.</summary>
    public bool Toggle(string source, int step)
    {
        if (!_steps.TryGetValue(source, out var set))
        {
            _steps[source] = set = [];
        }

        var isSet = set.Add(step);
        if (!isSet)
        {
            set.Remove(step);
        }

        Changed?.Invoke(source, step, isSet);
        return isSet;
    }

    /// <summary>Takes off the breakpoints of steps the function no longer has, since a step id can be used again.</summary>
    public void Keep(string source, IReadOnlyCollection<int> alive)
    {
        foreach (var step in Steps(source).Where(step => !alive.Contains(step)).ToList())
        {
            Toggle(source, step);
        }
    }
}

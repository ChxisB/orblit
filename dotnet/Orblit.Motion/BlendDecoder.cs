using Orblit.Scene;
using Orblit.Sequence;

namespace Orblit.Motion;

/// <summary>
/// A blend out of decoded JSON already at the current format: lenient about the parts, with a note in the
/// problems for each one left out, and strict about the whole.
/// </summary>
internal static class BlendDecoder
{
    internal static BlendLoad Decode(JsonMap json, List<string> problems)
    {
        var inputs = ReadInputs(json.GetValueOrDefault("inputs"), problems);
        var states = ReadStates(json.GetValueOrDefault("states"), problems);
        if (states.Count == 0)
        {
            throw new BlendFormatException("This blend has no state that could be read.");
        }

        var known = KnownNames(states);
        // The start is read before the changes so the notes come out in the order a reader meets them.
        var start = ReadStart(json, states, known, problems);
        var blend = new BlendDocument(
            name: Values.Text(json, "name") ?? "Blend",
            states: states,
            inputs: inputs,
            changes: ReadChanges(json.GetValueOrDefault("changes"), known, problems),
            start: start,
            fades: ReadFades(json.GetValueOrDefault("fades"), problems));
        foreach (var input in blend.InputsRead.Where(input => !blend.Inputs.ContainsKey(input)))
        {
            problems.Add($"The blend reads \"{input}\", which it does not declare, so it is nought until something sets it.");
        }

        return new BlendLoad(blend, problems);
    }

    private static OrderedDictionary<string, double> ReadInputs(object? raw, List<string> problems)
    {
        var inputs = new OrderedDictionary<string, double>();
        foreach (var (input, value) in Values.Object(raw))
        {
            if (JsonNumbers.Finite(value) is { } number)
            {
                inputs[input] = number;
                continue;
            }

            problems.Add($"The input \"{input}\" starts at something that is not a number, so it starts at nought.");
            inputs[input] = 0;
        }

        return inputs;
    }

    private static List<BlendState> ReadStates(object? raw, List<string> problems)
    {
        var states = new List<BlendState>();
        foreach (var one in raw as JsonList ?? Array.Empty<object?>())
        {
            if (one is JsonMap map && ReadState(map, states, problems) is { } state)
            {
                AppendState(states, state, problems);
            }
        }

        return states;
    }

    private static BlendState? ReadState(JsonMap raw, List<BlendState> states, List<string> problems)
    {
        if (Values.Text(raw, "name") is not { Length: > 0 } name)
        {
            problems.Add("A state with no name was left out.");
            return null;
        }

        if (states.Any(state => state.Name == name))
        {
            problems.Add($"Two states are called \"{name}\"; the second was left out.");
            return null;
        }

        if (BlendSource.FromJson(raw, $"the state \"{name}\"", problems) is not { } plays)
        {
            return null;
        }

        return new BlendState(
            name,
            plays,
            ReadSpeed(raw, name, problems),
            FileNames.Named<WhenDone>(raw.GetValueOrDefault("whenDone")),
            ReadSync(raw.GetValueOrDefault("sync"), name, problems));
    }

    private static double ReadSpeed(JsonMap raw, string state, List<string> problems)
    {
        var speed = Values.MaybeNumber(raw, "speed") ?? 1;
        if (double.IsFinite(speed) && speed >= 0)
        {
            return speed;
        }

        problems.Add($"The state \"{state}\" has a speed it cannot play at, so it plays at one.");
        return 1;
    }

    private static List<string> ReadSync(object? raw, string state, List<string> problems)
    {
        if (raw is null)
        {
            return [];
        }

        if (raw is JsonList { Count: >= 2 } list
            && list.All(static one => one is string { Length: > 0 })
            && list.Distinct().Count() == list.Count)
        {
            return list.Cast<string>().ToList();
        }

        problems.Add($"The state \"{state}\" has invalid sync markers, so it uses ordinary laps.");
        return [];
    }

    // A state is left out when its graph's leaves would clash with another state's names, or cannot be
    // made at all, such as a speed that overflows when a graph's own speed multiplies it.
    private static void AppendState(List<BlendState> states, BlendState state, List<string> problems)
    {
        var candidate = states.Append(state).ToList();
        try
        {
            var names = GraphExpansion.ExpandedStates(candidate).Select(static leaf => leaf.Name).ToList();
            if (names.Distinct().Count() != names.Count || GraphExpansion.GraphEntries(candidate).Keys.Any(names.Contains))
            {
                problems.Add($"The state \"{state.Name}\" conflicts with another graph path, so it was left out.");
                return;
            }

            states.Add(state);
        }
        catch (ArgumentException)
        {
            problems.Add($"The state \"{state.Name}\" could not be expanded, so it was left out.");
        }
    }

    private static Func<string, bool> KnownNames(List<BlendState> states)
    {
        var leaves = GraphExpansion.ExpandedStates(states).Select(static leaf => leaf.Name).ToHashSet(StringComparer.Ordinal);
        var entries = GraphExpansion.GraphEntries(states);
        return state => entries.ContainsKey(state) || leaves.Contains(state);
    }

    private static string ReadStart(JsonMap json, List<BlendState> states, Func<string, bool> known, List<string> problems)
    {
        var start = Values.Text(json, "start") ?? states[0].Name;
        if (known(start))
        {
            return start;
        }

        problems.Add($"The blend starts in \"{start}\", which is not one of its states, so it starts in \"{states[0].Name}\".");
        return states[0].Name;
    }

    private static List<BlendChange> ReadChanges(object? raw, Func<string, bool> known, List<string> problems)
    {
        var changes = new List<BlendChange>();
        foreach (var one in raw as JsonList ?? Array.Empty<object?>())
        {
            if (one is JsonMap map && ReadChange(map, known, problems) is { } change)
            {
                changes.Add(change);
            }
        }

        return changes;
    }

    private static BlendChange? ReadChange(JsonMap raw, Func<string, bool> known, List<string> problems)
    {
        var from = Values.Text(raw, "from");
        var to = Values.Text(raw, "to");
        var source = from is null ? "anywhere" : $"\"{from}\"";
        var where = $"The change from {source} to \"{to}\"";
        if (to is null || !known(to) || (from is not null && !known(from)))
        {
            problems.Add($"{where} is not between two of the states, so it was left out.");
            return null;
        }

        var when = raw.ContainsKey("when") ? BlendCondition.FromJson(raw["when"], where, problems) : BlendCondition.Always;
        if (when is null)
        {
            problems.Add($"{where} was left out, since its condition could not be read.");
            return null;
        }

        return new BlendChange(
            to,
            from,
            when,
            ReadFade(raw, where, problems),
            FileNames.Named<Easing>(raw.GetValueOrDefault("shape")) ?? Easing.Smooth,
            Values.Flag(raw, "inStep", fallback: false),
            Values.Flag(raw, "fromPose", fallback: false));
    }

    private static double ReadFade(JsonMap raw, string where, List<string> problems)
    {
        var fade = Values.MaybeNumber(raw, "fade") ?? 0;
        if (double.IsFinite(fade) && fade >= 0)
        {
            return fade;
        }

        problems.Add($"{where} fades for a time that is not one, so it cuts.");
        return 0;
    }

    private static List<BlendFade> ReadFades(object? raw, List<string> problems)
    {
        var fades = new List<BlendFade>();
        foreach (var one in raw as JsonList ?? Array.Empty<object?>())
        {
            if (one is not JsonMap entry)
            {
                problems.Add("A clip-pair fade could not be read.");
            }
            else if (ReadPairFade(entry) is { } fade)
            {
                fades.Add(fade);
            }
            else
            {
                problems.Add("A clip-pair fade needs two names and a finite time at least zero.");
            }
        }

        return fades;
    }

    private static BlendFade? ReadPairFade(JsonMap entry) =>
        Values.Text(entry, "from") is { Length: > 0 } from
        && Values.Text(entry, "to") is { Length: > 0 } to
        && Values.MaybeNumber(entry, "seconds") is { } seconds
        && double.IsFinite(seconds)
        && seconds >= 0
            ? new BlendFade(from, to, seconds)
            : null;
}

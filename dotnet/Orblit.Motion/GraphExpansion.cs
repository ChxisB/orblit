namespace Orblit.Motion;

/// <summary>
/// How a graph inside a state becomes states and changes of the blend that holds it: the leaves are
/// addressed as <c>state/leaf</c>, and a change into the parent enters the graph at its start.
/// </summary>
internal static class GraphExpansion
{
    internal static List<BlendState> ExpandedStates(IReadOnlyList<BlendState> states)
    {
        var expanded = new List<BlendState>();
        foreach (var state in states)
        {
            if (state.Plays is BlendGraph { Graph: var graph })
            {
                expanded.AddRange(graph.States.Select(leaf => Leaf(state, leaf)));
            }
            else
            {
                expanded.Add(state);
            }
        }

        return expanded;
    }

    internal static Dictionary<string, string> GraphEntries(IReadOnlyList<BlendState> states)
    {
        var entries = new Dictionary<string, string>();
        foreach (var state in states)
        {
            if (state.Plays is not BlendGraph { Graph: var graph })
            {
                continue;
            }

            entries[state.Name] = $"{state.Name}/{graph.Start}";
            foreach (var (key, value) in graph.Entries)
            {
                entries[$"{state.Name}/{key}"] = $"{state.Name}/{value}";
            }
        }

        return entries;
    }

    internal static OrderedDictionary<string, double> GraphInputs(IReadOnlyList<BlendState> states)
    {
        var inputs = new OrderedDictionary<string, double>();
        foreach (var state in states)
        {
            if (state.Plays is not BlendGraph { Graph: var graph })
            {
                continue;
            }

            foreach (var (key, value) in graph.Inputs)
            {
                inputs[key] = value;
            }
        }

        return inputs;
    }

    internal static IReadOnlyList<BlendChange> ExpandedChanges(IReadOnlyList<BlendState> states, IReadOnlyList<BlendChange> changes)
    {
        var entries = GraphEntries(states);
        var leaves = ExpandedStates(states);
        var expanded = changes.SelectMany(change => Outer(change, entries, leaves)).ToList();
        foreach (var state in states)
        {
            if (state.Plays is BlendGraph { Graph: var graph })
            {
                expanded.AddRange(Inner(state.Name, graph));
            }
        }

        return expanded.AsReadOnly();
    }

    private static BlendState Leaf(BlendState parent, BlendState leaf) => new(
        $"{parent.Name}/{leaf.Name}",
        leaf.Plays,
        parent.Speed * leaf.Speed,
        leaf.WhenDone ?? parent.WhenDone,
        leaf.Sync.Count == 0 ? parent.Sync : leaf.Sync);

    // A change out of a graph leaves from each of its leaves, and one into a graph enters at its start.
    private static List<BlendChange> Outer(BlendChange change, Dictionary<string, string> entries, List<BlendState> leaves)
    {
        var from = change.From;
        var sources = from is not null && entries.ContainsKey(from)
            ? leaves.Where(leaf => leaf.Name.StartsWith($"{from}/", StringComparison.Ordinal)).Select(static leaf => (string?)leaf.Name).ToList()
            : new List<string?> { from };
        var to = entries.GetValueOrDefault(change.To) ?? change.To;
        return sources
            .Select(source => source == from && to == change.To ? change : change.CopyWith(from: source, clearFrom: source is null, to: to))
            .ToList();
    }

    private static IEnumerable<BlendChange> Inner(string name, BlendDocument graph)
    {
        foreach (var change in graph.Changes)
        {
            var sources = change.From is null
                ? graph.States.Where(leaf => leaf.Name != change.To).Select(static leaf => leaf.Name)
                : new[] { change.From };
            foreach (var source in sources)
            {
                yield return change.CopyWith(from: $"{name}/{source}", to: $"{name}/{change.To}");
            }
        }
    }
}

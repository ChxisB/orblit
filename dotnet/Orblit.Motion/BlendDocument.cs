using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Orblit.Json;
using Orblit.Sequence;

namespace Orblit.Motion;

/// <summary>
/// Which clips play, and how much each has to say: a graph of states, each playing a clip or a mix of clips,
/// and changes between them, each with the condition that takes it and how long its fade is.
/// </summary>
/// <remarks>
/// A blend is an asset, like a clip, and like a clip it never changes while it plays. Where it is playing is
/// a <see cref="BlendPlace"/>, a value on its own, and what drives it is inputs, named numbers that gameplay
/// sets: a speed, a direction, a flag for being on the ground. Everything here is a question asked of a place
/// and the inputs, so any answer can be had without playing up to it: which change comes next, which clips
/// have what say, and, given the clips, the pose.
/// <para>
/// A clip is named rather than held, and the names are whatever the caller finds the clips by:
/// project-relative paths, the way a scene's motion component lists them, or anything else, so long as it is
/// the same names handed back with the clips.
/// </para>
/// </remarks>
public sealed partial class BlendDocument
{
    /// <summary>The extension a blend file carries.</summary>
    public const string Extension = ".oblend";

    public const string Marker = "orblit.blend";

    /// <summary>The shape of the file. Bumped with a migration whenever it changes.</summary>
    public const int FormatVersion = 2;

    /// <summary>Every step from an older blend file to this one, oldest first.</summary>
    public static readonly IReadOnlyList<BlendMigration> Migrations = [new BlendV2()];

    private static readonly FileFormat Format = new(
        Marker,
        "blend",
        FormatVersion,
        message => new BlendFormatException(message),
        Migrations);

    private readonly Dictionary<string, int> _byName = [];

    // F2: six parameters, all but the first two optional and called by name.
    public BlendDocument(
        string name,
        IReadOnlyList<BlendState> states,
        IReadOnlyDictionary<string, double>? inputs = null,
        IReadOnlyList<BlendChange>? changes = null,
        string? start = null,
        IReadOnlyList<BlendFade>? fades = null)
    {
        changes ??= [];
        fades ??= [];
        Name = name;
        Inputs = MergedInputs(states, inputs);
        States = GraphExpansion.ExpandedStates(states).AsReadOnly();
        Changes = GraphExpansion.ExpandedChanges(states, changes);
        Entries = new ReadOnlyDictionary<string, string>(GraphExpansion.GraphEntries(states));
        Fades = states
            .Select(static state => state.Plays)
            .OfType<BlendGraph>()
            .SelectMany(static plays => plays.Graph.Fades)
            .Concat(fades)
            .ToList()
            .AsReadOnly();
        WrittenStates = states.ToList().AsReadOnly();
        WrittenChanges = changes.ToList().AsReadOnly();
        WrittenFades = fades.ToList().AsReadOnly();
        WrittenStart = start ?? (states.Count == 0 ? string.Empty : states[0].Name);
        Start = Entries.GetValueOrDefault(WrittenStart) ?? WrittenStart;
        for (var i = 0; i < States.Count; i++)
        {
            _byName[States[i].Name] = i;
        }

        Check(states);
    }

    public string Name { get; }

    /// <summary>
    /// The inputs it reads, and what each is until something sets it. An input read but not here is nought
    /// until set.
    /// </summary>
    public IReadOnlyDictionary<string, double> Inputs { get; }

    /// <summary>
    /// Expanded leaf states in authored order. <see cref="BlendPlace.ToNumbers"/> uses these indices,
    /// including nested graph leaves.
    /// </summary>
    public IReadOnlyList<BlendState> States { get; }

    /// <summary>In order of precedence: when two could be taken, the first is.</summary>
    public IReadOnlyList<BlendChange> Changes { get; }

    /// <summary>The state a character starts in.</summary>
    public string Start { get; }

    /// <summary>Parent graph names and the leaf each enters.</summary>
    public IReadOnlyDictionary<string, string> Entries { get; }

    public IReadOnlyList<BlendFade> Fades { get; }

    /// <summary>
    /// The states as a file writes them: a nested graph is one state here and its leaves are not listed. What
    /// an editor changes.
    /// </summary>
    public IReadOnlyList<BlendState> WrittenStates { get; }

    /// <summary>The changes as a file writes them, without the ones a nested graph brings.</summary>
    public IReadOnlyList<BlendChange> WrittenChanges { get; }

    /// <summary>The fades as a file writes them, without the ones a nested graph brings.</summary>
    public IReadOnlyList<BlendFade> WrittenFades { get; }

    /// <summary>The start as a file writes it: a state, or a nested graph that is entered at its own start.</summary>
    public string WrittenStart { get; }

    /// <summary>Every clip it names, once each, for whoever loads them.</summary>
    public IEnumerable<string> ClipNames => States.SelectMany(static state => state.Plays.Clips).Distinct();

    /// <summary>Every input it reads, declared or not.</summary>
    public IEnumerable<string> InputsRead => States
        .SelectMany(static state => state.Plays.Inputs)
        .Concat(Changes.SelectMany(static change => change.When.Inputs))
        .Distinct();

    /// <summary>
    /// A blend out of a file's text, with whatever could not be read.
    /// </summary>
    /// <remarks>
    /// Lenient about the parts, strict about the whole, like a clip: a state or a change that cannot be read
    /// is left out with a note, and a file that is not a blend, is one from a newer Orblit, or has no state
    /// that can be read throws <see cref="BlendFormatException"/>.
    /// </remarks>
    public static BlendLoad Decode(string text)
    {
        var problems = new List<string>();
        var json = Format.Open(text, problems);
        return BlendDecoder.Decode(json, problems);
    }

    public static BlendLoad FromJson(JsonMap raw) => Decode(JsonText.Encode(raw));

    /// <summary>
    /// This blend with whatever is given in place of what it has, as a file writes it. Throws as the
    /// constructor does, so a change left pointing at a state that is gone cannot get through.
    /// </summary>
    // F2: six parameters, all optional and called by name.
    public BlendDocument CopyWith(
        string? name = null,
        IReadOnlyDictionary<string, double>? inputs = null,
        IReadOnlyList<BlendState>? states = null,
        IReadOnlyList<BlendChange>? changes = null,
        string? start = null,
        IReadOnlyList<BlendFade>? fades = null) => new(
        name ?? Name,
        states ?? WrittenStates,
        inputs ?? Inputs,
        changes ?? WrittenChanges,
        start ?? WrittenStart,
        fades ?? WrittenFades);

    public BlendState? StateNamed(string name) => _byName.TryGetValue(ResolveState(name), out var index) ? States[index] : null;

    /// <summary>Where <paramref name="state"/> is in <see cref="States"/>, or -1.</summary>
    public int IndexOf(string state) => _byName.GetValueOrDefault(ResolveState(state), -1);

    public string ResolveState(string state) => Entries.GetValueOrDefault(state) ?? state;

    /// <summary>The shortest directed route, excluding <paramref name="from"/>, or null if unreachable.</summary>
    public IReadOnlyList<string>? RouteTo(string from, string to)
    {
        var origin = ResolveState(from);
        var goal = ResolveState(to);
        if (StateNamed(origin) is null || StateNamed(goal) is null)
        {
            return null;
        }

        var routes = new Dictionary<string, List<string>> { [origin] = [] };
        var queue = new List<string> { origin };
        for (var i = 0; i < queue.Count; i++)
        {
            var here = queue[i];
            if (here == goal)
            {
                return routes[here];
            }

            foreach (var change in Changes.Where(change => change.From is null || change.From == here))
            {
                if (routes.TryAdd(change.To, [.. routes[here], change.To]))
                {
                    queue.Add(change.To);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Pair-specific fade, or <paramref name="fallback"/> when no pair was authored. The last pair written for
    /// the dominant clips of the two states wins.
    /// </summary>
    // F2: five parameters, the last two optional and called by name.
    public double FadeBetween(
        BlendPlace place,
        string to,
        IReadOnlyDictionary<string, double> values,
        double fallback = 0,
        IReadOnlyDictionary<string, ClipDocument>? clips = null)
    {
        var read = Reader(values);
        var fromClip = Dominant(StateNamed(place.State)?.Plays.Weigh(read), clips);
        var toClip = Dominant(StateNamed(to)?.Plays.Weigh(read), clips);
        return Fades.LastOrDefault(fade => fade.From == fromClip && fade.To == toClip)?.Seconds ?? fallback;
    }

    /// <summary>
    /// The change that would be taken at <paramref name="place"/> with <paramref name="values"/> for inputs,
    /// or null when none would.
    /// </summary>
    public BlendChange? ChangeFor(BlendPlace place, IReadOnlyDictionary<string, double> values)
    {
        var read = Reader(values);
        return Changes.FirstOrDefault(change =>
            (change.From is null ? change.To != place.State : change.From == place.State)
            && change.When.Holds(read, place.Lap));
    }

    /// <summary>
    /// Every clip with a say at <paramref name="place"/> with <paramref name="values"/> for inputs, and its
    /// share, newest state first. Needs no clips: it is the blend's answer, whatever clips are handed to it.
    /// </summary>
    public IReadOnlyList<ClipWeight> WeightsAt(BlendPlace place, IReadOnlyDictionary<string, double> values)
    {
        var read = Reader(values);
        var weights = new List<ClipWeight>();
        foreach (var (layer, share) in place.Shares)
        {
            if (!(share > 0) || StateNamed(layer.State) is not { } state)
            {
                continue;
            }

            weights.AddRange(state.Plays.Weigh(read).Select(entry => new ClipWeight(layer.State, entry.Key, share * entry.Value, layer.Lap)));
        }

        return weights;
    }

    /// <summary>
    /// Seconds one time through <paramref name="state"/> takes at its own speed of one, with
    /// <paramref name="values"/> for inputs: the lengths of its clips, mixed as the clips are. Nought for a
    /// state with no clip that is there.
    /// </summary>
    public double LengthOf(string state, IReadOnlyDictionary<string, double> values, IReadOnlyDictionary<string, ClipDocument> clips) =>
        StateNamed(state) is { } found ? Length(found, Reader(values), clips) : 0;

    /// <summary>
    /// The pose at <paramref name="place"/> with <paramref name="values"/> for inputs: every clip with a say,
    /// sampled where its state is and mixed. Root motion is held in place, as a player holds it. A clip not
    /// in <paramref name="clips"/> has no say, and the others share what it would have had.
    /// </summary>
    public ClipFrame SampleAt(
        BlendPlace place,
        IReadOnlyDictionary<string, double> values,
        IReadOnlyDictionary<string, ClipDocument> clips,
        ClipFrame? rest = null)
    {
        var sampled = new BlendFrames(clips, Reader(values), rest);
        var remaining = 1.0;
        for (var layer = place; layer is not null && remaining > 0; layer = layer.From)
        {
            if (StateNamed(layer.State) is { } state)
            {
                sampled.AddState(state, layer.Lap, remaining * layer.Weight);
            }

            remaining *= 1 - layer.Weight;
            if (layer.Frozen is { } frozen)
            {
                sampled.Add(frozen.Sample(), remaining);
                break;
            }
        }

        return sampled.Mixed();
    }

    public OrderedDictionary<string, object?> ToJson()
    {
        var inputs = new OrderedDictionary<string, object?>();
        foreach (var (input, value) in Inputs)
        {
            inputs[input] = value;
        }

        var json = new OrderedDictionary<string, object?>
        {
            ["kind"] = Marker,
            ["formatVersion"] = FormatVersion,
            ["name"] = Name,
            ["inputs"] = inputs,
            ["start"] = WrittenStart,
            ["states"] = WrittenStates.Select(static state => (object?)state.ToJson()).ToList(),
            ["changes"] = WrittenChanges.Select(static change => (object?)change.ToJson()).ToList(),
        };
        if (WrittenFades.Count > 0)
        {
            json["fades"] = WrittenFades.Select(static fade => (object?)fade.ToJson()).ToList();
        }

        return json;
    }

    /// <summary>
    /// The file's text: indented, with anything short enough on a line of its own, so a state, a point and a
    /// change each read as one line.
    /// </summary>
    public string Encode() => BlendText.Of(ToJson());

    private static OrderedDictionary<string, double> MergedInputs(IReadOnlyList<BlendState> states, IReadOnlyDictionary<string, double>? inputs)
    {
        var merged = GraphExpansion.GraphInputs(states);
        foreach (var (input, value) in inputs ?? new Dictionary<string, double>())
        {
            merged[input] = value;
        }

        return merged;
    }

    private static string? Dominant(OrderedDictionary<string, double>? weights, IReadOnlyDictionary<string, ClipDocument>? clips)
    {
        string? found = null;
        var most = 0.0;
        foreach (var (clip, weight) in weights ?? [])
        {
            if ((clips is null || clips.ContainsKey(clip)) && weight > most)
            {
                found = clip;
                most = weight;
            }
        }

        return found;
    }

    // An input not given, or not a number, is as the blend declares it, or nought.
    private Func<string, double> Reader(IReadOnlyDictionary<string, double> values) =>
        input => values.TryGetValue(input, out var value) && double.IsFinite(value) ? value : Inputs.GetValueOrDefault(input, 0);

    private static double Length(BlendState state, Func<string, double> read, IReadOnlyDictionary<string, ClipDocument> clips)
    {
        var total = 0.0;
        var sum = 0.0;
        foreach (var (name, weight) in state.Plays.Weigh(read))
        {
            if (clips.TryGetValue(name, out var clip))
            {
                sum += clip.Duration * weight;
                total += weight;
            }
        }

        return total > 0 ? sum / total : 0;
    }

    [SuppressMessage("Usage", "CA2208", Justification = "These are the constructor's checks, split out to keep it short.")]
    private void Check(IReadOnlyList<BlendState> written)
    {
        if (written.Count == 0)
        {
            throw new ArgumentException("A blend needs a state.", "states");
        }

        if (_byName.Count != States.Count
            || written.Select(static state => state.Name).Distinct().Count() != written.Count
            || Entries.Keys.Any(_byName.ContainsKey))
        {
            throw new ArgumentException("Two states have one name.", "states");
        }

        if (StateNamed(Start) is null)
        {
            throw new ArgumentException("Not one of the states.", "start");
        }

        var stray = Changes.FirstOrDefault(change => StateNamed(change.To) is null || (change.From is not null && StateNamed(change.From) is null));
        if (stray is not null)
        {
            throw new ArgumentException($"A change is between two of the states: {stray.From ?? "anywhere"} to {stray.To}.", "changes");
        }
    }

    private sealed class BlendV2 : BlendMigration
    {
        public override int From => 1;

        public override JsonMap Apply(JsonMap json, List<string> notes)
        {
            var upgraded = new OrderedDictionary<string, object?>();
            foreach (var (key, value) in json)
            {
                upgraded[key] = value;
            }

            upgraded["formatVersion"] = 2L;
            return upgraded;
        }
    }
}

using Orblit.Scene;
using Orblit.Sequence;

namespace Orblit.Motion;

/// <summary>
/// Where a blend is: which state, how far into it, and what it is fading in over.
/// </summary>
/// <remarks>
/// All of a blend's playing is here and nowhere else. The blend itself is an asset that never changes, and the
/// player holding one of these holds nothing a place does not say, so a place is enough to carry on from:
/// written into a save file, a character comes back mid-stride; sent as numbers, a character on another
/// machine stands the same way; built by hand, a test can say where a character is and ask what happens next,
/// without playing a frame to get there.
/// <para>
/// A fade is a place with another place under it. <see cref="From"/> is what <see cref="State"/> is fading in
/// over, which may itself still be fading in over something else, when a change comes before the last one has
/// finished. No more than <see cref="Deepest"/> are kept: past that the oldest is dropped, which it barely
/// shows by then.
/// </para>
/// </remarks>
public sealed partial class BlendPlace : IEquatable<BlendPlace>
{
    /// <summary>The most places one place holds, itself included.</summary>
    public const int Deepest = 4;

    private const int Each = 5;

    /// <summary>
    /// Numbers in the fixed prefix. A place without routes or frozen poses fits a <c>float64</c> column of
    /// this arity. Extended places append metadata.
    /// </summary>
    public const int Width = 1 + (Each * Deepest);

    // F2: eight parameters, all but the first optional and called by name.
    public BlendPlace(
        string state,
        double lap = 0,
        BlendPlace? from = null,
        double faded = 0,
        double fade = 0,
        Easing shape = Easing.Smooth,
        PoseSnapshot? frozen = null,
        BlendRoute? journey = null)
    {
        State = state;
        Lap = lap;
        From = from;
        Faded = faded;
        Fade = fade;
        Shape = shape;
        Frozen = frozen;
        Journey = journey;
    }

    /// <summary>The state being played.</summary>
    public string State { get; }

    /// <summary>
    /// How far into it, in times through: nought on the way in, one at the end of the first time through, and
    /// two and a half halfway through the third lap of a loop. Times rather than seconds, because a state
    /// mixing a walk and a run is as long as the mix says, and that changes with the speed.
    /// </summary>
    public double Lap { get; }

    /// <summary>What <see cref="State"/> is fading in over, or null when it has the whole say.</summary>
    public BlendPlace? From { get; }

    /// <summary>Seconds of the fade gone.</summary>
    public double Faded { get; }

    /// <summary>Seconds the fade takes in all.</summary>
    public double Fade { get; }

    /// <summary>How the fade eases.</summary>
    public Easing Shape { get; }

    /// <summary>The outgoing pose held still during this fade, when requested.</summary>
    public PoseSnapshot? Frozen { get; }

    public BlendRoute? Journey { get; }

    /// <summary>Remaining leaf states on a requested route.</summary>
    public IReadOnlyList<string> Route => Journey?.States ?? [];

    /// <summary>
    /// How much say <see cref="State"/> has over <see cref="From"/>: nought as the fade starts and one at its
    /// end, and one when there is nothing to fade from.
    /// </summary>
    public double Weight => From is null || !(Fade > 0) ? 1 : Doubles.Clamp(Easings.Ease(Shape, Faded / Fade), 0, 1);

    /// <summary>Every state being played and how much say it has, newest first. The says add to one.</summary>
    public IReadOnlyList<(BlendPlace Place, double Share)> Shares
    {
        get
        {
            var shares = new List<(BlendPlace, double)>();
            var left = 1.0;
            for (var at = this; at is not null && left > 0; at = at.From)
            {
                var say = at.Weight;
                shares.Add((at, left * say));
                left *= 1 - say;
            }

            return shares;
        }
    }

    /// <summary>How many places this one holds, itself included.</summary>
    public int Depth => 1 + (From?.Depth ?? 0);

    public BlendPlace WithRoute(IReadOnlyList<string> route) =>
        new(State, Lap, From, Faded, Fade, Shape, Frozen, route.Count == 0 ? null : new BlendRoute(route));

    /// <summary>
    /// Where playing goes on from after a change into <paramref name="state"/>.
    /// </summary>
    /// <remarks>
    /// A <paramref name="fade"/> of nothing is a cut, and leaves nothing behind. <paramref name="inStep"/>
    /// starts the new state as far through its lap as this one is through its own, so a walk turning into a
    /// run keeps its feet.
    /// </remarks>
    // F2: six parameters, all but the first optional and called by name.
    public BlendPlace Enter(
        string state,
        double fade = 0,
        Easing shape = Easing.Smooth,
        bool inStep = false,
        PoseSnapshot? frozen = null,
        IReadOnlyList<string>? route = null)
    {
        var into = inStep ? Lap - Math.Floor(Lap) : 0.0;
        var planned = route is { Count: > 0 } ? new BlendRoute(route) : null;
        if (!(fade > 0) || !double.IsFinite(fade))
        {
            return new BlendPlace(state, lap: into, journey: planned);
        }

        return new BlendPlace(state, lap: into, from: Keeping(Deepest - 1), fade: fade, shape: shape, frozen: frozen, journey: planned);
    }

    public OrderedDictionary<string, object?> ToJson()
    {
        var written = new OrderedDictionary<string, object?> { ["state"] = State, ["lap"] = Lap };
        if (Route.Count > 0)
        {
            written["route"] = Route;
        }

        if (From is { } from)
        {
            written["fade"] = Fade;
            written["faded"] = Faded;
            written["shape"] = Shape == Easing.Smooth ? null : FileNames.Of(Shape);
            written["from"] = from.ToJson();
            if (Frozen is { } frozen)
            {
                written["frozen"] = frozen.ToJson();
            }
        }

        return Values.Pruned(written);
    }

    public bool Equals(BlendPlace? other) =>
        other is not null
        && other.State == State
        && other.Lap == Lap
        && other.Faded == Faded
        && other.Fade == Fade
        && other.Shape == Shape
        && Equals(other.Frozen, Frozen)
        && other.Route.SequenceEqual(Route)
        && Equals(other.From, From);

    public override bool Equals(object? obj) => Equals(obj as BlendPlace);

    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(State);
        hash.Add(Lap);
        hash.Add(Faded);
        hash.Add(Fade);
        hash.Add(Shape);
        hash.Add(From);
        hash.Add(Frozen);
        foreach (var next in Route)
        {
            hash.Add(next);
        }

        return hash.ToHashCode();
    }

    // This place with no more than the given number in it.
    private BlendPlace Keeping(int places)
    {
        if (From is not { } from)
        {
            return this;
        }

        if (places <= 1)
        {
            return new BlendPlace(State, lap: Lap);
        }

        var kept = from.Keeping(places - 1);
        return ReferenceEquals(kept, from)
            ? this
            : new BlendPlace(State, Lap, kept, Faded, Fade, Shape, Frozen, Journey);
    }
}

using Orblit.Sequence;

namespace Orblit.Motion;

/// <summary>A blend being played on one character.</summary>
/// <remarks>
/// Holds the three things a blend needs and nothing more: the clips it names, the inputs gameplay sets, and
/// the <see cref="Place"/>, which is public and can be set, because a place is the whole of where playing has
/// got to. To save a character, keep its place; to restore one, set it.
/// </remarks>
public sealed class BlendPlayer
{
    // F2: five parameters, all but the first two optional and called by name.
    public BlendPlayer(
        BlendDocument blend,
        IReadOnlyDictionary<string, ClipDocument> clips,
        BlendPlace? place = null,
        IReadOnlyDictionary<string, double>? inputs = null,
        ClipFrame? rest = null)
    {
        Blend = blend;
        Clips = clips;
        Rest = rest;
        Place = place ?? new BlendPlace(blend.Start);
        Inputs = new Dictionary<string, double>(blend.Inputs);
        foreach (var (input, value) in inputs ?? new Dictionary<string, double>())
        {
            Inputs[input] = value;
        }
    }

    public BlendDocument Blend { get; }

    /// <summary>The clips, by the names <see cref="Blend"/> gives them.</summary>
    public IReadOnlyDictionary<string, ClipDocument> Clips { get; }

    /// <summary>Fills unauthored channels and returns removed channels to rest in fades.</summary>
    public ClipFrame? Rest { get; }

    /// <summary>Where playing has got to.</summary>
    public BlendPlace Place { get; set; }

    /// <summary>The inputs, set by gameplay. Starts as the blend says each starts.</summary>
    public Dictionary<string, double> Inputs { get; }

    /// <summary>The state being played.</summary>
    public string State => Place.State;

    /// <summary>Plays on by <paramref name="seconds"/> and hands back what happened on the way.</summary>
    public BlendStep Advance(double seconds)
    {
        var step = Blend.Advance(Place, Inputs, seconds, Clips, Rest);
        Place = step.Place;
        return step;
    }

    /// <summary>The pose where the blend now is, without moving.</summary>
    public ClipFrame Sample() => Blend.SampleAt(Place, Inputs, Clips, Rest);

    /// <summary>
    /// Goes into <paramref name="state"/> whatever the changes say: a cut, or a fade over
    /// <paramref name="fade"/> seconds. For gameplay that knows better than any condition, a hit landing or a
    /// cutscene taking over.
    /// </summary>
    // F2: five parameters, all but the first optional and called by name.
    public void Enter(
        string state,
        double fade = 0,
        Easing shape = Easing.Smooth,
        bool inStep = false,
        bool fromPose = false)
    {
        if (Blend.StateNamed(state) is null)
        {
            throw new ArgumentException("Not a state of the blend.", nameof(state));
        }

        Place = Place.Enter(
            Blend.ResolveState(state),
            fade: Blend.FadeBetween(Place, state, Inputs, fallback: fade, clips: Clips),
            shape: shape,
            inStep: inStep,
            frozen: fromPose ? new PoseSnapshot(Sample()) : null);
    }

    /// <summary>Follows the shortest directed route, taking one edge per step.</summary>
    /// <remarks>
    /// A requested route overrides conditions and waits for each fade to finish. Throws when the state is
    /// unknown or unreachable, leaving the place intact.
    /// </remarks>
    public void Travel(string state)
    {
        var route = Blend.RouteTo(Place.State, state)
            ?? throw new ArgumentException("Unreachable state.", nameof(state));
        Place = Place.WithRoute(route);
    }
}

using Orblit.Sequence;

namespace Orblit.Motion;

public sealed partial class BlendDocument
{
    /// <summary>
    /// Plays on from <paramref name="place"/> by <paramref name="seconds"/>, with <paramref name="values"/>
    /// for inputs.
    /// </summary>
    /// <remarks>
    /// Every state being played moves on, the ones fading out as well, and every fade with them; a fade that
    /// finishes takes whatever it was fading from away. Then, if a change can be taken from where the state
    /// being played now is, the first that can be is, and the step ends at the start of it. One change a
    /// step, so a blend whose changes chase each other round cannot hang a frame.
    /// <para>
    /// Marks come from the state being played, and from the clip in it with the most say, so a walk mixed
    /// with a run takes one footstep, not two. Root motion comes from every clip with a say, mixed by it. A
    /// step of nothing plays nothing but still takes a change, which is how a new input is acted on at once.
    /// </para>
    /// </remarks>
    // F2: five parameters, the last optional and called by name.
    public BlendStep Advance(
        BlendPlace place,
        IReadOnlyDictionary<string, double> values,
        double seconds,
        IReadOnlyDictionary<string, ClipDocument> clips,
        ClipFrame? rest = null)
    {
        var read = Reader(values);
        var (moved, faded) = Move(place, read, double.IsFinite(seconds) && seconds > 0 ? seconds : 0.0, clips);
        var now = Rebuilt(moved, faded);

        var walk = new Walk(read, clips);
        var shares = now.Shares;
        for (var i = 0; i < shares.Count; i++)
        {
            if (moved[i].State is { } state)
            {
                walk.Layer(moved[i], state, shares[i].Share, newest: i == 0);
            }
        }

        var change = now.Route.Count == 0 ? ChangeFor(now, values) : RouteChange(now);
        var next = change is null
            ? now
            : Taken(
                now,
                change,
                FadeBetween(now, change.To, values, change.Fade, clips),
                change.FromPose ? new PoseSnapshot(SampleAt(now, values, clips, rest)) : null);
        return new BlendStep(next, SampleAt(next, values, clips, rest), walk.Marks, walk.Moved, change);
    }

    // The place each state being played reaches in one step, newest first, with how far each fade has got.
    private (List<Stride> Moved, List<double> Faded) Move(BlendPlace place, Func<string, double> read, double step, IReadOnlyDictionary<string, ClipDocument> clips)
    {
        var moved = new List<Stride>();
        var faded = new List<double>();
        for (var at = place; at is not null; at = at.From)
        {
            var state = StateNamed(at.State);
            var lap = state is not null && step > 0 && state.Speed > 0 ? LapAfter(state, at.Lap, Length(state, read, clips), step) : at.Lap;
            moved.Add(new Stride(at, state, lap));
            var fading = at.From is not null && at.Fade > 0;
            faded.Add(fading ? Math.Min(at.Faded + step, at.Fade) : 0);

            // A fade that has finished has the whole say, and whatever it was fading in over is finished with.
            if (!fading || faded[^1] >= at.Fade)
            {
                break;
            }
        }

        return (moved, faded);
    }

    // A state with no length is through as soon as it starts.
    private static double LapAfter(BlendState state, double lap, double length, double step) =>
        length > 0 ? lap + (step * state.Speed / length) : Math.Max(lap, 1.0);

    private static BlendPlace Rebuilt(List<Stride> moved, List<double> faded)
    {
        BlendPlace? built = null;
        for (var i = moved.Count - 1; i >= 0; i--)
        {
            var (was, _, lap) = moved[i];
            built = built is null
                ? new BlendPlace(was.State, lap: lap, journey: was.Journey)
                : new BlendPlace(was.State, lap, from: built, faded: faded[i], fade: was.Fade, shape: was.Shape, frozen: was.Frozen, journey: was.Journey);
        }

        // The walk always records the place it started from.
        return built!;
    }

    private BlendChange? RouteChange(BlendPlace place) => place.From is not null
        ? null
        : Changes.FirstOrDefault(change => (change.From is null || change.From == place.State) && change.To == place.Route[0]);

    private static BlendPlace Taken(BlendPlace now, BlendChange change, double fade, PoseSnapshot? frozen) =>
        now.Enter(change.To, fade, change.Shape, change.InStep, frozen, now.Route.Count == 0 ? [] : now.Route.Skip(1).ToList());

    /// <summary>The steps of root motion mixed by the say of the clips that took them.</summary>
    private static RootStep Mixed(List<RootStep> steps, List<double> says)
    {
        if (steps.Count == 0)
        {
            return new RootStep();
        }

        if (steps.Count == 1)
        {
            return steps[0];
        }

        return new RootStep(
            Mixers.OfVector3.Mix(steps.Select(static one => one.Position).ToList(), says),
            Mixers.OfQuaternion.Mix(steps.Select(static one => one.Rotation).ToList(), says));
    }

    private readonly record struct Stride(BlendPlace Was, BlendState? State, double Lap);

    // What the clips of each state being played pass in a step: their root motion, and the marks of the newest.
    private sealed class Walk(Func<string, double> read, IReadOnlyDictionary<string, ClipDocument> clips)
    {
        private readonly List<RootStep> _steps = [];
        private readonly List<double> _says = [];

        public List<Mark> Marks { get; } = [];

        public RootStep Moved => Mixed(_steps, _says);

        public void Layer(Stride stride, BlendState state, double share, bool newest)
        {
            var (was, _, lap) = stride;
            ClipDocument? loudest = null;
            var most = 0.0;
            foreach (var (name, weight) in state.Plays.Weigh(read))
            {
                if (!clips.TryGetValue(name, out var clip))
                {
                    continue;
                }

                if (share * weight > 0)
                {
                    _steps.Add(ClipPass.Over(clip, state.WhenDone ?? clip.WhenDone, state.LapFor(clip, was.Lap), state.LapFor(clip, lap), marks: false).Moved);
                    _says.Add(share * weight);
                }

                if (weight > most)
                {
                    most = weight;
                    loudest = clip;
                }
            }

            if (newest && loudest is not null)
            {
                MarksOf(state, loudest, was.Lap, lap);
            }
        }

        // A state that has only just left its start also passes the mark that sits at the moment it began.
        private void MarksOf(BlendState state, ClipDocument loudest, double from, double to)
        {
            var whenDone = state.WhenDone ?? loudest.WhenDone;
            var start = state.LapFor(loudest, from);
            if (from == 0 && to > 0 && start > 0)
            {
                var time = ClipPass.TimeAt(loudest, whenDone, start);
                Marks.AddRange(loudest.MarksBetween(time - 1e-9, time));
            }

            Marks.AddRange(ClipPass.Over(loudest, whenDone, start, state.LapFor(loudest, to)).Marks);
        }
    }
}

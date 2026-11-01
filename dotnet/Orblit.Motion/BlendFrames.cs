using Orblit.Sequence;

namespace Orblit.Motion;

/// <summary>
/// The frames of every clip with a say at a place, collected to be mixed by it.
/// </summary>
/// <remarks>
/// The mix sits at the time of the clip with the most say, so a walk blended with a run is stamped with the
/// walk's time while the walk is the louder.
/// </remarks>
internal sealed class BlendFrames(IReadOnlyDictionary<string, ClipDocument> clips, Func<string, double> read, ClipFrame? rest)
{
    private readonly List<ClipFrame> _frames = [];
    private readonly List<double> _weights = [];
    private double _at;
    private double _loudest;

    /// <summary>Every clip <paramref name="state"/> plays that is in the clips, sampled at <paramref name="lap"/> and given <paramref name="share"/> of the say.</summary>
    internal void AddState(BlendState state, double lap, double share)
    {
        if (share <= 0)
        {
            return;
        }

        foreach (var (name, weight) in state.Plays.Weigh(read))
        {
            if (!clips.TryGetValue(name, out var clip))
            {
                continue;
            }

            var time = ClipPass.TimeAt(clip, state.WhenDone ?? clip.WhenDone, state.LapFor(clip, lap));
            Add(clip.SampleAt(time, inPlace: true), share * weight);
        }
    }

    internal void Add(ClipFrame frame, double weight)
    {
        _frames.Add(rest is null ? frame : FrameLayers.Complete(frame, rest));
        _weights.Add(weight);
        if (weight > _loudest)
        {
            _loudest = weight;
            _at = frame.At;
        }
    }

    internal ClipFrame Mixed()
    {
        var mixed = ClipFrame.Mix(_frames, _weights, _at);
        return rest is null ? mixed : FrameLayers.Complete(mixed, rest);
    }
}

using Orblit.VectorMath;

namespace Orblit.Sequence;

/// <summary>
/// A cutscene, as a thing rather than as a playing of one. A sequence holds no
/// playhead and no bindings: it is the score, not the performance. It is not
/// called <c>Sequence</c>, which a type in the namespace <c>Orblit.Sequence</c> cannot be.
/// </summary>
public sealed class CutSequence(
    IReadOnlyList<Track> tracks,
    string name = "sequence",
    double rate = 30,
    double? duration = null)
{
    private readonly double? _duration = duration;

    public string Name { get; } = name;

    public IReadOnlyList<Track> Tracks { get; } = tracks;

    /// <summary>
    /// Frames a second, for an editor's ruler and for snapping. Sampling never
    /// uses it: a sequence is continuous.
    /// </summary>
    public double Rate { get; } = rate;

    /// <summary>
    /// How long it runs. Long enough for every track by default, and settable
    /// where a sequence is meant to hold on its last frame for a while.
    /// </summary>
    public double Duration
    {
        get
        {
            if (_duration is { } given)
            {
                return given;
            }

            var end = 0.0;
            foreach (var track in Tracks)
            {
                if (track.End > end)
                {
                    end = track.End;
                }
            }

            return end;
        }
    }

    /// <summary>
    /// Everything the sequence has to say at <paramref name="at"/>. A pure
    /// function of the moment: nothing is remembered between calls.
    /// </summary>
    public SequenceFrame SampleAt(double at)
    {
        var frame = new SequenceFrame(at);
        foreach (var track in Tracks)
        {
            track.Contribute(at, frame);
        }

        return frame;
    }

    /// <summary>
    /// The marks crossed going from <paramref name="from"/> to <paramref name="to"/>,
    /// in the order they happened. Empty when <paramref name="to"/> is not after
    /// <paramref name="from"/>, so running a scene backwards does not open the
    /// door again.
    /// </summary>
    public List<Mark> MarksBetween(double from, double to)
    {
        var crossed = new List<Mark>();
        foreach (var track in Tracks)
        {
            crossed.AddRange(track.MarksIn(from, to));
        }

        // A stable sort, so marks with equal times keep their order.
        return [.. crossed.OrderBy(mark => mark.At, Comparer<double>.Create(Doubles.Compare))];
    }

    /// <summary>The nearest frame boundary to <paramref name="at"/>, for an editor that snaps.</summary>
    public double Snap(double at) => Doubles.Round(at * Rate) / Rate;
}

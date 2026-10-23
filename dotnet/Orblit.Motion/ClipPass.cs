using System.Diagnostics;
using Orblit.Sequence;

namespace Orblit.Motion;

/// <summary>What a stretch of playing did: where it carried the character, and the marks it passed.</summary>
public readonly record struct Passed(RootStep Moved, IReadOnlyList<Mark> Marks);

/// <summary>
/// A clip played a number of times through, without a <see cref="ClipPlayer"/> to hold the playhead.
/// </summary>
public static class ClipPass
{
    // Past this many passes in one step the step is not a frame, it is a stall.
    private const int MostPasses = 256;

    /// <summary>
    /// Seconds into <paramref name="clip"/> at <paramref name="lap"/> times through, ending the
    /// <paramref name="whenDone"/> way.
    /// </summary>
    /// <remarks>
    /// A loop at a whole lap is at its end rather than its start, and so is a bounce at an odd one, so a step
    /// that lands exactly on the last frame shows it, as a player's does. Letting go is holding here: a blend
    /// always has a say, and handing a character back is a change to another state.
    /// </remarks>
    public static double TimeAt(ClipDocument clip, WhenDone whenDone, double lap)
    {
        var length = clip.Duration;
        if (!(length > 0) || !(lap > 0))
        {
            return 0;
        }

        switch (whenDone)
        {
            case WhenDone.Hold:
            case WhenDone.Release:
                return Math.Min(lap, 1.0) * length;
            case WhenDone.Loop:
                var into = lap - Math.Floor(lap);
                return (into == 0 ? 1.0 : into) * length;
            case WhenDone.Bounce:
                return BouncedTime(lap, length);
            default:
                throw new UnreachableException();
        }
    }

    /// <summary>
    /// Where <paramref name="clip"/> carries the character, and the marks it passes, playing from
    /// <paramref name="from"/> times through to <paramref name="to"/> the <paramref name="whenDone"/> way.
    /// </summary>
    /// <remarks>
    /// The same as a player playing the stretch: marks fire on the way forwards, the first frame of each pass
    /// included, and root motion is measured over every stretch actually played. <paramref name="marks"/>
    /// false skips looking for them.
    /// </remarks>
    // F2: five parameters, the last called by name where it is wanted.
    public static Passed Over(ClipDocument clip, WhenDone whenDone, double from, double to, bool marks = true)
    {
        var trip = new Trip(clip, marks);
        if (!(to > from) || !(clip.Duration > 0))
        {
            return trip.Result;
        }

        if (whenDone is WhenDone.Hold or WhenDone.Release)
        {
            var start = Doubles.Clamp(from, 0.0, 1.0);
            var end = Doubles.Clamp(to, 0.0, 1.0);
            if (end > start)
            {
                trip.Forwards(start * clip.Duration, end * clip.Duration);
            }

            return trip.Result;
        }

        trip.At = Math.Max(from, 0.0);
        trip.Pass = Doubles.Floor(trip.At);
        while (trip.At < to)
        {
            if (to - trip.At > MostPasses && trip.At == trip.Pass)
            {
                trip.SkipWhole(whenDone == WhenDone.Loop, Doubles.Floor(to - trip.At) - 1);
            }
            else
            {
                trip.PlayPass(whenDone == WhenDone.Loop, to);
            }
        }

        return trip.Result;
    }

    private static double BouncedTime(double lap, double length)
    {
        var pass = Doubles.Floor(lap);
        var into = lap - pass;
        if (into == 0)
        {
            return pass % 2 == 1 ? length : 0;
        }

        return (pass % 2 == 0 ? into : 1 - into) * length;
    }

    private sealed class Trip(ClipDocument clip, bool wantMarks)
    {
        private readonly List<Mark> _passed = [];
        private RootStep _moved = new();

        /// <summary>How far through, in laps.</summary>
        public double At;

        /// <summary>The whole lap <see cref="At"/> is in.</summary>
        public long Pass;

        public Passed Result => new(_moved, _passed);

        public void Forwards(double start, double end)
        {
            if (wantMarks)
            {
                // A pass that starts at the very start takes the marks there too: nothing else would ever
                // fire them.
                _passed.AddRange(clip.MarksBetween(start <= 0 ? -1e-9 : start, end));
            }

            _moved = _moved.Then(clip.RootStepBetween(start, end));
        }

        /// <summary>The way back of a bounce, which fires nothing.</summary>
        public void Backwards(double start, double stop) => _moved = _moved.Then(clip.RootStepBetween(start, stop));

        /// <summary>
        /// Whole passes are skipped, with where they would have taken the character but without their marks.
        /// </summary>
        public void SkipWhole(bool loops, long whole)
        {
            var length = clip.Duration;
            if (loops)
            {
                _moved = _moved.Then(RootStep.Repeated(clip.RootStepBetween(0, length), whole));
            }
            else if (whole % 2 == 1)
            {
                // A bounce there and back goes nowhere, so only the odd one out counts, and it goes the way
                // the first of them did.
                _moved = _moved.Then(Pass % 2 == 0 ? clip.RootStepBetween(0, length) : clip.RootStepBetween(length, 0));
            }

            At += whole;
            Pass += whole;
        }

        public void PlayPass(bool loops, double to)
        {
            var length = clip.Duration;
            var end = Math.Min(to, Pass + 1.0);
            var start = (At - Pass) * length;
            var stop = (end - Pass) * length;
            if (loops || Pass % 2 == 0)
            {
                Forwards(start, stop);
            }
            else
            {
                Backwards(length - start, length - stop);
            }

            At = end;
            Pass++;
        }
    }
}

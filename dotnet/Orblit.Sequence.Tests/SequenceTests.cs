using Orblit.VectorMath;

namespace Orblit.Sequence.Tests;

public class SequenceTests
{
    private static Clip ClipOf(double start, double duration, double easeIn = 0, double easeOut = 0) =>
        new(start, duration, easeIn: easeIn, easeOut: easeOut);

    private static Channel<double> Constant(double value) =>
        new([new Key<double>(0, value)], Mixers.OfDouble);

    private static PropertyTrack<double> NumbersOn(string binding, string property, IReadOnlyList<Keyed<double>> clips) =>
        new(binding: binding, property: property, clips: clips, mixer: Mixers.OfDouble);

    private static double Height(SequenceFrame frame) => (double)frame.Get("crate", "height")!;

    // channels

    [Fact]
    public void DoNotExtrapolatePastTheirEnds()
    {
        var channel = new Channel<double>(
            [new Key<double>(1, 10.0), new Key<double>(2, 20.0)],
            Mixers.OfDouble);
        Assert.Equal(10, channel.At(0)); // held before the first key
        Assert.Equal(20, channel.At(3)); // held after the last
        Assert.Equal(15, channel.At(1.5), 1e-9); // the smooth default is symmetric at the middle
    }

    [Fact]
    public void AStepKeyHoldsUntilTheNextOne()
    {
        var channel = new Channel<double>(
            [new Key<double>(0, 1.0, Hold.Step), new Key<double>(1, 9.0)],
            Mixers.OfDouble);
        Assert.Equal(1, channel.At(0.99));
        Assert.Equal(9, channel.At(1));
    }

    [Fact]
    public void TwoKeysAtTheSameMomentAreACut()
    {
        var channel = new Channel<double>(
            [new Key<double>(0, 0.0), new Key<double>(1, 1.0), new Key<double>(1, 5.0)],
            Mixers.OfDouble);
        Assert.Equal(5, channel.At(1));
    }

    [Fact]
    public void RotationsGoTheShortWayRound()
    {
        var from = Quaternion.AxisAngle(new Vector3(0, 1, 0), 0.1);
        var to = Quaternion.AxisAngle(new Vector3(0, 1, 0), -0.1);
        // Negated, which is the same rotation written the other way. Without a
        // sign check the blend takes the long way and swings all the way round.
        var flipped = new Quaternion(-to.X, -to.Y, -to.Z, -to.W);
        var middle = Mixers.OfQuaternion.Lerp(from, flipped, 0.5);
        Assert.True(Math.Abs(middle.Y) < 1e-6, "halfway between +0.1 and -0.1 about Y is no turn at all");
        Assert.Equal(1, Math.Abs(middle.W), 1e-6);
    }

    // a rotation turns at an even rate: partway round a tipped axis, and a wide
    // turn, where blending the four numbers would lag at a quarter and run
    // ahead at three quarters.

    private const double Wide = 2 * Math.PI / 3;

    private static readonly Vector3 Axis = new Vector3(1, 2, 3).Normalized();

    private static Quaternion TurnedBy(double angle) => Quaternion.AxisAngle(Axis, 0.2 + angle);

    /// <summary>The angle between two rotations, whichever way round each is written.</summary>
    private static double Apart(Quaternion a, Quaternion b)
    {
        var dot = (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z) + (a.W * b.W);
        return 2 * Math.Acos(Doubles.Clamp(Math.Abs(dot), 0.0, 1.0));
    }

    [Fact]
    public void BetweenTwoKeys()
    {
        var channel = new Channel<Quaternion>(
            [new Key<Quaternion>(0, TurnedBy(0), Hold.Linear), new Key<Quaternion>(1, TurnedBy(Wide))],
            Mixers.OfQuaternion);
        foreach (var part in new[] { 0.25, 0.5, 0.75 })
        {
            Assert.True(Apart(channel.At(part), TurnedBy(part * Wide)) < 1e-6, $"at {part} of the way");
        }
    }

    [Fact]
    public void PastEitherKeyForAShapeThatOvershoots()
    {
        foreach (var part in new[] { -0.1, 1.1 })
        {
            var turn = Mixers.OfQuaternion.Lerp(TurnedBy(0), TurnedBy(Wide), part);
            Assert.True(Apart(turn, TurnedBy(part * Wide)) < 1e-6, $"at {part} of the way");
        }
    }

    [Fact]
    public void BetweenTwoKeysThatBarelyDiffer()
    {
        var turn = Mixers.OfQuaternion.Lerp(TurnedBy(0), TurnedBy(1e-9), 0.5);
        Assert.Equal(1, turn.Length, 1e-12);
        Assert.True(Apart(turn, TurnedBy(0.5e-9)) < 1e-6);
    }

    [Fact]
    public void ABinarySearchFindsTheRightSpanInALongChannel()
    {
        var keys = new List<Key<double>>();
        for (var i = 0; i < 200; i++)
        {
            keys.Add(new Key<double>(i, i * 2.0, Hold.Linear));
        }

        var channel = new Channel<double>(keys, Mixers.OfDouble);
        Assert.Equal(301, channel.At(150.5), 1e-9);
    }

    // clips

    [Fact]
    public void WeightFadesInAndOutAndHoldsBetween()
    {
        var clip = ClipOf(0, 10, easeIn: 2, easeOut: 2);
        Assert.Equal(0, clip.WeightAt(-1));
        Assert.Equal(0, clip.WeightAt(0));
        Assert.Equal(Easings.Ease(Easing.InOut, 0.5), clip.WeightAt(1), 1e-9);
        Assert.Equal(1, clip.WeightAt(5));
        Assert.Equal(Easings.Ease(Easing.InOut, 0.5), clip.WeightAt(9), 1e-9);
        Assert.Equal(0, clip.WeightAt(10)); // the end is exclusive
    }

    [Fact]
    public void TrimmingAndSpeedChangeWhereTheContentIsRead()
    {
        var clip = new Clip(start: 10, duration: 4, clipIn: 2, speed: 2);
        Assert.Equal(2, clip.LocalAt(10)); // starts two seconds in
        Assert.Equal(4, clip.LocalAt(11)); // and runs at double
    }

    [Fact]
    public void AClipInTheMiddleOfAFadeCountsForLessThanOne()
    {
        var clip = ClipOf(0, 4, easeOut: 4);
        Assert.Equal(0.5, clip.WeightAt(2), 1e-9);
    }

    // property tracks

    [Fact]
    public void SayNothingWhereNoClipCovers()
    {
        var track = NumbersOn("crate", "height", [new Keyed<double>(ClipOf(2, 2), Constant(5.0))]);
        Assert.False(track.TryValueAt(0, out _), "before its clip");
        Assert.True(track.TryValueAt(3, out var inside));
        Assert.Equal(5, inside);
        Assert.False(track.TryValueAt(5, out _), "after it");
    }

    [Fact]
    public void TwoOverlappingClipsBlendByWeight()
    {
        var track = NumbersOn(
            "crate",
            "height",
            [
                new Keyed<double>(ClipOf(0, 4, easeOut: 2), Constant(0.0)),
                new Keyed<double>(ClipOf(2, 4, easeIn: 2), Constant(10.0)),
            ]);
        // At the exact middle of the overlap both are at half weight, so the
        // answer is halfway between what each of them wanted.
        Assert.True(track.TryValueAt(3, out var middle));
        Assert.Equal(5, middle, 1e-9);
        Assert.True(track.TryValueAt(1, out var first));
        Assert.Equal(0, first); // only the first has anything to say
        Assert.True(track.TryValueAt(5, out var last));
        Assert.Equal(10, last);
    }

    [Fact]
    public void AMutedTrackContributesNothing()
    {
        var track = new PropertyTrack<double>(
            binding: "crate",
            property: "height",
            muted: true,
            mixer: Mixers.OfDouble,
            clips: [new Keyed<double>(ClipOf(0, 4), Constant(5.0))]);
        var frame = new SequenceFrame(1);
        track.Contribute(1, frame);
        Assert.Empty(frame.Values);
    }

    [Fact]
    public void PositionsBlendAsPositions()
    {
        var track = new PropertyTrack<Vector3>(
            binding: "crate",
            property: "position",
            mixer: Mixers.OfVector3,
            clips:
            [
                new Keyed<Vector3>(
                    ClipOf(0, 2),
                    new Channel<Vector3>(
                        [new Key<Vector3>(0, new Vector3(0, 0, 0), Hold.Linear), new Key<Vector3>(2, new Vector3(10, 0, 0))],
                        Mixers.OfVector3)),
            ]);
        Assert.True(track.TryValueAt(1, out var position));
        Assert.Equal(5, position.X, 1e-9);
    }

    // activation

    [Fact]
    public void SaysOffInTheGapsAndNothingOutsideItsSpan()
    {
        var track = new ActivationTrack([new Clip(1, 1), new Clip(3, 1)], binding: "door");
        Assert.Null(track.ContributionAt(0)); // before it starts
        Assert.True(track.ContributionAt(1.5));
        Assert.False(track.ContributionAt(2.5)); // in the gap
        Assert.True(track.ContributionAt(3.5));
        Assert.Null(track.ContributionAt(9)); // after it ends
    }

    // marks

    [Fact]
    public void FireOnTheWayForwardsAndNeverOnTheWayBack()
    {
        var sequence = new CutSequence(
            [new MarkTrack([new Mark(1, "open"), new Mark(2, "shut")])],
            duration: 5);

        Assert.Equal(["open", "shut"], sequence.MarksBetween(0, 3).Select(one => one.Name));
        Assert.Empty(sequence.MarksBetween(3, 0));
        // The start of a span is exclusive, so nothing fires twice.
        Assert.Equal(["shut"], sequence.MarksBetween(1, 2).Select(one => one.Name));
    }

    [Fact]
    public void AStepBigEnoughToSkipSeveralFiresAllOfThem()
    {
        var sequence = new CutSequence(
            [new MarkTrack([new Mark(0.1, "a"), new Mark(0.2, "b"), new Mark(0.3, "c")])],
            duration: 1);
        Assert.Equal(3, sequence.MarksBetween(0, 0.9).Count);
    }

    // the director

    private static CutSequence SequenceOf() => new(
        duration: 4,
        tracks:
        [
            NumbersOn(
                "crate",
                "height",
                [
                    new Keyed<double>(
                        ClipOf(0, 4),
                        new Channel<double>(
                            [new Key<double>(0, 0.0, Hold.Linear), new Key<double>(4, 4.0)],
                            Mixers.OfDouble)),
                ]),
            new MarkTrack([new Mark(1, "ping"), new Mark(3, "pong")]),
        ]);

    [Fact]
    public void DoesNotMoveWhilePaused()
    {
        var director = new Director(SequenceOf());
        var step = director.Advance(1);
        Assert.Equal(0, director.At);
        Assert.Empty(step.Marks);
        Assert.Equal(0, Height(step.Frame));
    }

    [Fact]
    public void FiresEachMarkOnceAsItPasses()
    {
        var director = new Director(SequenceOf());
        director.Play();
        Assert.Empty(director.Advance(0.5).Marks);
        Assert.Equal(["ping"], director.Advance(1).Marks.Select(one => one.Name));
        Assert.Empty(director.Advance(1).Marks);
        Assert.Equal(["pong"], director.Advance(1).Marks.Select(one => one.Name));
    }

    [Fact]
    public void HoldingStopsOnTheLastFrame()
    {
        var director = new Director(SequenceOf());
        director.Play();
        director.Advance(10);
        Assert.Equal(4, director.At);
        Assert.False(director.Playing);
        Assert.True(director.Finished);
    }

    [Fact]
    public void LoopingWrapsAndFiresTheMarksItCrossesOnTheWayRound()
    {
        var director = new Director(SequenceOf(), WhenDone.Loop);
        director.Play();
        // Both, in order, in one step.
        Assert.Equal(["ping", "pong"], director.Advance(3.5).Marks.Select(one => one.Name));
        var wrapped = director.Advance(1.5);
        Assert.Equal(1, director.At, 1e-9);
        // Nothing left this pass, then the first of the next.
        Assert.Equal(["ping"], wrapped.Marks.Select(one => one.Name));
        Assert.True(director.Playing);
    }

    [Fact]
    public void BouncingTurnsRoundRatherThanStopping()
    {
        var director = new Director(SequenceOf(), WhenDone.Bounce);
        director.Play();
        director.Advance(5);
        Assert.Equal(3, director.At, 1e-9);
        Assert.True(director.Playing);
        director.Advance(1);
        Assert.Equal(2, director.At, 1e-9); // still going backwards
    }

    [Fact]
    public void SeekingMovesThePlayheadAndFiresNothing()
    {
        var director = new Director(SequenceOf());
        director.Play();
        director.Seek(3.5);
        Assert.Equal(3.5, director.At);
        Assert.Equal(3.5, Height(director.Sample()), 1e-9);
        // Advancing from there crosses no mark, because seeking already passed
        // them and passing them by looking is not passing them.
        Assert.Empty(director.Advance(0.1).Marks);
    }

    [Fact]
    public void SeekingIsClampedToTheSequence()
    {
        var director = new Director(SequenceOf());
        director.Seek(-5);
        Assert.Equal(0, director.At);
        director.Seek(500);
        Assert.Equal(4, director.At);
    }

    [Fact]
    public void ScrubbingBackwardsGivesTheSameValuesAsPlayingForwards()
    {
        // The clip's end is exclusive, so the last sample is null both ways.
        var sequence = SequenceOf();
        var forwards = new List<object?>();
        for (var i = 0; i <= 40; i++)
        {
            forwards.Add(sequence.SampleAt(i / 10.0).Get("crate", "height"));
        }

        var backwards = new List<object?>();
        for (var i = 40; i >= 0; i--)
        {
            backwards.Add(sequence.SampleAt(i / 10.0).Get("crate", "height"));
        }

        backwards.Reverse();
        Assert.Equal(forwards, backwards);
    }

    // shots and sounds

    [Fact]
    public void AdjacentShotsAreACutAndOverlappingOnesABlend()
    {
        var track = new ShotTrack([(new Clip(0, 2), "wide"), (new Clip(2, 2), "close")]);
        var cut = new SequenceFrame(1.9);
        track.Contribute(1.9, cut);
        Assert.Equal(["wide"], cut.Shots.Select(one => one.Camera));

        var blended = new ShotTrack(
            [(new Clip(0, 3, easeOut: 1), "wide"), (new Clip(2, 3, easeIn: 1), "close")]);
        var over = new SequenceFrame(2.5);
        blended.Contribute(2.5, over);
        Assert.Equal(2, over.Shots.Count);
        Assert.Equal(over.Shots[^1].Weight, over.Shots[0].Weight, 1e-9);
    }

    [Fact]
    public void ASoundReportsWhereInItselfThePlayheadIs()
    {
        var track = new SoundTrack([(new Clip(4, 3, clipIn: 1), "thunder.wav")]);
        var frame = new SequenceFrame(5);
        track.Contribute(5, frame);
        var sound = Assert.Single(frame.Sounds);
        Assert.Equal("thunder.wav", sound.Sound);
        Assert.Equal(2, sound.At); // one second of trim, one played
    }

    [Fact]
    public void ASequenceIsAsLongAsItsLongestTrackUnlessToldOtherwise()
    {
        List<Track> tracks =
        [
            NumbersOn("a", "x", [new Keyed<double>(ClipOf(0, 2), Constant(0.0))]),
            NumbersOn("b", "x", [new Keyed<double>(ClipOf(0, 7), Constant(0.0))]),
        ];
        Assert.Equal(7, new CutSequence(tracks).Duration);
        Assert.Equal(20, new CutSequence(tracks, duration: 20).Duration);
    }

    [Fact]
    public void SnappingLandsOnAFrameBoundary()
    {
        var sequence = new CutSequence([], rate: 24);
        Assert.Equal(12.0 / 24, sequence.Snap(0.5), 1e-9);
        Assert.Equal(12.0 / 24, sequence.Snap(0.501), 1e-9);
    }
}

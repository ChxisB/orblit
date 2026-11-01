using Orblit.Audio;
using Orblit.Input;

namespace Orblit.Test;

/// <summary>
/// A game with no window. It steps in sampled time, so a test says how long passed and the game
/// takes that many whole frames. The same test gives the same frames on every machine.
/// </summary>
public sealed class TestStage
{
    private const long MicrosPerSecond = 1_000_000;

    private readonly Action<Frame> _onFrame;

    // Microsecond-frames carried between steps, so 100 steps of 8 ms still make 48 frames.
    private long _owed;

    public TestStage(Action<Frame> onFrame, ActionMap? actions = null, int fps = 60)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fps);
        _onFrame = onFrame;
        Actions = actions;
        Fps = fps;
        Recording = new RecordedAudio();
        Audio = new AudioPlayer(Recording);
    }

    /// <summary>The actions that read <see cref="Controls"/> before each frame, or null.</summary>
    public ActionMap? Actions { get; }

    public int Fps { get; }

    public ScriptedControls Controls { get; } = new();

    /// <summary>What the game's sound asked for. Nothing is played.</summary>
    public RecordedAudio Recording { get; }

    /// <summary>The player a game plays its sound through. It moves on with each frame.</summary>
    public AudioPlayer Audio { get; }

    public int Frames { get; private set; }

    /// <summary>The time of the last frame, from the frame count so it never drifts.</summary>
    public TimeSpan Now => TimeSpan.FromTicks((long)Math.Round(Frames * 1e6 / Fps, MidpointRounding.AwayFromZero) * 10);

    /// <summary>Runs the whole frames that <paramref name="span"/> adds up to, with what was left over from before.</summary>
    public void Step(TimeSpan span)
    {
        if (span < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(span), span, "must not be negative");
        }

        _owed = checked(_owed + (span.Ticks / 10 * Fps));
        var count = _owed / MicrosPerSecond;
        _owed -= count * MicrosPerSecond;
        for (var i = 0L; i < count; i++)
        {
            RunFrame();
        }
    }

    /// <summary>Runs frames until <paramref name="done"/> is true. Fails when it is not true within <paramref name="within"/>.</summary>
    public void StepUntil(Func<bool> done, TimeSpan within)
    {
        var limit = Frames + Math.Round(within.Ticks / 10.0 * Fps / MicrosPerSecond, MidpointRounding.AwayFromZero);
        while (!done())
        {
            if (Frames >= limit)
            {
                throw new InvalidOperationException($"Not done after {within}, at frame {Frames}.");
            }

            RunFrame();
        }
    }

    private void RunFrame()
    {
        Frames++;
        Actions?.Update(Controls, Now);
        _onFrame(new Frame(1.0 / Fps, Now));
        Audio.Advance(1.0 / Fps);
    }
}

using Orblit.Camera;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>The "+N" over the runner for the coins just taken.</summary>
/// <param name="Points">What the coins were worth together.</param>
/// <param name="At">Where on the view, from -1 to 1 across and up.</param>
/// <param name="Opacity">1 at first, fading to nothing.</param>
public sealed record RunnerPopup(int Points, ScreenPoint At, double Opacity);

// What a screen over the game shows besides the scene, and the one press that
// means go.
public sealed partial class RunnerExample
{
    /// <summary>How fast the runner is going, in metres a second. Coming off a wall backwards is none.</summary>
    public double Speed => Math.Max(_speed, 0);

    /// <summary>Whether the run that ended beat the best before it.</summary>
    public bool NewBest { get; private set; }

    /// <summary>Seconds since the last crash.</summary>
    public double SinceCrash => _clock - _crashedAt;

    /// <summary>Whether the runner has just stumbled, so one more is the end of the run.</summary>
    public bool Careful => Phase == RunnerPhase.Running && _clock - _stumbledAt < 1.2;

    /// <summary>A tap, Space or Enter: whatever go means just now.</summary>
    public void Confirm()
    {
        switch (Phase)
        {
            case RunnerPhase.Ready:
                Start();
                break;
            case RunnerPhase.Paused:
                Resume();
                break;
            // Not in the moment of the crash, when a tap was meant for a jump.
            case RunnerPhase.Over when SinceCrash > 0.7:
                Restart();
                break;
            default:
                break;
        }
    }

    /// <summary>The "+N" for the coins just picked up, over the runner's head, or null when there is none.</summary>
    public RunnerPopup? Popup()
    {
        var since = _clock - _lastCoinAt;
        if (_popup == 0 || since > 1.1)
        {
            return null;
        }

        var camera = Camera();
        var at = Framing.Project(
            camera.Position,
            Rotations.LookRotation(camera.Target - camera.Position),
            new Vector3(_x, _y + 1.75 + since * 0.4, Z(_along)),
            new Lens(FieldOfView: camera.FieldOfView),
            Aspect);
        var fade = since < 0.8 ? 1 : 1 - (since - 0.8) / 0.3;
        return at.InFront ? new RunnerPopup(_popup, at, Math.Clamp(fade, 0, 1)) : null;
    }
}

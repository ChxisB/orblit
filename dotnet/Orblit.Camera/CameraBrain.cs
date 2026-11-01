using Orblit.VectorMath;

namespace Orblit.Camera;

/// <summary>
/// Chooses which shot is live, and gets there. The single point where "which camera" is
/// decided. Game code raises and lowers priorities and never touches a transform, which is
/// what stops two systems fighting over the camera.
/// </summary>
public sealed class CameraBrain(double aspect = 16.0 / 9, BlendTable? blends = null)
{
    private readonly List<VirtualCamera> _cameras = [];
    private double _aspect = aspect;
    private CameraState? _blendFrom;
    private Blend _blend = Blend.Cut();
    private double _blendElapsed;
    private double _time;

    /// <summary>
    /// Frame width over height. Affects composition, since a dead zone is a fraction of the
    /// frame and the frame is not square. Anything that is not a real ratio is ignored rather
    /// than stored. A host reads this off its own surface, and a surface that has not been laid
    /// out yet is zero by zero, which is a NaN. One of those reaches the projection, comes back
    /// out as a rotation, and is damped towards on the next frame from a value that is already
    /// NaN, so nothing recovers. Refusing it here is the only version of this that stays fixed.
    /// </summary>
    public double Aspect
    {
        get => _aspect;
        set
        {
            if (double.IsFinite(value) && value > 0)
            {
                _aspect = value;
            }
        }
    }

    public BlendTable Blends { get; } = blends ?? new BlendTable();

    /// <summary>What the real camera should be doing this frame.</summary>
    public CameraState State { get; } = new();

    /// <summary>The camera currently in charge, or null if none is enabled.</summary>
    public VirtualCamera? Live { get; private set; }

    /// <summary>Between zero and one while a transition is running.</summary>
    public double BlendProgress => _blendFrom is null ? 1 : Doubles.Clamp(_blendElapsed / _blend.Duration, 0.0, 1.0);

    public bool IsBlending => _blendFrom is not null;

    public IReadOnlyList<VirtualCamera> Cameras => _cameras.ToArray();

    public void Add(VirtualCamera camera)
    {
        _cameras.Add(camera);
        camera.Snap(Aspect);
    }

    public void Remove(VirtualCamera camera)
    {
        _cameras.Remove(camera);
        if (Live == camera)
        {
            Live = null;
        }
    }

    public void Update(double delta)
    {
        _time += delta;

        // Every camera keeps solving, live or not. A camera that only started thinking when it
        // became live would cut in from wherever it was left.
        foreach (var camera in _cameras.Where(camera => camera.Enabled))
        {
            camera.Solve(delta, Aspect);
        }

        var wanted = Choose();
        if (wanted != Live)
        {
            Switch(wanted);
        }

        if (Live?.State is not { } target)
        {
            return;
        }

        if (_blendFrom is null)
        {
            CopyFrom(target);
        }
        else
        {
            _blendElapsed += delta;
            var t = _blend.Ease(_blendElapsed / _blend.Duration);
            var blended = CameraState.Lerp(_blendFrom, target, t);
            State.Position = blended.Position;
            State.Rotation = blended.Rotation;
            State.Lens = blended.Lens;
            if (t >= 1)
            {
                _blendFrom = null;
            }
        }

        ApplyNoise();
    }

    /// <summary>Cuts to whatever should be live, with no transition.</summary>
    public void Snap()
    {
        foreach (var camera in _cameras.Where(camera => camera.Enabled))
        {
            camera.Snap(Aspect);
        }

        Live = Choose();
        _blendFrom = null;
        if (Live?.State is { } target)
        {
            CopyFrom(target);
        }
    }

    // The enabled camera with the highest priority. Ties go to the one added first, so a
    // scene's ordering is a tiebreak rather than a coin toss.
    private VirtualCamera? Choose()
    {
        VirtualCamera? best = null;
        foreach (var camera in _cameras.Where(camera => camera.Enabled))
        {
            if (best is null || camera.Priority > best.Priority)
            {
                best = camera;
            }
        }

        return best;
    }

    private void Switch(VirtualCamera? wanted)
    {
        // Blending from the current output rather than from the outgoing camera, so
        // interrupting a transition continues from what is on screen instead of jumping back to
        // where the last one started.
        var blend = Blends.Between(Live?.Name, wanted?.Name);
        if (Live is not null && wanted is not null && blend.Duration > 0)
        {
            _blendFrom = State.Clone();
            _blend = blend;
            _blendElapsed = 0;
        }
        else
        {
            _blendFrom = null;
        }

        Live = wanted;
    }

    private void CopyFrom(CameraState target)
    {
        State.Position = target.Position.Clone();
        State.Rotation = target.Rotation.Clone();
        State.Lens = target.Lens;
    }

    // Handheld movement, added last so it disturbs the finished frame rather than the framing
    // decision. A composer should not fight a wobble.
    private void ApplyNoise()
    {
        if (Live?.Noise is not { } noise)
        {
            return;
        }

        State.Position += Rotations.RotateVector(State.Rotation, noise.PositionAt(_time));

        var wobble = noise.RotationAt(_time) * (3.14159265358979 / 180);
        var turn = Quaternion.Euler(wobble.Y, wobble.X, wobble.Z);
        turn.Normalize();
        State.Rotation *= turn;
    }
}

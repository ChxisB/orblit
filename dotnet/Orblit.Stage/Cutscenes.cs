using Orblit.Filament;
using Orblit.Motion;
using Orblit.Scene;
using Orblit.Sequence;
using Orblit.VectorMath;

namespace Orblit.Stage;

/// <summary>The cutscenes a game can play over a staged scene, one at a time.</summary>
/// <remarks>
/// A cutscene is started by its name, from game code or from a mark. While it runs, each
/// <see cref="Advance"/> moves what it keys and has the view look through its shots. When it is over
/// the view looks through the scene's own camera again. A cutscene that holds leaves the scene where
/// it put it, and one that releases puts back everything it moved.
/// </remarks>
public sealed class Cutscenes
{
    private readonly DocumentView _view;
    private readonly Dictionary<string, CutsceneDocument> _byName = [];
    private Running? _running;

    public Cutscenes(DocumentView view, IEnumerable<CutsceneDocument> cutscenes)
    {
        _view = view;
        foreach (var cutscene in cutscenes)
        {
            if (!_byName.TryAdd(cutscene.Name, cutscene))
            {
                throw new ArgumentException(
                    $"Two cutscenes are called \"{cutscene.Name}\", so one could never be started.",
                    nameof(cutscenes));
            }
        }
    }

    /// <summary>The name of the cutscene running, or null when none is.</summary>
    public string? Playing => _running?.Cutscene.Name;

    /// <summary>Whether there is a cutscene called <paramref name="name"/> to start.</summary>
    public bool Has(string name) => _byName.ContainsKey(name);

    /// <summary>Starts the cutscene called <paramref name="name"/> from its beginning, ending whichever one was running first.</summary>
    public void Start(string name)
    {
        if (!_byName.TryGetValue(name, out var cutscene))
        {
            throw new ArgumentException($"There is no cutscene called \"{name}\".", nameof(name));
        }

        Stop();
        var director = new Director(cutscene.Sequence, cutscene.Motion.WhenDone);
        director.Play();
        _running = new Running(cutscene, director);
        Show(_running);
    }

    /// <summary>
    /// Starts the cutscene named by the first of <paramref name="marks"/> that names one. A mark named
    /// after a cutscene is how a clip, or another cutscene, starts one without code.
    /// </summary>
    public void StartFrom(IEnumerable<Mark> marks)
    {
        if (marks.FirstOrDefault(mark => Has(mark.Name)) is { } found)
        {
            Start(found.Name);
        }
    }

    /// <summary>Ends the cutscene running now, as though it had reached its end.</summary>
    public void Stop()
    {
        if (_running is { } running)
        {
            End(running);
        }
    }

    /// <summary>Plays on by <paramref name="seconds"/>, and says what happened on the way.</summary>
    public CutsceneStep Advance(double seconds)
    {
        if (_running is not { } running)
        {
            return new CutsceneStep();
        }

        var step = running.Director.Advance(seconds);
        Show(running);
        if (!running.Director.Finished)
        {
            return new CutsceneStep(step.Marks, step.Frame.Sounds);
        }

        End(running);
        return new CutsceneStep(step.Marks, ended: true);
    }

    /// <summary>
    /// The view from between <paramref name="cameras"/>, each with how much it has to say. Where they
    /// stand and which way they look are averaged by weight, and so is the field of view. The rest is
    /// the heaviest camera's, since an exposure halfway between two is not something a shot is framed
    /// with. Null when no camera has any say.
    /// </summary>
    public static RenderCamera? BlendCameras(IEnumerable<(RenderCamera Camera, double Weight)> cameras)
    {
        var weighted = cameras.Where(static one => one.Weight > 0).ToList();
        if (weighted.Count == 0)
        {
            return null;
        }

        if (weighted.Count == 1)
        {
            return weighted[0].Camera;
        }

        var (heaviest, most) = weighted[0];
        var total = 0.0;
        var fieldOfView = 0.0;
        var position = Vector3.Zero();
        var facing = Vector3.Zero();
        foreach (var (camera, weight) in weighted)
        {
            total += weight;
            fieldOfView += camera.FieldOfView * weight;
            position.AddScaled(camera.Position, weight);
            facing.AddScaled(Facing(camera), weight);
            if (weight > most)
            {
                (heaviest, most) = (camera, weight);
            }
        }

        position.Scale(1 / total);

        // Two cameras looking exactly opposite ways add up to looking nowhere, so look the heaviest
        // one's way instead.
        var forward = facing.Length2 < 1e-12 ? Facing(heaviest) : facing.Normalized();
        return heaviest with
        {
            Position = position,
            Target = position + forward,
            FieldOfView = fieldOfView / total,
        };
    }

    private static Vector3 Facing(RenderCamera camera) => (camera.Target - camera.Position).Normalized();

    // Keys first, since a camera may be one of the things a cutscene moves.
    private void Show(Running running)
    {
        var at = running.Director.At;
        var ops = ClipScene.OpsFor(running.Cutscene.Motion.SampleAt(at), _view.Document, ClipScope.WholeScene);
        foreach (var op in ops)
        {
            running.Before.TryAdd((op.Id, op.Type, op.Field), op.From);
        }

        _view.Apply(new SceneDiff(ops));
        _view.Through = BlendCameras(running.Cutscene.ShotsAt(at)
            .Select(shot => (Camera: _view.CameraOf(shot.Camera), shot.Weight))
            .Where(static one => one.Camera is not null)
            .Select(static one => (one.Camera!, one.Weight)));
    }

    private void End(Running running)
    {
        _running = null;
        _view.Through = null;
        if (running.Director.WhenDone == WhenDone.Release)
        {
            _view.Apply(new SceneDiff(PutBack(running.Before)));
        }
    }

    // The changes that put back every field a cutscene moved to what it was before the cutscene first
    // moved it.
    private List<SetField> PutBack(OrderedDictionary<(string Id, string Type, string Field), object?> before)
    {
        var changes = new List<SetField>();
        foreach (var ((id, type, field), was) in before)
        {
            if (_view.Document[id]?[type] is { } component && component.ToJson().GetValueOrDefault(field) is var now && !Values.Same(now, was))
            {
                changes.Add(new SetField(id, type, field, from: now, to: was));
            }
        }

        return changes;
    }

    private sealed class Running(CutsceneDocument cutscene, Director director)
    {
        public CutsceneDocument Cutscene { get; } = cutscene;

        public Director Director { get; } = director;

        /// <summary>Each field the cutscene has moved, as it was before the first move.</summary>
        public OrderedDictionary<(string Id, string Type, string Field), object?> Before { get; } = [];
    }
}

/// <summary>What happened in one <see cref="Cutscenes.Advance"/>.</summary>
public sealed class CutsceneStep(IReadOnlyList<Mark>? marks = null, IReadOnlyList<SoundAt>? sounds = null, bool ended = false)
{
    /// <summary>The marks passed, in order, for the game to act on.</summary>
    public IReadOnlyList<Mark> Marks { get; } = marks ?? [];

    /// <summary>The sounds that should be playing now, and how far into each.</summary>
    public IReadOnlyList<SoundAt> Sounds { get; } = sounds ?? [];

    /// <summary>Whether the cutscene ended in this step.</summary>
    public bool Ended { get; } = ended;
}

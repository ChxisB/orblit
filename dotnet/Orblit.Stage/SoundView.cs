using Orblit.Audio;
using Orblit.Scene;
using Orblit.Sequence;
using Orblit.VectorMath;
using AudioMotion = Orblit.Audio.Motion;

namespace Orblit.Stage;

/// <summary>The sound of a staged scene.</summary>
/// <remarks>
/// A <see cref="SoundSourceComponent"/> is heard from where its entity is, for as long as the entity
/// is shown. The sound is heard from the first entity with a <see cref="SoundListenerComponent"/>, or
/// from the view's camera when there is none. A cutscene's sound cues play through the same call, from
/// the step that <see cref="Cutscenes.Advance"/> returned.
///
/// Call <see cref="Advance"/> and then <see cref="AudioPlayer.Advance"/> once a frame. The first puts
/// the listener and the emitters where the scene has them, and the second tells the backend what that
/// sounds like.
/// </remarks>
public sealed class SoundView
{
    private const double Release = 0.05;

    private readonly DocumentView _view;
    private readonly AudioPlayer _audio;
    private readonly CueBoard _cues;
    private readonly OrderedDictionary<string, Source> _sources = [];
    private OrderedDictionary<string, SoundSourceComponent> _wanted = [];
    private SceneDocument? _synced;
    private string? _listenerId;
    private bool _heard;

    /// <param name="view">The scene the sound follows.</param>
    /// <param name="audio">Where the sound is played.</param>
    /// <param name="cueBus">The bus a cutscene's sounds play on, or the master bus.</param>
    public SoundView(DocumentView view, AudioPlayer audio, Bus? cueBus = null)
    {
        _view = view;
        _audio = audio;
        _cues = new CueBoard(audio, cueBus, emitterFor: EmitterFor, release: Release);
    }

    private bool Changed => !ReferenceEquals(_view.Document, _synced);

    /// <summary>What the entity <paramref name="id"/> is made of, as far as sound goes, or null when it has no surface to name.</summary>
    public string? SurfaceOf(string id) =>
        _view.Document[id]?[SceneComponents.SoundSurface] is SoundSurfaceComponent { Name.Length: > 0 } surface ? surface.Name : null;

    /// <summary>Plays the source on <paramref name="id"/> again from the start. A source that is not in the scene, or not shown, makes no sound.</summary>
    public void Play(string id)
    {
        if (Changed)
        {
            Scan();
            if (!_heard)
            {
                Hear(0);
            }

            Reconcile();
        }

        if (_sources.TryGetValue(id, out var source))
        {
            source.Voice?.Stop(fadeSeconds: Release);
            source.Voice = Begin(id, source);
        }
    }

    /// <summary>Moves the listener and the sources to where the scene has them, adds and drops sources as the scene changed, and plays the cues in <paramref name="step"/>.</summary>
    public void Advance(double seconds, CutsceneStep? step = null)
    {
        // The listener goes first, since a voice that starts is given the level it has for where the
        // listener is.
        var changed = Changed;
        if (changed)
        {
            Scan();
        }

        Hear(seconds);
        if (changed)
        {
            Reconcile();
        }

        foreach (var (id, source) in _sources)
        {
            Glide(source.Emitter, _view.WorldOf(id).GetTranslation(), seconds);
        }

        _cues.Update((step?.Sounds ?? []).Select(static sound => new Cue(sound.Sound, sound.At, sound.Gain, sound.Binding)));
    }

    /// <summary>Fades out every voice this made, for the end of a scene. A scene that changes after this starts its sources again.</summary>
    public void Stop()
    {
        foreach (var source in _sources.Values)
        {
            source.Voice?.Stop(fadeSeconds: Release);
        }

        _sources.Clear();
        _cues.Stop();
    }

    /// <summary>What a source's voice cannot change once it is playing.</summary>
    private static (string?, string, bool, double, double, SoundCurve) Shape(SoundSourceComponent source) =>
        (source.Clip, source.Bus, source.Loop, source.Near, source.Far, source.Curve);

    // A move faster than Doppler can follow is a cut, which has no speed.
    private static void Glide(AudioMotion motion, Vector3 to, double seconds)
    {
        if (motion.Position.DistanceTo(to) > Acoustics.SpeedOfSound / 2 * seconds)
        {
            motion.Teleport(to);
        }
        else
        {
            motion.MoveTo(to, seconds);
        }
    }

    // Reads which sources should be sounding, by entity, and which entity is the listener.
    private void Scan()
    {
        _listenerId = null;
        _wanted = [];
        foreach (var entity in _view.Document.Entities)
        {
            if (!_view.IsShown(entity.Id))
            {
                continue;
            }

            if (entity[SceneComponents.SoundListener] is not null)
            {
                _listenerId ??= entity.Id;
            }

            if (entity[SceneComponents.SoundSource] is SoundSourceComponent { Clip: not null } source)
            {
                _wanted[entity.Id] = source;
            }
        }
    }

    // Starts the sources that are wanted and not sounding, follows the ones that stay and drops the ones
    // that are not wanted.
    private void Reconcile()
    {
        foreach (var id in _sources.Keys.ToList())
        {
            var source = _sources[id];
            if (_wanted.TryGetValue(id, out var next) && Shape(next) == Shape(source.Component))
            {
                source.Component = next;
                if (source.Voice is { } voice)
                {
                    voice.Volume = next.Volume;
                }
            }
            else
            {
                _sources.Remove(id);
                source.Voice?.Stop(fadeSeconds: Release);
            }
        }

        foreach (var (id, component) in _wanted)
        {
            if (_sources.ContainsKey(id))
            {
                continue;
            }

            var source = new Source(component, EmitterOf(id, component));
            source.Voice = Begin(id, source);
            _sources[id] = source;
        }

        // Last, so a source that cannot start is tried again next frame and fails again, and not
        // forgotten after one error.
        _synced = _view.Document;
    }

    private Emitter EmitterOf(string id, SoundSourceComponent source) => new(
        _view.WorldOf(id).GetTranslation(),
        new Falloff(
            source.Near,
            source.Far,
            source.Curve switch
            {
                SoundCurve.Linear => FalloffCurve.Linear,
                _ => FalloffCurve.Inverse,
            }));

    private Voice Begin(string id, Source source)
    {
        var component = source.Component;
        Bus bus;
        try
        {
            bus = _audio.Mixer.Bus(component.Bus);
        }
        catch (ArgumentOutOfRangeException error)
        {
            throw new ArgumentException($"The sound source \"{id}\" plays on a bus the mixer does not have.", error);
        }

        // Scan only keeps a source that has a clip.
        return _audio.Play(component.Clip!, bus, component.Loop, component.Volume, at: source.Emitter);
    }

    private void Hear(double seconds)
    {
        var listener = _audio.Listener;
        Vector3 at;
        Vector3 forward;
        Vector3 up;
        if (_listenerId is { } id)
        {
            var world = _view.WorldOf(id);
            at = world.GetTranslation();
            forward = world.Rotated3(new Vector3(0, 0, -1)).Normalized();
            up = world.Rotated3(new Vector3(0, 1, 0)).Normalized();
        }
        else
        {
            var camera = _view.Camera;
            at = camera.Position;
            forward = (camera.Target - camera.Position).Normalized();

            // shortcut: the camera's up is taken as world up, so a camera looking straight up or down
            // hears no pan, give RenderCamera an up if one does.
            up = new Vector3(0, 1, 0);
        }

        if (_heard)
        {
            Glide(listener, at, seconds);
        }
        else
        {
            listener.Teleport(at);
            _heard = true;
        }

        listener.Forward = forward;
        listener.Up = up;
    }

    // Where a cue bound to an entity is heard from, fixed where the entity stands when the cue starts, or
    // nowhere for a binding that is no entity.
    // shortcut: the emitter does not follow the entity, make it one of _sources if a cue's sound needs
    // to travel with what it comes from.
    private Emitter? EmitterFor(string binding) => _view.Document.Contains(binding)
        ? new Emitter(_view.WorldOf(binding).GetTranslation())
        : null;

    private sealed class Source(SoundSourceComponent component, Emitter emitter)
    {
        public SoundSourceComponent Component { get; set; } = component;

        public Emitter Emitter { get; } = emitter;

        public Voice? Voice { get; set; }
    }
}

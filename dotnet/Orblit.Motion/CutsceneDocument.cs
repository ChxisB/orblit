using Orblit.Scene;
using Orblit.Sequence;

namespace Orblit.Motion;

/// <summary>A file that is not a cutscene, or is one from a newer Orblit.</summary>
public sealed class CutsceneFormatException(string message) : Exception(message);

/// <summary>A cutscene read back off disk, with anything that could not be read.</summary>
public sealed class CutsceneLoad(CutsceneDocument cutscene, IReadOnlyList<string>? problems = null)
{
    public CutsceneDocument Cutscene { get; } = cutscene;

    public IReadOnlyList<string> Problems { get; } = problems ?? [];
}

/// <summary>
/// A cutscene: the scene moving while it is watched through cameras that take turns.
/// </summary>
/// <remarks>
/// Its keys are a clip's, named by the scene's own ids rather than by a path from whoever plays it, since
/// the scene plays a cutscene and nothing in it does. On top of those it has shots, which say which camera
/// is looked through when, and sounds. Two shots that overlap fade from one camera to the other over the
/// time both run. Playing one is a <see cref="Director"/> over <see cref="Sequence"/>, which holds the
/// shots, sounds and marks. The keys are sampled from <see cref="Motion"/> at the director's time.
/// </remarks>
public sealed class CutsceneDocument
{
    /// <summary>The extension a cutscene file carries.</summary>
    public const string Extension = ".ocutscene";

    public const string Marker = "orblit.cutscene";

    /// <summary>The shape of the file. Bumped with a migration whenever it changes.</summary>
    public const int FormatVersion = 1;

    /// <summary>
    /// Every step from an older cutscene file to this one, oldest first. None yet: this is the first
    /// format.
    /// </summary>
    public static readonly IReadOnlyList<CutsceneMigration> Migrations = [];

    private static readonly FileFormat Format = new(
        Marker,
        "cutscene",
        FormatVersion,
        message => new CutsceneFormatException(message),
        Migrations);

    // A stable sort on the total order, so shots that start together keep the order they were given in.
    private static readonly Comparer<double> ByStart = Comparer<double>.Create(Doubles.Compare);

    private readonly Lazy<CutSequence> _sequence;

    public CutsceneDocument(
        ClipDocument motion,
        IReadOnlyList<CutsceneShot>? shots = null,
        IReadOnlyList<CutsceneSound>? sounds = null)
    {
        if (motion.Channels.Any(static channel => channel.Bone is not null) || motion.RootMotion is not null)
        {
            throw new ArgumentException("A cutscene moves what is in the scene, not the bones of a model.", nameof(motion));
        }

        Motion = motion;
        Shots = (shots ?? []).OrderBy(static shot => shot.Start, ByStart).ToList().AsReadOnly();
        Sounds = (sounds ?? []).OrderBy(static sound => sound.Start, ByStart).ToList().AsReadOnly();
        _sequence = new Lazy<CutSequence>(BuildSequence);
    }

    /// <summary>
    /// Its keys and marks, its length, its frame rate and what it does at the end, as a clip whose targets
    /// are the scene's ids.
    /// </summary>
    /// <remarks>
    /// A clip rather than property tracks of a sequence, because a clip's keys hold after the last one. A
    /// sequence's tracks are over at the moment they end, so a cutscene held on its last frame would lose
    /// where its last keys put things.
    /// </remarks>
    public ClipDocument Motion { get; }

    /// <summary>In order of when they start.</summary>
    public IReadOnlyList<CutsceneShot> Shots { get; }

    /// <summary>In order of when they start.</summary>
    public IReadOnlyList<CutsceneSound> Sounds { get; }

    public string Name => Motion.Name;

    public double Duration => Motion.Duration;

    /// <summary>
    /// Its shots, sounds and marks, for a <see cref="Director"/> to play. Nothing is looked through before
    /// the first shot, after the last, or at the very end, where every shot is over.
    /// </summary>
    public CutSequence Sequence => _sequence.Value;

    /// <summary>A cutscene out of a file's text, with whatever could not be read.</summary>
    /// <remarks>
    /// Lenient about the parts, strict about the whole, like a clip: a shot that cannot be read is dropped
    /// with a note, and a file that is not a cutscene, or is one from a newer Orblit, throws
    /// <see cref="CutsceneFormatException"/>.
    /// </remarks>
    public static CutsceneLoad Decode(string text)
    {
        var problems = new List<string>();
        var json = Format.Open(text, problems);
        var shots = ReadAll(json.GetValueOrDefault("shots"), problems, CutsceneShot.FromJson);
        var sounds = ReadAll(json.GetValueOrDefault("sounds"), problems, CutsceneSound.FromJson);
        var motion = SceneOnly(ClipDocument.FromJson(json, problems), problems);
        if (Values.Text(json, "name") is null)
        {
            motion = motion.CopyWith(name: "Cutscene");
        }

        // A length that is missing is the last thing the cutscene does, which may be a shot or a sound
        // rather than a key.
        if (Values.MaybeNumber(json, "duration") is not >= 0)
        {
            var ends = shots.Select(static shot => shot.End).Concat(sounds.Select(static sound => sound.End));
            motion = motion.CopyWith(duration: ends.Aggregate(motion.Duration, Math.Max));
        }

        return new CutsceneLoad(new CutsceneDocument(motion, shots, sounds), problems);
    }

    /// <summary>The cameras looked through at <paramref name="at"/> seconds in, each with how much it has to say. Two where shots overlap, and none where there is no shot.</summary>
    public List<ShotAt> ShotsAt(double at) => Sequence.SampleAt(at).Shots;

    public CutsceneDocument CopyWith(
        ClipDocument? motion = null,
        IReadOnlyList<CutsceneShot>? shots = null,
        IReadOnlyList<CutsceneSound>? sounds = null) => new(motion ?? Motion, shots ?? Shots, sounds ?? Sounds);

    public OrderedDictionary<string, object?> ToJson()
    {
        var clip = Motion.ToJson();
        return Values.Pruned(new OrderedDictionary<string, object?>
        {
            ["kind"] = Marker,
            ["formatVersion"] = FormatVersion,
            ["name"] = clip["name"],
            ["duration"] = clip["duration"],
            ["rate"] = clip["rate"],
            ["whenDone"] = clip["whenDone"],
            ["shots"] = Shots.Select(static shot => (object?)shot.ToJson()).ToList(),
            ["sounds"] = Sounds.Select(static sound => (object?)sound.ToJson()).ToList(),
            ["channels"] = clip["channels"],
            ["marks"] = clip["marks"],
        });
    }

    /// <summary>The file's text, written the way a clip's is: one key, mark, shot or sound to a line.</summary>
    public string Encode() => FileText.Of(ToJson());

    private static List<T> ReadAll<T>(object? raw, List<string> problems, Func<object?, List<string>, T?> read)
        where T : class
    {
        var all = new List<T>();
        foreach (var one in raw as JsonList ?? Array.Empty<object?>())
        {
            if (read(one, problems) is { } item)
            {
                all.Add(item);
            }
        }

        return all;
    }

    /// <summary>
    /// <paramref name="motion"/> without the keys on bones and the root motion a clip can have and a
    /// cutscene cannot, with a note in <paramref name="problems"/> when it had any.
    /// </summary>
    private static ClipDocument SceneOnly(ClipDocument motion, List<string> problems)
    {
        var kept = motion.Channels.Where(static channel => channel.Bone is null).ToList();
        if (kept.Count == motion.Channels.Count && motion.RootMotion is null)
        {
            return motion;
        }

        problems.Add("A cutscene moves what is in the scene, not the bones of a model. Its keys on bones were left out.");
        return motion.CopyWith(channels: kept, clearRootMotion: true);
    }

    /// <summary>
    /// How long two shots run together: the end of the first past the start of the second, at most the
    /// shorter of the two.
    /// </summary>
    private static double Overlap(CutsceneShot first, CutsceneShot second) =>
        Doubles.Clamp(first.End - second.Start, 0, Math.Min(first.Duration, second.Duration));

    private CutSequence BuildSequence() => new(
        [
            new ShotTrack(ShotClips()),
            new SoundTrack([.. Sounds.Select(static sound => (new Clip(sound.Start, sound.Duration), sound.Sound))]),
            new MarkTrack(Motion.Marks),
        ],
        Name,
        Motion.Rate,
        Duration);

    /// <summary>
    /// Each shot as a clip of the shot track, faded in over its overlap with the shot before and out over
    /// its overlap with the shot after. Both fades are smoothstep, which is symmetric, so across an
    /// overlap the two weights always add up to one.
    /// </summary>
    private (Clip Clip, string Camera)[] ShotClips() => [.. Shots.Select((shot, i) => (
        new Clip(
            shot.Start,
            shot.Duration,
            easeIn: i == 0 ? 0 : Overlap(Shots[i - 1], shot),
            easeOut: i == Shots.Count - 1 ? 0 : Overlap(shot, Shots[i + 1]),
            shapeIn: Easing.Smooth,
            shapeOut: Easing.Smooth),
        shot.Camera))];
}

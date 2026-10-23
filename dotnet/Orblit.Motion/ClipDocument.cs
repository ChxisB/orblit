using Orblit.Scene;
using Orblit.Sequence;

namespace Orblit.Motion;

/// <summary>A file that is not a clip, or is one from a newer Orblit.</summary>
public sealed class ClipFormatException(string message) : Exception(message);

/// <summary>A clip read back off disk, with anything that could not be read.</summary>
public sealed class ClipLoad(ClipDocument clip, IReadOnlyList<string>? problems = null)
{
    public ClipDocument Clip { get; } = clip;

    public IReadOnlyList<string> Problems { get; } = problems ?? [];
}

/// <summary>
/// An animation: what moves, through which keys, over how long.
/// </summary>
/// <remarks>
/// A clip is an asset in the way a sequence is: it names what it moves by path, never by object, so it can
/// be written before the thing it animates exists and played on every copy of it afterwards. Sampling it is
/// stateless, so a scrub backwards shows exactly what playing forwards to the same moment would.
/// </remarks>
public sealed class ClipDocument
{
    /// <summary>The extension a clip file carries.</summary>
    public const string Extension = ".oclip";

    public const string Marker = "orblit.clip";

    /// <summary>The shape of the file. Bumped with a migration whenever it changes.</summary>
    public const int FormatVersion = 1;

    /// <summary>
    /// Every step from an older clip file to this one, oldest first. None yet: this is the first format. A
    /// step is decoded JSON in and out, like a scene's.
    /// </summary>
    public static readonly IReadOnlyList<ClipMigration> Migrations = [];

    private static readonly FileFormat Format = new(
        Marker,
        "clip",
        FormatVersion,
        message => new ClipFormatException(message),
        Migrations);

    private readonly RootTrack? _root;

    // F2: seven parameters, all but the first two optional and called by name.
    public ClipDocument(
        string name,
        double duration,
        WhenDone whenDone = WhenDone.Hold,
        double rate = 30,
        IReadOnlyList<ClipChannel>? channels = null,
        IReadOnlyList<Mark>? marks = null,
        RootMotion? rootMotion = null)
    {
        Name = name;
        Duration = duration;
        WhenDone = whenDone;
        Rate = rate;
        Channels = (channels ?? []).ToList().AsReadOnly();
        Marks = (marks ?? []).OrderBy(static mark => mark.At).ToList().AsReadOnly();
        RootMotion = rootMotion;
        _root = RootTrack.Of(this);
    }

    public string Name { get; }

    /// <summary>
    /// How long it is, in seconds. Not always the last key: a clip can hold still at the end, and a loop's
    /// last key is often short of the length so that the first key is where it lands.
    /// </summary>
    public double Duration { get; }

    /// <summary>What playing does at the end, until whoever plays it says otherwise.</summary>
    public WhenDone WhenDone { get; }

    /// <summary>
    /// Frames a second it was authored at, which is what a timeline snaps to. A clip's own, because a clip
    /// made at 24 snapped to 30 lands between its own keys.
    /// </summary>
    public double Rate { get; }

    public IReadOnlyList<ClipChannel> Channels { get; }

    /// <summary>Moments something happens, a footstep or a swing landing. In order.</summary>
    public IReadOnlyList<Mark> Marks { get; }

    /// <summary>Which channel moves the character, or null when none does.</summary>
    public RootMotion? RootMotion { get; }

    /// <summary>A clip out of a file's text, with whatever could not be read.</summary>
    /// <remarks>
    /// Lenient about the parts, strict about the whole: a key that cannot be read is dropped with a note, and
    /// a file that is not a clip, or is one from a newer Orblit, throws <see cref="ClipFormatException"/>.
    /// </remarks>
    public static ClipLoad Decode(string text)
    {
        var problems = new List<string>();
        var json = Format.Open(text, problems);
        return new ClipLoad(FromJson(json, problems), problems);
    }

    /// <summary>
    /// A clip out of decoded JSON already at <see cref="FormatVersion"/>, adding to
    /// <paramref name="problems"/> whatever could not be read.
    /// </summary>
    /// <remarks>
    /// The part of <see cref="Decode"/> a cutscene shares, since a cutscene's keys and marks are written the
    /// way a clip's are.
    /// </remarks>
    public static ClipDocument FromJson(JsonMap json, List<string> problems)
    {
        var channels = ReadChannels(json.GetValueOrDefault("channels"), problems);
        var marks = ReadMarks(json.GetValueOrDefault("marks"), problems);

        var last = channels.Select(static channel => channel.End).Concat(marks.Select(static mark => mark.At)).Prepend(0.0).Max();
        var stated = Values.MaybeNumber(json, "duration");
        var rate = Values.MaybeNumber(json, "rate");

        return new ClipDocument(
            Values.Text(json, "name") ?? "Clip",

            // A length that is missing, or shorter than nothing, is the last thing the clip does. One
            // shorter than its keys is kept: a loop cut before its last key is a thing people do on purpose.
            stated is >= 0 ? stated.Value : last,
            FileNames.Named<WhenDone>(json.GetValueOrDefault("whenDone")) ?? WhenDone.Hold,
            rate is > 0 ? rate.Value : 30,
            channels,
            marks,
            RootMotion.FromJson(json.GetValueOrDefault("rootMotion")));
    }

    /// <summary>The channel moving <paramref name="property"/> of <paramref name="target"/>, or of its <paramref name="bone"/>.</summary>
    public ClipChannel? ChannelFor(string target, string property, string? bone = null) =>
        Channels.FirstOrDefault(channel => channel.Target == target && channel.Bone == bone && channel.Property == property);

    /// <summary>
    /// What the clip says at <paramref name="at"/> seconds in.
    /// </summary>
    /// <remarks>
    /// As authored, unless <paramref name="inPlace"/>: then the part of the root that goes to the character
    /// is taken out, and the model walks on the spot while <see cref="RootStepBetween"/> says how far. A
    /// player asks for it in place; a timeline scrubbing a walk wants to see it walk.
    /// </remarks>
    public ClipFrame SampleAt(double at, bool inPlace = false)
    {
        var frame = ClipFrame.Sample(this, at);
        if (inPlace)
        {
            _root?.HoldInPlace(frame);
        }

        return frame;
    }

    /// <summary>
    /// How far <see cref="RootMotion"/> carries the character from <paramref name="from"/> seconds in to
    /// <paramref name="to"/>. Nowhere, when the clip has no root motion.
    /// </summary>
    public RootStep RootStepBetween(double from, double to) => _root?.Step(from, to) ?? new RootStep();

    /// <summary>
    /// Every mark after <paramref name="from"/> and up to and including <paramref name="to"/>. Half-open that
    /// way round so a mark on a frame boundary fires exactly once, in the step that reaches it.
    /// </summary>
    public IEnumerable<Mark> MarksBetween(double from, double to) => Marks.Where(mark => mark.At > from && mark.At <= to);

    // F2: eight parameters, all optional and called by name.
    public ClipDocument CopyWith(
        string? name = null,
        double? duration = null,
        WhenDone? whenDone = null,
        double? rate = null,
        IReadOnlyList<ClipChannel>? channels = null,
        IReadOnlyList<Mark>? marks = null,
        RootMotion? rootMotion = null,
        bool clearRootMotion = false) => new(
        name ?? Name,
        duration ?? Duration,
        whenDone ?? WhenDone,
        rate ?? Rate,
        channels ?? Channels,
        marks ?? Marks,
        clearRootMotion ? null : rootMotion ?? RootMotion);

    public OrderedDictionary<string, object?> ToJson() => Values.Pruned(new OrderedDictionary<string, object?>
    {
        ["kind"] = Marker,
        ["formatVersion"] = FormatVersion,
        ["name"] = Name,
        ["duration"] = Duration,
        ["rate"] = Rate,
        ["whenDone"] = FileNames.Of(WhenDone),
        ["rootMotion"] = RootMotion?.ToJson(),
        ["channels"] = Channels.Select(static channel => (object?)channel.ToJson()).ToList(),
        ["marks"] = Marks.Select(static mark => (object?)MarkToJson(mark)).ToList(),
    });

    /// <summary>The file's text: indented like a scene, but with one key to a line.</summary>
    public string Encode() => FileText.Of(ToJson());

    private static OrderedDictionary<string, object?> MarkToJson(Mark mark) => Values.Pruned(new OrderedDictionary<string, object?>
    {
        ["at"] = mark.At,
        ["name"] = mark.Name,
        ["payload"] = mark.Payload,
    });

    private static List<ClipChannel> ReadChannels(object? raw, List<string> problems)
    {
        var channels = new List<ClipChannel>();
        foreach (var one in raw as JsonList ?? Array.Empty<object?>())
        {
            if (ClipChannel.FromJson(one, problems) is not { } channel)
            {
                continue;
            }

            if (channels.Any(channel.SameAddress))
            {
                problems.Add($"Two channels move {channel.Property} on the same thing; the second was left out.");
                continue;
            }

            channels.Add(channel);
        }

        return channels;
    }

    private static List<Mark> ReadMarks(object? raw, List<string> problems)
    {
        var marks = new List<Mark>();
        foreach (var one in raw as JsonList ?? Array.Empty<object?>())
        {
            if (one is not JsonMap map)
            {
                continue;
            }

            if (Values.MaybeNumber(map, "at") is not { } at || Values.Text(map, "name") is not { } name)
            {
                problems.Add("A mark with no time or no name was left out.");
                continue;
            }

            marks.Add(new Mark(at, name, map.GetValueOrDefault("payload")));
        }

        return marks;
    }
}

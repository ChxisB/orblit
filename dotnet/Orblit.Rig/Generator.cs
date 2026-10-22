using System.Diagnostics.CodeAnalysis;

namespace Orblit.Rig;

/// <summary>
/// A skeleton, marked up with what each part should become. The thing an artist actually builds: a
/// plain armature roughly the shape of the character, with a note on each chain saying this is an
/// arm. Everything else is generated from it, which means a rig can be thrown away and rebuilt after
/// the skeleton changes rather than being repaired by hand.
/// </summary>
public sealed class MetaRig(Armature armature)
{
    private readonly Dictionary<string, IRigType> _assignments = [];

    public Armature Armature { get; } = armature;

    /// <summary>Marks the chain starting at <paramref name="bone"/> as something.</summary>
    public void Assign(string bone, IRigType type) => _assignments[bone] = type;

    public IRigType? TypeOf(string bone) => _assignments.GetValueOrDefault(bone);

    /// <summary>Assignments in a stable order, so generating twice produces the same rig.</summary>
    public List<KeyValuePair<string, IRigType>> Assignments =>
        [.. _assignments.OrderBy(entry => entry.Key, StringComparer.Ordinal)];

    public bool IsEmpty => _assignments.Count == 0;
}

/// <summary>
/// Something wrong with a meta-rig, reported rather than thrown. Collected rather than raised on the
/// first one, because an artist fixing a rig wants the whole list. Finding one problem per generate
/// is how a five minute fix becomes an afternoon.
/// </summary>
public sealed class RigProblem(string bone, string message)
{
    public string Bone { get; } = bone;

    public string Message { get; } = message;

    public override string ToString() => $"{Bone}: {Message}";
}

/// <summary>A rig, generated.</summary>
// F2: eight parameters, called by name.
public sealed class GeneratedRig(
    Armature armature,
    Pose pose,
    IReadOnlyCollection<string> controls,
    IReadOnlyDictionary<string, double> properties,
    IReadOnlyDictionary<string, BoneWidget> widgets,
    BoneCollections collections,
    IReadOnlyCollection<string> unassigned,
    IReadOnlyList<RigProblem> problems)
{
    public Armature Armature { get; } = armature;

    public Pose Pose { get; } = pose;

    /// <summary>The bones an animator is meant to touch.</summary>
    public IReadOnlyCollection<string> Controls { get; } = controls;

    /// <summary>Named values with the number each starts at.</summary>
    public IReadOnlyDictionary<string, double> Properties { get; } = properties;

    /// <summary>
    /// The shape drawn for each control. A control with no entry is drawn as a plain bone, which is a
    /// legible default rather than an omission.
    /// </summary>
    public IReadOnlyDictionary<string, BoneWidget> Widgets { get; } = widgets;

    /// <summary>Which bones show and hide together.</summary>
    public BoneCollections Collections { get; } = collections;

    /// <summary>
    /// Bones in the meta-rig that no rig type claimed. Reported rather than dropped silently: a bone
    /// nobody claimed is almost always a chain somebody forgot to mark, and it is invisible in the
    /// result precisely when it matters.
    /// </summary>
    public IReadOnlyCollection<string> Unassigned { get; } = unassigned;

    public IReadOnlyList<RigProblem> Problems { get; } = problems;

    public bool HasProblems => Problems.Count > 0;

    public IEnumerable<string> DeformBones => Armature.DeformingBones.Select(bone => bone.Name);
}

/// <summary>Turns a marked-up skeleton into a working rig.</summary>
public sealed class RigGenerator
{
    private static readonly Dictionary<BoneRole, string> Hidden = new()
    {
        [BoneRole.Original] = "ORG",
        [BoneRole.Deform] = "DEF",
        [BoneRole.Mechanism] = "MCH",
    };

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "A generator is an object a caller holds, so it stays an instance method.")]
    public GeneratedRig Generate(MetaRig meta)
    {
        var output = new Armature();
        var pose = new Pose(output);
        var context = new RigContext(meta.Armature, output, pose);

        var problems = new List<RigProblem>();
        var claimed = new HashSet<string>();

        foreach (var (start, type) in meta.Assignments)
        {
            if (!meta.Armature.Contains(start))
            {
                problems.Add(new RigProblem(start, $"assigned {type.Id} but there is no such bone"));
                continue;
            }

            var chain = ChainFrom(meta, start);
            if (chain.Count < type.MinimumBones)
            {
                problems.Add(new RigProblem(
                    start,
                    $"{type.Id} needs at least {type.MinimumBones} connected bones, and this chain has {chain.Count}"));
                continue;
            }

            claimed.UnionWith(chain);
            type.Generate(context, chain);
        }

        var unassigned = new OrderedDictionary<string, bool>();
        foreach (var bone in meta.Armature.Bones.Where(bone => !claimed.Contains(bone.Name)))
        {
            unassigned[bone.Name] = true;
        }

        // Appended after the generator's own, so a chain that could not be built at all is reported
        // before the details of one that half-was.
        problems.AddRange(context.Problems.Select(p => new RigProblem(p.Bone, p.Message)));

        CollectMachinery(context);

        return new GeneratedRig(
            output,
            pose,
            context.Controls,
            context.Defaults,
            context.Widgets,
            context.Collections,
            unassigned.Keys,
            problems);
    }

    /// <summary>
    /// Files everything that is not a control by its role, hidden. Done centrally rather than by
    /// each rig type, because the rule holds for every rig type there will ever be: an original, a
    /// deform bone and a mechanism are not an animator's to touch, and a rig that opens showing all
    /// three reads as broken.
    /// </summary>
    private static void CollectMachinery(RigContext context)
    {
        foreach (var bone in context.Output.Bones)
        {
            if (Hidden.GetValueOrDefault(BoneNaming.RoleOf(bone.Name)) is { } collection)
            {
                context.Collect(bone.Name, collection, visible: false);
            }
        }
    }

    /// <summary>
    /// The run of bones one rig type owns. Follows connected children while there is exactly one,
    /// and stops at anything that has been marked as something else. That is what lets a hand be
    /// assigned separately from the arm it hangs off without either one having to name the other.
    /// </summary>
    private static List<string> ChainFrom(MetaRig meta, string start)
    {
        var chain = new List<string> { start };
        var seen = new HashSet<string> { start };

        var current = start;
        while (true)
        {
            var children = meta.Armature.ChildrenOf(current).Where(bone => bone.Connected).ToList();

            if (children.Count != 1)
            {
                break;
            }

            var next = children[0].Name;
            // A chain that loops would run forever. The armature refuses one, but a generator that
            // trusted it would be a worse place to find out.
            if (!seen.Add(next) || meta.TypeOf(next) is not null)
            {
                break;
            }

            chain.Add(next);
            current = next;
        }

        return chain;
    }
}

using System.Collections.ObjectModel;

namespace Orblit.Motion;

/// <summary>How much an overlay may move each bone of one target.</summary>
public sealed class BoneMask
{
    /// <exception cref="ArgumentException">When a weight is not between zero and one.</exception>
    public BoneMask(IReadOnlyDictionary<string, double> weights, string target = "")
    {
        foreach (var weight in weights.Values)
        {
            FrameLayers.CheckWeight(weight, nameof(weights));
        }

        Target = target;
        Weights = new ReadOnlyDictionary<string, double>(new OrderedDictionary<string, double>(weights));
    }

    public string Target { get; }

    public IReadOnlyDictionary<string, double> Weights { get; }

    /// <summary>
    /// Includes <paramref name="bone"/> and every bone hanging from it, each with the same
    /// <paramref name="weight"/>.
    /// </summary>
    /// <exception cref="ArgumentException">When the skeleton has no such bone.</exception>
    public static BoneMask Below(RestSkeleton skeleton, string bone, string target = "", double weight = 1)
    {
        var root = skeleton.IndexOf(bone) ?? throw new ArgumentException("Unknown bone.", nameof(bone));
        var included = new HashSet<int> { root };
        var weights = new Dictionary<string, double> { [skeleton.Names[root]] = weight };

        // Parents come before their children, so one pass finds every bone below the root.
        for (var i = root + 1; i < skeleton.Length; i++)
        {
            if (included.Contains(skeleton.Parents[i]))
            {
                included.Add(i);
                weights[skeleton.Names[i]] = weight;
            }
        }

        return new BoneMask(weights, target);
    }

    /// <summary>How much of <paramref name="bone"/> of <paramref name="target"/> this mask lets through.</summary>
    public double WeightOf(string target, string bone) => target == Target ? Weights.GetValueOrDefault(bone) : 0;
}

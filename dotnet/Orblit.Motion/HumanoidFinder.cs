using System.Text.RegularExpressions;

namespace Orblit.Motion;

/// <summary>Searches one skeleton for the parts of a person.</summary>
internal sealed partial class HumanoidFinder
{
    /// <summary>A foot and a head are within this share of the skeleton's height of the floor and the ceiling.</summary>
    private const double Near = 0.2;

    private static readonly HumanoidBone[] LeftLeg = [HumanoidBone.LeftUpperLeg, HumanoidBone.LeftLowerLeg, HumanoidBone.LeftFoot, HumanoidBone.LeftToes];
    private static readonly HumanoidBone[] RightLeg = [HumanoidBone.RightUpperLeg, HumanoidBone.RightLowerLeg, HumanoidBone.RightFoot, HumanoidBone.RightToes];
    private static readonly HumanoidBone[] LeftArm = [HumanoidBone.LeftShoulder, HumanoidBone.LeftUpperArm, HumanoidBone.LeftLowerArm, HumanoidBone.LeftHand];
    private static readonly HumanoidBone[] RightArm = [HumanoidBone.RightShoulder, HumanoidBone.RightUpperArm, HumanoidBone.RightLowerArm, HumanoidBone.RightHand];

    private readonly RestSkeleton _skeleton;
    private readonly Vector3 _left;
    private readonly List<int>[] _children;
    private readonly double[] _height;

    // The lowest and highest point of each bone and everything below it, and how many bones that is.
    private readonly double[] _low;
    private readonly double[] _high;
    private readonly int[] _size;
    private readonly double _floor;
    private readonly double _ceiling;

    public HumanoidFinder(RestSkeleton skeleton, Vector3 up, Vector3 forward)
    {
        _skeleton = skeleton;
        _left = up.Cross(forward);
        _children = [.. Enumerable.Range(0, skeleton.Length).Select(static _ => new List<int>())];
        for (var bone = 0; bone < skeleton.Length; bone++)
        {
            if (skeleton.Parents[bone] >= 0)
            {
                _children[skeleton.Parents[bone]].Add(bone);
            }
        }

        _height = [.. Enumerable.Range(0, skeleton.Length).Select(bone => skeleton.OriginOf(bone).Dot(up))];
        _low = [.. _height];
        _high = [.. _height];
        _size = [.. Enumerable.Repeat(1, skeleton.Length)];
        _floor = _height.Aggregate(Math.Min);
        _ceiling = _height.Aggregate(Math.Max);
    }

    public OrderedDictionary<HumanoidBone, string> Found { get; } = [];

    public List<string> Problems { get; } = [];

    private double Span => _ceiling - _floor;

    [GeneratedRegex("(end|top|tip|nub|site)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EndSite();

    public void Run()
    {
        for (var bone = _skeleton.Length - 1; bone > 0; bone--)
        {
            var parent = _skeleton.Parents[bone];
            if (parent < 0)
            {
                continue;
            }

            _low[parent] = Math.Min(_low[parent], _low[bone]);
            _high[parent] = Math.Max(_high[parent], _high[bone]);
            _size[parent] += _size[bone];
        }

        if (Span < 1e-6)
        {
            Problems.Add("The skeleton has no height, so no body was looked for.");
            return;
        }

        if (FindHips() is not { } hips)
        {
            Problems.Add("No bone has two legs below it and a spine above it, so the skeleton was not read as a person.");
            return;
        }

        Found[HumanoidBone.Hips] = _skeleton.Names[hips];
        Legs(hips);
        if (Spine(hips) is not { } top)
        {
            // Legs and no shoulders is a table or a quadruped. Placing its front legs as thighs would drive
            // them from the wrong part of a person.
            Found.Clear();
            Problems.Add("The skeleton was not read as a person.");
            return;
        }

        Head(top);
        Arms(top);
    }

    private bool IsLeg(int parent, int child) =>
        _low[child] <= _floor + (Near * Span) && _high[child] <= _height[parent] + (0.1 * Span);

    private bool ReachesCeiling(int bone) => _high[bone] >= _ceiling - (Near * Span);

    private int? FindHips()
    {
        int? legsOnly = null;
        for (var bone = 0; bone < _skeleton.Length; bone++)
        {
            var kids = _children[bone];
            if (kids.Count(kid => IsLeg(bone, kid)) < 2)
            {
                continue;
            }

            if (kids.Any(ReachesCeiling))
            {
                return bone;
            }

            legsOnly ??= bone;
        }

        return legsOnly;
    }

    /// <summary>
    /// <paramref name="start"/> and every bone below it while each has exactly one child. An end site, a bone
    /// that only marks where the last one stops, is left off.
    /// </summary>
    private List<int> Chain(int start)
    {
        var chain = new List<int> { start };
        while (_children[chain[^1]].Count == 1)
        {
            chain.Add(_children[chain[^1]][0]);
        }

        if (chain.Count > 1 && EndSite().IsMatch(_skeleton.Names[chain[^1]]))
        {
            chain.RemoveAt(chain.Count - 1);
        }

        return chain;
    }

    /// <summary>
    /// Which of two limbs is the left, going by the name of each and, where a name says nothing, by which side
    /// of the body it is on.
    /// </summary>
    private (int Left, int Right)? Sides(int a, int b, string what)
    {
        switch (BoneNames.CleanName(_skeleton.Names[a]).Side, BoneNames.CleanName(_skeleton.Names[b]).Side)
        {
            case ("L", "R"):
                return (a, b);
            case ("R", "L"):
                return (b, a);
        }

        var across = _skeleton.OriginOf(a).Dot(_left) - _skeleton.OriginOf(b).Dot(_left);
        if (Math.Abs(across) < 1e-6 * Span)
        {
            Problems.Add($"The two {what} start on the same side of the body, so which is left was not worked out.");
            return null;
        }

        return across > 0 ? (a, b) : (b, a);
    }

    private void Legs(int hips)
    {
        var legs = _children[hips].Where(kid => IsLeg(hips, kid)).ToList();
        if (legs.Count != 2)
        {
            Problems.Add($"The hips have {legs.Count} legs. Two were expected, so none were placed.");
            return;
        }

        if (Sides(legs[0], legs[1], "legs") is not var (left, right))
        {
            return;
        }

        Leg(Chain(left), "left", LeftLeg);
        Leg(Chain(right), "right", RightLeg);
    }

    private void Leg(List<int> chain, string side, HumanoidBone[] slots)
    {
        if (chain.Count > slots.Length)
        {
            Problems.Add($"The {side} leg has {chain.Count} bones in a line. A leg has slots for {slots.Length}, so it was not placed.");
            return;
        }

        Place(chain, slots);
    }

    private void Place(List<int> chain, HumanoidBone[] slots)
    {
        for (var i = 0; i < chain.Count; i++)
        {
            Found[slots[i]] = _skeleton.Names[chain[i]];
        }
    }

    /// <summary>The child that goes highest.</summary>
    private int? UpChild(int bone)
    {
        int? best = null;
        foreach (var kid in _children[bone])
        {
            if (best is not { } held || _high[kid] > _high[held])
            {
                best = kid;
            }
        }

        return best;
    }

    /// <summary>
    /// The children of <paramref name="bone"/> that look like arms: not the way up, and with enough bones below
    /// to be a shoulder, an elbow and a hand.
    /// </summary>
    private List<int> ArmsOf(int bone)
    {
        var up = UpChild(bone);
        return [.. _children[bone].Where(kid => kid != up && _size[kid] >= 3)];
    }

    /// <summary>
    /// Places the spine and answers its top bone, the one the arms and neck hang from, or null when there is
    /// none to be found.
    /// </summary>
    private int? Spine(int hips)
    {
        int? first = null;
        foreach (var kid in _children[hips].Where(ReachesCeiling))
        {
            if (first is not { } held || _high[kid] > _high[held])
            {
                first = kid;
            }
        }

        if (first is not { } start)
        {
            Problems.Add("Nothing above the hips reaches the head, so no spine.");
            return null;
        }

        var chain = new List<int> { start };
        while (ArmsOf(chain[^1]).Count < 2)
        {
            if (UpChild(chain[^1]) is not { } next)
            {
                Problems.Add("The spine runs out before any arms hang from it.");
                return null;
            }

            chain.Add(next);
        }

        SpineSlots(chain);
        return chain[^1];
    }

    private void SpineSlots(List<int> chain)
    {
        Found[HumanoidBone.Spine] = _skeleton.Names[chain[0]];
        if (chain.Count == 1)
        {
            return;
        }

        var upper = chain.Count >= 3;
        Found[HumanoidBone.Chest] = _skeleton.Names[chain[upper ? chain.Count - 2 : 1]];
        if (upper)
        {
            Found[HumanoidBone.UpperChest] = _skeleton.Names[chain[^1]];
        }
    }

    private void Head(int top)
    {
        if (UpChild(top) is not { } up)
        {
            return;
        }

        var chain = Chain(up);
        if (chain.Count == 1)
        {
            Found[HumanoidBone.Head] = _skeleton.Names[chain[0]];
            return;
        }

        Found[HumanoidBone.Neck] = _skeleton.Names[chain[0]];
        Found[HumanoidBone.Head] = _skeleton.Names[chain[^1]];
    }

    private void Arms(int top)
    {
        var arms = ArmsOf(top);
        if (arms.Count != 2)
        {
            Problems.Add($"The top of the spine has {arms.Count} arms. Two were expected, so none were placed.");
            return;
        }

        if (Sides(arms[0], arms[1], "arms") is not var (left, right))
        {
            return;
        }

        Arm(Chain(left), "left", LeftArm);
        Arm(Chain(right), "right", RightArm);
    }

    /// <summary>
    /// <paramref name="slots"/> are shoulder, upper arm, lower arm, hand. A chain of three has no shoulder, and
    /// a chain of four starts with one.
    /// </summary>
    private void Arm(List<int> chain, string side, HumanoidBone[] slots)
    {
        if (chain.Count is < 3 or > 4)
        {
            Problems.Add($"The {side} arm has {chain.Count} bones in a line. Three or four were expected, so it was not placed.");
            return;
        }

        Place(chain, chain.Count == 4 ? slots : slots[1..]);
    }
}

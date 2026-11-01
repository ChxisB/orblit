using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>The cloud <see cref="SplatClouds.Garden"/> describes, grown one part at a time.</summary>
/// <remarks>
/// Every part draws from one seeded <see cref="Random"/>, so the order of the
/// calls in <see cref="Grown"/> is part of the cloud.
/// </remarks>
internal sealed class Garden
{
    private const double Floor = -0.44;

    // Where the light came from, on the day this was not photographed.
    private static readonly Vector3 Sun = new Vector3(-0.36, 0.86, 0.36).Normalized();

    private static readonly Vector3[] Petals =
    [
        new(0.95, 0.72, 0.24),
        new(0.92, 0.36, 0.30),
        new(0.88, 0.84, 0.72),
        new(0.62, 0.46, 0.86),
    ];

    private readonly Random _random = new(3);
    private readonly int _count;
    private readonly Records _records;
    private readonly List<Limb> _limbs = [];
    private readonly List<(Vector3 Centre, double Radius)> _bunches = [];

    public Garden(int count)
    {
        _count = count;
        _records = new Records(count);
    }

    private static Vector3 Up => new(0, 1, 0);

    public byte[] Grown()
    {
        // Four rounds of splitting off a trunk half again as long as the first
        // branch: a clear stem with a crown over it, rather than the low bundle
        // that fewer, longer, floppier limbs come out as.
        Grow(new Vector3(0.05, Floor, 0.1), new Vector3(-0.03, 1, -0.02).Normalized(), (1.5, 0.17), 4);

        // Most splats go on the leaves, because that is where a real capture
        // spends them too: a surface needs as many splats as it has detail, and
        // a canopy is nothing but detail.
        var onGround = Share(0.32);
        var onBark = Share(0.11);
        var onBushes = Share(0.08);
        var onFlowers = Share(0.015);
        Ground(onGround);
        Bark(onBark);
        Leaves(_count - onGround - onBark - onBushes - onFlowers);
        Bushes(onBushes);
        Flowers(onFlowers);
        return _records.Pack();
    }

    private int Share(double part) => (int)Math.Round(_count * part, MidpointRounding.AwayFromZero);

    /// <summary>
    /// The skeleton the bark and leaves are scattered over. Each limb is three
    /// quarters the length of the one it came from, and a bunch of leaves is
    /// left wherever the growing stops.
    /// </summary>
    private void Grow(Vector3 from, Vector3 way, (double Length, double Radius) size, int depth)
    {
        var to = from + (way * size.Length);
        _limbs.Add(new Limb(from, to, size.Radius));
        if (depth == 0)
        {
            _bunches.Add((to + (way * 0.14), 0.20 + (_random.NextDouble() * 0.14)));
            return;
        }

        for (var i = 0; i < (depth > 1 ? 3 : 2); i++)
        {
            // A trunk divides narrowly and twigs splay, which is the
            // difference between a tree and a bush.
            var lean = depth > 2 ? 0.20 + (_random.NextDouble() * 0.22) : 0.40 + (_random.NextDouble() * 0.45);
            var turn = _random.NextDouble() * 2 * Math.PI;
            var (u, v) = Across(way, new Vector3(1, 0, 0));
            var round = (u * Math.Cos(turn)) + (v * Math.Sin(turn));
            var next = ((way * Math.Cos(lean)) + (round * Math.Sin(lean))).Normalized();

            // And everything reaches for the light, so nothing grows downwards.
            next.Y += 0.22;
            var length = size.Length * (0.70 + (_random.NextDouble() * 0.10));
            Grow(to, next.Normalized(), (length, size.Radius * 0.66), depth - 1);
        }
    }

    /// <summary>Flat discs for the earth and upright slivers for the grass over it.</summary>
    private void Ground(int share)
    {
        var placed = 0;
        while (placed < share)
        {
            var a = _random.NextDouble() * 2 * Math.PI;

            // Straight in the unit interval rather than its root, which would
            // spread them evenly over the area. A capture is densest where the
            // photographs were taken, and they were taken around the tree.
            var r = 7.5 * _random.NextDouble();
            var at = new Vector3(Math.Cos(a) * r, Floor, Math.Sin(a) * r);

            // Darker under the tree, where less of the sky reaches.
            var shadow = 1 - (0.48 * Math.Exp(-(r * r) / 2.2));
            var blade = _random.NextDouble() < 0.55;

            // Thinning out towards the rim rather than ending at it. A capture
            // gives out where the photographs did, and a disc of splats cut off
            // in a circle looks like nothing else.
            var edge = Math.Clamp(1 - ((r - 4.6) / 2.9), 0, 1);
            if (_random.NextDouble() > edge)
            {
                // Try again somewhere else rather than going one splat short:
                // the shares are exact, and the leaves are owed what is left.
                continue;
            }

            var dry = _random.NextDouble();
            var soil = (new Vector3(0.105, 0.215, 0.055) * (1 - dry)) + (new Vector3(0.300, 0.280, 0.090) * dry);
            Put(blade ? Blade(at, soil, shadow) : Earth(at, soil, shadow));
            placed++;
        }
    }

    private Splat Blade(Vector3 at, Vector3 soil, double shadow)
    {
        var way = new Vector3(_random.NextDouble() - 0.5, 0, _random.NextDouble() - 0.5).Normalized();
        var tall = 0.045 + (_random.NextDouble() * 0.055);
        var size = new Vector3(tall, 0.006 + (_random.NextDouble() * 0.005), 0.003);

        // A blade catches the sky along its length however it is turned, so it
        // is lit as though it faced up rather than sideways.
        var colour = Lit(soil * 1.25, Up, shadow);
        return new Splat(at + new Vector3(0, tall * 0.5, 0), way, Up, size, colour, 0.82 + (_random.NextDouble() * 0.16));
    }

    private Splat Earth(Vector3 at, Vector3 soil, double shadow)
    {
        var facing = (Up + (Unit() * 0.22)).Normalized();
        var along = Unit();
        var size = new Vector3(0.05 + (_random.NextDouble() * 0.05), 0.04 + (_random.NextDouble() * 0.04), 0.004);
        return new Splat(at, facing, along, size, Lit(soil * 0.85, facing, shadow), 0.88 + (_random.NextDouble() * 0.12));
    }

    /// <summary>Spread over the limbs by area, so a twig does not get what the trunk gets.</summary>
    private void Bark(int share)
    {
        var area = _limbs.Sum(limb => limb.Area);
        for (var i = 0; i < share; i++)
        {
            var (from, to, radius) = Pick(_random.NextDouble() * area);
            var way = (to - from).Normalized();
            var (u, v) = Across(way, new Vector3(1, 0, 0));
            var t = _random.NextDouble();
            var turn = _random.NextDouble() * 2 * Math.PI;
            var outward = ((u * Math.Cos(turn)) + (v * Math.Sin(turn))).Normalized();

            // Narrower towards the tip, and roughened, so the trunk has a grain
            // rather than a polish.
            var r = radius * (1 - (0.35 * t)) * (0.92 + (_random.NextDouble() * 0.16));
            var at = from + ((to - from) * t) + (outward * r);
            var dark = 0.72 + (_random.NextDouble() * 0.28);
            var size = new Vector3(0.030 + (_random.NextDouble() * 0.030), 0.012 + (_random.NextDouble() * 0.010), 0.004);
            var colour = Lit(new Vector3(0.265, 0.180, 0.115) * dark, outward, 0.95);
            Put(new Splat(at, outward, way, size, colour, 0.90 + (_random.NextDouble() * 0.10)));
        }
    }

    /// <summary>The limb <paramref name="along"/> falls on, with the limbs laid end to end by area.</summary>
    private Limb Pick(double along)
    {
        foreach (var limb in _limbs)
        {
            along -= limb.Area;
            if (along <= 0)
            {
                return limb;
            }
        }

        return _limbs[0];
    }

    /// <summary>
    /// A shell rather than a ball: the inside of a bunch of leaves is not seen,
    /// and its edge is the part that has to hold up against the sky.
    /// </summary>
    private void Leaves(int share)
    {
        for (var i = 0; i < share; i++)
        {
            var (centre, radius) = _bunches[_random.Next(_bunches.Count)];
            var way = Unit();
            var deep = 0.58 + (0.42 * Math.Pow(_random.NextDouble(), 0.33));
            var at = centre + (way * (radius * deep)) + (Unit() * 0.035);

            // Leaves face every way, but a little more outwards than not.
            var facing = ((way * 0.55) + Unit()).Normalized();
            var autumn = _random.NextDouble();
            var green = new Vector3(0.075, 0.215, 0.045) + (new Vector3(0.130, 0.185, 0.050) * _random.NextDouble());
            var leaf = autumn > 0.94 ? new Vector3(0.330, 0.160, 0.040) : green;

            // Deeper in the bunch is darker, which is the only shadow a cloud
            // with no lighting gets, and the one that makes it read as thick.
            var colour = Lit(leaf, facing, 0.30 + (0.80 * deep));
            var along = Unit();
            var size = new Vector3(0.028 + (_random.NextDouble() * 0.024), 0.020 + (_random.NextDouble() * 0.018), 0.004);
            Put(new Splat(at, facing, along, size, colour, 0.55 + (_random.NextDouble() * 0.35)));
        }
    }

    private void Bushes(int share)
    {
        var bushes = new (Vector3 Foot, double Radius)[5];
        for (var i = 0; i < bushes.Length; i++)
        {
            var foot = new Vector3(((_random.NextDouble() * 2) - 1) * 2.6, Floor, ((_random.NextDouble() * 2) - 1) * 2.6);
            bushes[i] = (foot, 0.22 + (_random.NextDouble() * 0.20));
        }

        for (var i = 0; i < share; i++)
        {
            var (foot, radius) = bushes[_random.Next(bushes.Length)];

            // Only the top half of the shell: the underside of a bush is on the
            // ground and nothing sees it.
            var way = Unit();
            way.Y = Math.Abs(way.Y);
            var deep = 0.55 + (0.45 * Math.Pow(_random.NextDouble(), 0.33));
            var at = foot + new Vector3(0, radius * 0.75, 0) + (way * (radius * deep));
            var facing = ((way * 0.6) + Unit()).Normalized();
            var along = Unit();
            var size = new Vector3(0.026 + (_random.NextDouble() * 0.022), 0.018 + (_random.NextDouble() * 0.016), 0.004);
            var leaf = new Vector3(0.060, 0.155, 0.048) + (new Vector3(0.095, 0.135, 0.030) * _random.NextDouble());
            Put(new Splat(at, facing, along, size, Lit(leaf, facing, 0.34 + (0.66 * deep)), 0.62 + (_random.NextDouble() * 0.30)));
        }
    }

    /// <summary>A small share of the cloud, and the first thing anybody's eye goes to.</summary>
    private void Flowers(int share)
    {
        var heads = new Vector3[40];
        for (var i = 0; i < heads.Length; i++)
        {
            heads[i] = new Vector3(
                ((_random.NextDouble() * 2) - 1) * 3.0,
                Floor + 0.06 + (_random.NextDouble() * 0.09),
                ((_random.NextDouble() * 2) - 1) * 3.0);
        }

        for (var i = 0; i < share; i++)
        {
            var head = heads[_random.Next(heads.Length)];
            var facing = (Up + (Unit() * 0.5)).Normalized();
            var at = head + (Unit() * 0.028);
            var along = Unit();
            var size = new Vector3(0.014 + (_random.NextDouble() * 0.012), 0.012 + (_random.NextDouble() * 0.010), 0.004);
            var colour = Petals[_random.Next(Petals.Length)] * (0.55 + (0.45 * _random.NextDouble()));
            Put(new Splat(at, facing, along, size, colour, 0.88 + (_random.NextDouble() * 0.12)));
        }
    }

    private Vector3 Unit()
    {
        var z = (_random.NextDouble() * 2) - 1;
        var a = _random.NextDouble() * 2 * Math.PI;
        var r = Math.Sqrt(Math.Max(0, 1 - (z * z)));
        return new Vector3(r * Math.Cos(a), z, r * Math.Sin(a));
    }

    /// <summary>Two directions across <paramref name="n"/>, for a splat's own axes to lie along.</summary>
    private static (Vector3 U, Vector3 V) Across(Vector3 n, Vector3 hint)
    {
        var u = hint - (n * hint.Dot(n));
        if (u.Length2 < 1e-9)
        {
            u = (Math.Abs(n.Y) < 0.9 ? new Vector3(0, 1, 0) : new Vector3(1, 0, 0)).Cross(n);
        }

        u = u.Normalized();
        return (u, n.Cross(u).Normalized());
    }

    /// <summary><paramref name="colour"/> under the sun, facing <paramref name="facing"/>, with <paramref name="shade"/> of the sky reaching it.</summary>
    /// <remarks>
    /// A key from the sun, a fill from the sky that favours whatever faces up,
    /// and a floor under both so that nothing in shadow goes to black. That is
    /// what the bounce off a bright ground does outdoors.
    /// </remarks>
    private static Vector3 Lit(Vector3 colour, Vector3 facing, double shade)
    {
        var key = Math.Max(0, facing.Dot(Sun)) * 1.45;
        var sky = (0.5 + (0.5 * facing.Y)) * 0.55;
        return colour * (((key + sky) * shade) + 0.22);
    }

    private void Put(Splat splat)
    {
        var (u, v) = Across(splat.Facing, splat.Along);
        var frame = SplatClouds.Columns(u, v, splat.Facing);
        _records.Put(new Disc(splat.At, frame, splat.Size, splat.Colour, splat.Alpha));
    }

    private readonly record struct Limb(Vector3 From, Vector3 To, double Radius)
    {
        public double Area => (To - From).Length * Radius;
    }

    /// <summary>A disc at <paramref name="At"/> facing <paramref name="Facing"/>, stretched along <paramref name="Along"/>.</summary>
    private sealed record Splat(Vector3 At, Vector3 Facing, Vector3 Along, Vector3 Size, Vector3 Colour, double Alpha);
}

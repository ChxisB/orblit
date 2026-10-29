using Orblit.Filament;
using Orblit.VectorMath;

namespace Orblit.Examples;

// The world laid in chunks as the runner reaches them, and taken up again
// behind.
public sealed partial class RunnerExample
{
    private void Reset()
    {
        _runs++;
        // A different run every time, and the same runs in the same order every
        // time the game is opened. That is what lets a test say anything.
        _chance = new Random(_runs * 7919 + 1);
        _along = _prevAlong = _ran = 0;
        _origin = 0;
        _runTime = 0;
        _speed = StartSpeed;
        _slow = 1;
        _lane = 1;
        _x = _prevX = 0;
        _y = _vy = 0;
        _slideLeft = 0;
        _slideQueued = false;
        _jumpBuffer = 0;
        _stumbledAt = -100;
        _spin = 0;
        Coins = 0;
        _bonus = 0;
        _streak = 0;
        _lastCoinAt = -100;
        _hazards.Clear();
        _road.Clear();
        _nextRow = 60;
        _readySince = _clock;
        foreach (var spark in _sparks)
        {
            spark.Life = 0;
        }

        Plan();
    }

    private double Z(double along) => -(along - _origin);

    /// <summary>Makes the files and hands them to the renderer, once.</summary>
    private void Prepare()
    {
        if (_requested)
        {
            return;
        }

        _requested = true;
        try
        {
            foreach (var (name, bytes) in RunnerArt.Build())
            {
                _provide(name, bytes);
            }

            _artReady = true;
        }
        catch (Exception error) when (error is RendererException or DllNotFoundException)
        {
            Note = $"The runner's models could not be handed to the renderer: {error.Message}";
        }
    }

    private void LaySceneryAround()
    {
        var first = (int)Math.Floor(_along / Chunk) - Behind;
        for (var chunk = first; chunk < first + Chunks; chunk++)
        {
            var slot = (chunk % Chunks + Chunks) % Chunks;
            if (_slotChunk[slot] != chunk || _slotOrigin[slot] != _origin)
            {
                Lay(slot, chunk);
            }
        }
    }

    /// <summary>
    /// Fills <paramref name="slot"/> with the scenery of <paramref name="chunk"/>:
    /// the same trees and houses in the same places every time that stretch of
    /// road comes round.
    /// </summary>
    private void Lay(int slot, int chunk)
    {
        _slotChunk[slot] = chunk;
        _slotOrigin[slot] = _origin;
        _props[slot].Clear();
        var plot = new Plot(slot, new Random(chunk * 92821 + 7), chunk * Chunk, _props[slot]);
        LayHouses(plot);
        LayBillboard(plot);
        LayTrees(plot);
        LayVerge(plot);
        LayHill(plot);
        LayGrass(slot, plot.Chance, plot.Start);
    }

    /// <summary>A house now and then, facing the road, and sometimes a dark one across from it.</summary>
    private void LayHouses(Plot plot)
    {
        var houseSide = plot.Side();
        if (plot.Chance.NextDouble() < 0.6)
        {
            var x = houseSide * (13 + plot.Chance.NextDouble() * 6);
            var along = plot.Start + 8 + plot.Chance.NextDouble() * 24;
            plot.Put(2, RunnerArt.Place((x, 0, Z(along)), (1, 1, 1), yaw: houseSide * Math.PI / 2));
            plot.Take(x, along, 5);
        }

        if (plot.Chance.NextDouble() < 0.3)
        {
            var x = -houseSide * (13 + plot.Chance.NextDouble() * 6);
            var along = plot.Start + 8 + plot.Chance.NextDouble() * 24;
            plot.Put(3, RunnerArt.Place((x, 0, Z(along)), (1, 1, 1), yaw: -houseSide * Math.PI / 2));
            plot.Take(x, along, 5);
        }
    }

    /// <summary>A billboard, turned a little towards whoever is coming.</summary>
    private void LayBillboard(Plot plot)
    {
        if (plot.Chance.NextDouble() >= 0.3)
        {
            return;
        }

        var s = plot.Side();
        var x = s * 9.5;
        var along = plot.Start + plot.Chance.NextDouble() * Chunk;
        if (plot.Clear(x, along, 3))
        {
            plot.Put(7, RunnerArt.Place((x, 0, Z(along)), (1, 1, 1), yaw: -s * 0.35));
            plot.Take(x, along, 3);
        }
    }

    /// <summary>Trees, thicker near the road where they are seen going past.</summary>
    private void LayTrees(Plot plot)
    {
        var chance = plot.Chance;
        for (var i = 0; i < 16; i++)
        {
            var pine = chance.NextDouble() < 0.55;
            var outward = chance.NextDouble();
            var x = plot.Side() * (7.5 + outward * outward * 37.5);
            var along = plot.Start + chance.NextDouble() * Chunk;
            var size = 0.8 + chance.NextDouble() * 0.6;
            var yaw = chance.NextDouble() * Math.PI * 2;
            if (!plot.Clear(x, along, 2))
            {
                continue;
            }

            plot.Put(pine ? 0 : 1, RunnerArt.Place((x, 0, Z(along)), (size, size, size), yaw));
            plot.Take(x, along, 1.5);
        }
    }

    /// <summary>Bushes and rocks on the verge.</summary>
    private void LayVerge(Plot plot)
    {
        var chance = plot.Chance;
        for (var i = 0; i < 7; i++)
        {
            var kind = i < 3 ? 4 : i < 4 ? 5 : 6;
            var x = plot.Side() * (5.4 + chance.NextDouble() * 2.1);
            var along = plot.Start + chance.NextDouble() * Chunk;
            var size = kind == 6 ? 0.35 + chance.NextDouble() * 0.5 : 0.7 + chance.NextDouble() * 0.5;
            var yaw = chance.NextDouble() * Math.PI * 2;
            if (chance.NextDouble() < 0.35 || !plot.Clear(x, along, 0.8))
            {
                continue;
            }

            plot.Put(kind, RunnerArt.Place((x, -0.03, Z(along)), (size, size, size), yaw));
            plot.Take(x, along, size);
        }
    }

    /// <summary>A hill in the distance.</summary>
    private void LayHill(Plot plot)
    {
        if (plot.Chance.NextDouble() >= 0.7)
        {
            return;
        }

        var x = plot.Side() * (90 + plot.Chance.NextDouble() * 80);
        var along = plot.Start + plot.Chance.NextDouble() * Chunk;
        var size = 35 + plot.Chance.NextDouble() * 35;
        plot.Put(8, RunnerArt.Place((x, -0.05, Z(along)), (size, size, size)));
    }

    /// <summary>
    /// Tufts of grass along both sides of the road: blades in clumps, each a
    /// thin box leaning its own way.
    /// </summary>
    private void LayGrass(int slot, Random chance, double start)
    {
        var at = 0;
        while (at < BladesPerChunk)
        {
            var s = chance.Next(2) == 0 ? 1.0 : -1.0;
            var outward = chance.NextDouble();
            var x = s * (4.8 + outward * outward * 24);
            var along = start + chance.NextDouble() * Chunk;
            var shade = chance.NextDouble();
            var count = Math.Min(4 + chance.Next(4), BladesPerChunk - at);
            for (var i = 0; i < count; i++, at++)
            {
                Blade(slot, at, chance, (s, x, along, shade));
            }
        }

        _grassBounds[slot] = new Bounds(new Vector3(-30, -0.1, Z(start + Chunk) - 1), new Vector3(30, 0.6, Z(start) + 1));
        _grassRevision[slot]++;
    }

    /// <summary>Writes blade <paramref name="at"/> of a clump into <paramref name="slot"/>.</summary>
    private void Blade(int slot, int at, Random chance, (double Side, double X, double Along, double Shade) clump)
    {
        var height = 0.2 + chance.NextDouble() * 0.28;
        var bx = clump.X + (chance.NextDouble() - 0.5) * 0.4;
        var blade = Matrix4.Identity();
        blade.TranslateByDouble(clump.Side * Math.Max(4.6, Math.Abs(bx)), -0.03, Z(clump.Along + (chance.NextDouble() - 0.5) * 0.4), 1);
        blade.RotateY(chance.NextDouble() * Math.PI);
        blade.RotateZ((chance.NextDouble() - 0.5) * 0.7);
        blade.TranslateByDouble(0, height / 2, 0, 1);
        blade.ScaleByDouble(0.045, height, 0.045, 1);
        for (var k = 0; k < 16; k++)
        {
            _grass[slot][at * 16 + k] = (float)blade[k];
        }

        // From a deep green to a sunlit yellow green, by clump rather than by
        // blade, so a tuft reads as one plant.
        var tone = clump.Shade * 0.8 + chance.NextDouble() * 0.2;
        _grassColours[slot][at * 3] = (float)(0.16 + tone * 0.2);
        _grassColours[slot][at * 3 + 1] = (float)(0.42 + tone * 0.2);
        _grassColours[slot][at * 3 + 2] = (float)(0.02 + tone * 0.03);
    }

    /// <summary>One chunk being laid: its draws, where it starts, and what is already standing in it.</summary>
    private sealed class Plot(int slot, Random chance, double start, List<RenderObject> props)
    {
        private readonly int[] _counts = new int[Caps.Length];
        private readonly List<(double X, double Along, double Room)> _taken = [];

        public Random Chance { get; } = chance;

        public double Start { get; } = start;

        /// <summary>Adds one of scenery <paramref name="type"/>, unless the chunk has its share of them.</summary>
        public void Put(int type, Matrix4 transform)
        {
            if (_counts[type] >= Caps[type])
            {
                return;
            }

            props.Add(new RenderObject
            {
                Key = 1000 + type * 100 + slot * Caps[type] + _counts[type]++,
                Mesh = SceneryMeshes[type],
                Transform = transform,
                Colour = White,
                // Hills are too big and too far to shadow anything worth
                // seeing, and the shadow map has better things to spend its
                // texels on.
                CastShadows = type != 8,
            });
        }

        /// <summary>Whether <paramref name="room"/> around a point is free of everything taken so far.</summary>
        public bool Clear(double x, double along, double room) => _taken.All(taken =>
        {
            var dx = x - taken.X;
            var da = along - taken.Along;
            return dx * dx + da * da >= (taken.Room + room) * (taken.Room + room);
        });

        public void Take(double x, double along, double room) => _taken.Add((x, along, room));

        public double Side() => Chance.Next(2) == 0 ? 1.0 : -1.0;
    }
}

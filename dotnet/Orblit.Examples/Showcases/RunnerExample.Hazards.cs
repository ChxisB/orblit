namespace Orblit.Examples;

// What the road ahead is made of. Rows of hazards and lines of coins are
// planned a chunk at a time, ahead of where the runner is.
public sealed partial class RunnerExample
{
    private enum Kind
    {
        Hurdle,
        Bar,
        Container,
    }

    /// <summary>
    /// Lays rows of obstacles and coins until the road is full to the edge of
    /// the world.
    /// </summary>
    /// <remarks>
    /// A row is made as it comes into view rather than all at the start. How
    /// far apart rows should be depends on how fast the runner is going when it
    /// gets to them. And a run that never ends cannot be laid out in advance.
    /// </remarks>
    private void Plan()
    {
        while (_nextRow < _along + Ahead)
        {
            Row();
        }
    }

    private void Row()
    {
        var at = _nextRow;
        var difficulty = Math.Clamp((at - 60) / 1500, 0.0, 1.0);
        var speed = Math.Max(_speed, StartSpeed);
        var roll = _chance.NextDouble();
        var length = roll switch
        {
            < 0.12 => Breather(at),
            < 0.45 => One(at, speed),
            < 0.72 => Two(at),
            _ when roll < 0.72 + (difficulty > 0.3 ? 0.1 + difficulty * 0.15 : 0) => Three(at),
            _ => Containers(at, difficulty),
        };

        var gap = speed * (0.9 + _chance.NextDouble() * 0.6 - 0.25 * difficulty);
        _nextRow = at + length + Math.Max(14.0, gap);
    }

    /// <summary>A breather: nothing but coins.</summary>
    private double Breather(double at) => CoinLine(_chance.Next(3), at, 5 + _chance.Next(4));

    /// <summary>One thing in one lane.</summary>
    private double One(double at, double speed)
    {
        var lane = _chance.Next(3);
        var kind = AnyKind();
        var length = Put(kind, lane, at);
        if (kind == Kind.Hurdle && _chance.Next(2) == 0)
        {
            CoinArc(lane, at, speed);
        }
        else
        {
            CoinLine((lane + 1 + _chance.Next(2)) % 3, at - 4, 6);
        }

        return length;
    }

    /// <summary>Two taken, one free, and the free one paid for.</summary>
    private double Two(double at)
    {
        var free = _chance.Next(3);
        var length = 0.0;
        for (var lane = 0; lane < 3; lane++)
        {
            if (lane != free)
            {
                length = Math.Max(length, Put(AnyKind(), lane, at));
            }
        }

        CoinLine(free, at - 5, 6);
        return length;
    }

    /// <summary>
    /// All three, and at least one of them something you can get over or under
    /// rather than round.
    /// </summary>
    private double Three(double at)
    {
        var open = _chance.Next(3);
        var length = 0.0;
        for (var lane = 0; lane < 3; lane++)
        {
            var kind = lane == open ? (_chance.Next(2) == 0 ? Kind.Hurdle : Kind.Bar) : AnyKind();
            length = Math.Max(length, Put(kind, lane, at));
        }

        return length;
    }

    /// <summary>A train of containers, in one lane or two.</summary>
    private double Containers(double at, double difficulty)
    {
        var lane = _chance.Next(3);
        var cars = difficulty > 0.2 && _chance.Next(2) == 0 ? 2 : 1;
        var length = Train(lane, at, cars);
        var free = (lane + 1 + _chance.Next(2)) % 3;
        if (_chance.NextDouble() < 0.3 + difficulty * 0.3)
        {
            var other = free;
            free = 3 - lane - other;
            var offset = _chance.NextDouble() * 8;
            length = Math.Max(length, offset + Train(other, at + offset, 1));
        }

        CoinLine(free, at, 1 + (int)Math.Floor(length / 2.5));
        return length;
    }

    private Kind AnyKind()
    {
        var pick = _chance.NextDouble();
        return pick < 0.4 ? Kind.Hurdle : pick < 0.7 ? Kind.Bar : Kind.Container;
    }

    /// <summary>Puts one <paramref name="kind"/> in <paramref name="lane"/> at <paramref name="at"/>, and says how far along it reaches.</summary>
    private double Put(Kind kind, int lane, double at)
    {
        if (kind == Kind.Container)
        {
            return Train(lane, at, 1);
        }

        _hazards.Add(new Hazard { Kind = kind, Lane = lane, Near = at - 0.1, Far = at + 0.1, Key = HazardKey(kind) });
        return 0.1;
    }

    private double Train(int lane, double at, int cars)
    {
        const double gap = 0.6;
        for (var car = 0; car < cars; car++)
        {
            var near = at + car * (RunnerArt.ContainerLength + gap);
            _hazards.Add(new Hazard
            {
                Kind = Kind.Container,
                Lane = lane,
                Near = near,
                Far = near + RunnerArt.ContainerLength,
                Key = HazardKey(Kind.Container),
            });
        }

        return cars * RunnerArt.ContainerLength + (cars - 1) * gap;
    }

    private int HazardKey(Kind kind) => 2000 + (int)kind * 200 + _hazardCount++ % 200;

    private double CoinLine(int lane, double from, int count)
    {
        for (var i = 0; i < count; i++)
        {
            _road.Add(new Coin(lane, from + i * 2.5, 0.9, 3000 + _coinCount++ % 600));
        }

        return (count - 1) * 2.5;
    }

    /// <summary>
    /// Five coins along the path of a jump that clears a hurdle at
    /// <paramref name="at"/>, so the coins are also the advice.
    /// </summary>
    private void CoinArc(int lane, double at, double speed)
    {
        const double time = 2 * JumpSpeed / Gravity;
        for (var i = 0; i < 5; i++)
        {
            var t = i / 4.0 * time;
            var along = at + (i / 4.0 - 0.5) * time * speed;
            var height = 0.9 + JumpSpeed * t - 0.5 * Gravity * t * t;
            _road.Add(new Coin(lane, along, height, 3000 + _coinCount++ % 600));
        }
    }

    /// <summary>Lets go of what is well behind.</summary>
    private void Forget()
    {
        _hazards.RemoveAll(hazard => hazard.Far < _along - 30);
        _road.RemoveAll(coin => coin.Along < _along - 30);
    }

    private sealed class Hazard
    {
        public required Kind Kind { get; init; }

        public required int Lane { get; init; }

        /// <summary>Where along the road it starts.</summary>
        public required double Near { get; init; }

        /// <summary>Where along the road it ends.</summary>
        public required double Far { get; init; }

        public required int Key { get; init; }

        /// <summary>Run through, with crashing turned off, and no longer in the way.</summary>
        public bool Struck { get; set; }

        public double GrazedAt { get; set; } = -100;
    }

    private sealed class Coin(int lane, double along, double height, int key)
    {
        public int Lane { get; } = lane;

        public double Along { get; } = along;

        public double Height { get; } = height;

        public int Key { get; } = key;

        public double? TakenAt { get; set; }
    }
}

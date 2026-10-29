using Orblit.VectorMath;

namespace Orblit.Examples;

// A run in progress: moving the runner, and what happens when it meets
// something.
public sealed partial class RunnerExample
{
    private void Run(double dt)
    {
        _runTime += dt;
        _intro = Math.Min(1, _intro + dt / 0.9);
        _slow = Math.Min(1, _slow + dt * 0.25);
        _speed = (TopSpeed - (TopSpeed - StartSpeed) * Math.Exp(-_runTime / 110)) * _slow;

        if (Autopilot)
        {
            Drive();
        }

        _prevAlong = _along;
        _prevX = _x;
        _along += _speed * dt;
        _ran = _along;
        _x += (Lanes[_lane] - _x) * (1 - Math.Exp(-dt * 14));

        if (!Grounded)
        {
            Fall(dt);
        }

        _jumpBuffer = Math.Max(0, _jumpBuffer - dt);
        _slideLeft = Math.Max(0, _slideLeft - dt);
        _step += dt * (8 + _speed * 0.5);

        Collide();
        if (Phase != RunnerPhase.Running)
        {
            return;
        }

        Collect();
        Plan();
        Forget();

        if (_along - _origin > Rebase)
        {
            _origin += Rebase;
            foreach (var spark in _sparks)
            {
                spark.At.Z += Rebase;
            }
        }
    }

    private void Fall(double dt)
    {
        _vy -= Gravity * dt;
        _y += _vy * dt;
        if (_y > 0)
        {
            return;
        }

        _y = 0;
        _impact = Math.Min(1, -_vy / 18);
        _vy = 0;
        _landedAt = _clock;
        if (_slideQueued)
        {
            _slideQueued = false;
            _slideLeft = SlideTime;
        }
        else if (_jumpBuffer > 0)
        {
            _vy = JumpSpeed;
        }
    }

    /// <summary>After a crash: thrown up, turned over, and brought to a stop on the ground.</summary>
    private void Tumble(double dt)
    {
        _speed *= Math.Exp(-dt * 6);
        _along += _speed * dt;
        _prevAlong = _along;
        if (!Grounded)
        {
            _vy -= Gravity * dt;
            _y = Math.Max(0, _y + _vy * dt);
            _spin += dt * 9;
            if (_y <= 0)
            {
                _vy = Math.Abs(_vy) > 3 ? -_vy * 0.3 : 0;
            }
        }
        else
        {
            // Settled onto whichever side it came down nearest.
            const double quarter = Math.PI / 2;
            var rest = Math.Round(_spin / quarter, MidpointRounding.AwayFromZero) * quarter;
            _spin += (rest - _spin) * (1 - Math.Exp(-dt * 10));
        }
    }

    private void Collide()
    {
        var back = _prevAlong - HalfDepth;
        var front = _along + HalfDepth;
        foreach (var hazard in _hazards)
        {
            if (hazard.Struck || hazard.Near > front || hazard.Far < back
                || Math.Abs(_x - Lanes[hazard.Lane]) >= Reach || !BlockedBy(hazard))
            {
                continue;
            }

            Meet(hazard);
            if (Phase != RunnerPhase.Running)
            {
                return;
            }
        }
    }

    private void Meet(Hazard hazard)
    {
        // Come at from the side is a stumble and a bounce back. It was already
        // alongside, and the runner was not in its lane. That is the genre's
        // forgiveness for a lane change a moment too early. Anything else is
        // running into it.
        var laneX = Lanes[hazard.Lane];
        var alongside = hazard.Near < _prevAlong + HalfDepth;
        var beside = Math.Abs(_prevX - laneX) >= Reach;
        if (alongside && beside)
        {
            if (_clock - hazard.GrazedAt < 0.6)
            {
                return;
            }

            hazard.GrazedAt = _clock;
            _lane = Math.Clamp(_x < laneX ? hazard.Lane - 1 : hazard.Lane + 1, 0, 2);
            Stumble(hazard);
        }
        else if (Invincible)
        {
            hazard.Struck = true;
            Stumble(hazard);
        }
        else
        {
            Crash(hazard);
        }
    }

    private bool BlockedBy(Hazard hazard) => hazard.Kind switch
    {
        Kind.Hurdle => _y < HurdleClear,
        Kind.Bar => _y + (IsSliding ? Sliding : Standing) > BarBottom,
        Kind.Container => true,
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    private void Stumble(Hazard hazard)
    {
        // Twice in quick succession is once too often.
        if (!Invincible && _clock - _stumbledAt < 4)
        {
            Crash(hazard);
            return;
        }

        _stumbledAt = _clock;
        _slow = 0.7;
        _shake = 0.45;
        _streak = 0;
    }

    private void Crash(Hazard hazard)
    {
        Phase = RunnerPhase.Over;
        _crashedAt = _clock;
        Best = Math.Max(Best, Score);
        _shake = 0.8;
        _vy = 6.5;
        _y = Math.Max(_y, 0.01);
        _spin = 0;
        _slideLeft = 0;
        // A hurdle trips you over itself. A bar or the end of a container is a
        // wall, and you come off it backwards rather than through it.
        if (hazard.Kind != Kind.Hurdle)
        {
            _speed = -3;
            _along = Math.Min(_along, hazard.Near - HalfDepth);
        }
    }

    private void Collect()
    {
        var from = _prevAlong - 0.9;
        var to = _along + 0.9;
        foreach (var coin in _road)
        {
            if (coin.TakenAt is null && coin.Along >= from && coin.Along <= to
                && Math.Abs(Lanes[coin.Lane] - _x) <= 0.9 && Math.Abs(coin.Height - (_y + 0.7)) <= 1.0)
            {
                Take(coin);
            }
        }
    }

    private void Take(Coin coin)
    {
        coin.TakenAt = _clock;
        Coins++;
        // One after another is worth more each time, which is what makes a
        // line of them worth following rather than merely worth having.
        _streak = _clock - _lastCoinAt < 0.8 ? _streak + 1 : 0;
        _bonus += 1 + _streak;
        _lastCoinAt = _clock;
        for (var i = 0; i < 4; i++)
        {
            Sparkle(new Vector3(Lanes[coin.Lane], coin.Height, Z(coin.Along)));
        }
    }
}

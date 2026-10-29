namespace Orblit.Examples;

// The runner played by nobody. It is the fairness test: a game the autopilot
// cannot survive is one a person cannot either.
public sealed partial class RunnerExample
{
    /// <summary>
    /// Plays: picks the best lane it can get to, and jumps or slides for
    /// whatever is in the one it is in.
    /// </summary>
    /// <remarks>
    /// Only what a player could see, and only the three moves a player has. It
    /// presses the same buttons, so what it shows is the game working, not
    /// something that looks like it.
    /// </remarks>
    private void Drive()
    {
        var speed = _speed;
        var worth = Worth(speed * 1.1 + 6);
        var wanted = _lane;
        for (var lane = 0; lane < 3; lane++)
        {
            if (worth[lane] > worth[wanted])
            {
                wanted = lane;
            }
        }

        var settled = Math.Abs(_x - Lanes[_lane]) < 0.3;
        // Unless it is a container that is being left, which is the one thing
        // worth breaking off a jump for, because it cannot be jumped.
        var fleeing = worth[_lane] < -50;
        if (wanted != _lane && settled && (fleeing || !Clearing(speed)))
        {
            var next = _lane + (wanted > _lane ? 1 : -1);
            if (!Walled(next, speed))
            {
                Steer(next - _lane);
            }
        }

        Clear(speed);
    }

    /// <summary>What each lane is worth over the next <paramref name="look"/> metres.</summary>
    private double[] Worth(double look)
    {
        var worth = new double[3];
        foreach (var hazard in _hazards)
        {
            if (!hazard.Struck && hazard.Far >= _along - 0.5 && hazard.Near - _along <= look)
            {
                // A container is a wall. Nothing gets past one, and a lane with
                // one in it is worth nothing at any price. A hurdle and a bar
                // are cleared, over and under, at no cost but the timing. So
                // they count for very little here: enough to prefer an empty
                // lane when nothing else separates them, and not nearly enough
                // to give up a lane of coins for.
                worth[hazard.Lane] -= hazard.Kind == Kind.Container ? 100 : 0.12;
            }
        }

        foreach (var coin in _road)
        {
            var ahead = coin.Along - _along;
            if (coin.TakenAt is null && ahead > 0 && ahead < look)
            {
                worth[coin.Lane] += 0.4;
            }
        }

        worth[_lane] += 0.5;
        worth[1] += 0.1;
        return worth;
    }

    /// <summary>
    /// Whether something in this lane is being cleared. It goes on being
    /// cleared. Leaving halfway through looks like running away from a hurdle
    /// rather than over it, and it lands the runner in whatever the next lane
    /// was holding.
    /// </summary>
    private bool Clearing(double speed) => !Grounded || IsSliding || _hazards.Any(hazard =>
        !hazard.Struck
        && hazard.Kind != Kind.Container
        && hazard.Lane == _lane
        && hazard.Near - _along > 0
        && hazard.Near - _along < speed * 1.4 + 2);

    /// <summary>Jumps or slides for whatever is coming in the lane the runner is in.</summary>
    private void Clear(double speed)
    {
        foreach (var hazard in _hazards)
        {
            var ahead = hazard.Near - _along;
            if (!Coming(hazard, ahead))
            {
                continue;
            }

            if (hazard.Kind == Kind.Hurdle && ahead <= speed * 0.28 + 0.5 && Grounded)
            {
                Jump();
            }
            else if (hazard.Kind == Kind.Bar && ahead <= speed * 0.35 + 0.5 && !IsSliding)
            {
                Slide();
            }
        }
    }

    /// <summary>Whether <paramref name="hazard"/> is still ahead and close enough sideways to hit.</summary>
    private bool Coming(Hazard hazard, double ahead) => !hazard.Struck && ahead >= 0
        && (hazard.Lane == _lane || Math.Abs(_x - Lanes[hazard.Lane]) < Reach);

    /// <summary>
    /// Whether moving into <paramref name="lane"/> now would be moving into
    /// trouble: a container alongside it, or a hurdle or bar too close to get
    /// over or under once there.
    /// </summary>
    private bool Walled(int lane, double speed) => _hazards.Any(hazard =>
        hazard.Lane == lane
        && !hazard.Struck
        && hazard.Near - 1 < _along + Clearance(hazard.Kind, speed)
        && hazard.Far > _along - 1);

    /// <summary>
    /// Room enough to be in the lane before it, and for a hurdle, to be high
    /// enough by then. A jump takes a tenth of a second to clear one. A slide
    /// starts the moment it is asked for, so a bar needs little.
    /// </summary>
    private static double Clearance(Kind kind, double speed) => kind switch
    {
        Kind.Container => speed * 0.3 + 2,
        Kind.Hurdle => speed * 0.25 + 1,
        Kind.Bar => speed * 0.1 + 1,
        _ => throw new System.Diagnostics.UnreachableException(),
    };
}

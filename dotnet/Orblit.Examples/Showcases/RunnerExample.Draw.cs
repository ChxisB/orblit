using Orblit.Filament;
using Orblit.VectorMath;

namespace Orblit.Examples;

// Everything the scene is built from, once per frame: the runner itself, the
// road under it, the sky over it, and the eye on it.
public sealed partial class RunnerExample
{
    private static readonly double[] Sides = [1.0, -1.0];

    private void DrawRunner(List<RenderObject> objects)
    {
        var (sx, sy) = Squash();
        var crashed = Phase == RunnerPhase.Over;
        var bob = Phase == RunnerPhase.Ready || crashed || !Grounded || IsSliding ? 0.0 : Math.Abs(Math.Sin(_step)) * 0.08;

        // Leaning into a lane change, and forward into the run.
        var drift = Lanes[_lane] - _x;
        var roll = crashed ? 0.0 : Math.Clamp(-drift * 0.12, -0.3, 0.3);
        var yaw = crashed ? 0.0 : Math.Clamp(-drift * 0.1, -0.25, 0.25);

        var frame = Matrix4.Identity();
        frame.TranslateByDouble(_x, _y + 0.2 + 0.55 * sy + bob, Z(_along), 1);
        frame.RotateY(yaw);
        frame.RotateZ(roll);
        frame.RotateX(Pitch());

        var body = frame.Clone();
        body.ScaleByDouble(sx, sy, sx, 1);
        objects.Add(new RenderObject { Key = 1, Mesh = RunnerArt.Body, Transform = body, Colour = White });
        foreach (var side in Sides)
        {
            objects.Add(Hand(frame, side, sx, sy));
        }

        foreach (var side in Sides)
        {
            objects.Add(Foot(frame, side, yaw));
        }
    }

    /// <summary>
    /// Squash and stretch: a little with every step, flat when sliding, tall on
    /// the way up and squashed on landing.
    /// </summary>
    private (double X, double Y) Squash()
    {
        var crashed = Phase == RunnerPhase.Over;
        var sy = 1 - 0.05 * Math.Cos(_step * 2);
        if (Phase == RunnerPhase.Ready)
        {
            sy = 1 + 0.03 * Math.Sin(_clock * 3);
        }

        if (IsSliding)
        {
            sy = 0.6;
        }

        if (!Grounded && !crashed)
        {
            sy = _vy > 0 ? 1.1 : 1.02;
        }

        var sinceLanding = _clock - _landedAt;
        if (sinceLanding < 0.16 && !IsSliding && !crashed)
        {
            sy -= _impact * 0.28 * (1 - sinceLanding / 0.16);
        }

        return (IsSliding ? 1.25 : 1 / Math.Sqrt(sy), sy);
    }

    private double Pitch() => Phase switch
    {
        RunnerPhase.Over => -_spin,
        RunnerPhase.Ready => 0.0,
        _ => IsSliding ? -0.25 : -0.12,
    };

    /// <summary>
    /// A hand, swinging opposite the boot on the same side, which is what a run
    /// looks like. Thrown up in the air, out in front on a slide, and loose at
    /// the sides in a crash. It hangs off <paramref name="frame"/>, so it leans
    /// with the body into a lane change.
    /// </summary>
    private RenderObject Hand(Matrix4 frame, double side, double sx, double sy)
    {
        var (lift, reach) = HandSwing(side);
        var hand = frame.Clone();
        hand.TranslateByDouble(side * 0.44 * sx, 0.08 * sy + lift, reach, 1);
        return new RenderObject { Key = side > 0 ? 2 : 3, Mesh = RunnerArt.Hand, Transform = hand, Colour = White };
    }

    private (double Lift, double Reach) HandSwing(double side)
    {
        var phase = _step + (side > 0 ? Math.PI : 0);
        if (Phase == RunnerPhase.Over)
        {
            return (0.1, 0.12);
        }

        if (!Grounded)
        {
            return (0.34, -0.14);
        }

        if (IsSliding)
        {
            return (-0.16, -0.34);
        }

        // Not standing to attention when ready. A slow idle, so the runner
        // looks like it is waiting rather than switched off.
        return Phase == RunnerPhase.Ready
            ? (0.02, Math.Sin(_clock * 2.2) * 0.05)
            : (Math.Max(0, Math.Cos(phase)) * 0.07, Math.Sin(phase) * 0.30);
    }

    /// <summary>
    /// A foot: striding on the ground, tucked in the air, out in front on a
    /// slide, and wherever the body goes in a crash.
    /// </summary>
    private RenderObject Foot(Matrix4 frame, double side, double yaw)
    {
        Matrix4 foot;
        if (Phase == RunnerPhase.Over)
        {
            foot = frame.Clone();
            foot.TranslateByDouble(side * 0.2, -0.5, 0, 1);
        }
        else
        {
            var (lift, reach) = Stride(side);
            foot = Matrix4.Identity();
            foot.TranslateByDouble(_x, _y, Z(_along), 1);
            foot.RotateY(yaw);
            foot.TranslateByDouble(side * 0.2, 0.08 + lift, reach, 1);
        }

        return new RenderObject { Key = side > 0 ? 4 : 5, Mesh = RunnerArt.Foot, Transform = foot, Colour = White };
    }

    private (double Lift, double Reach) Stride(double side)
    {
        var phase = _step + (side > 0 ? 0 : Math.PI);
        if (Phase == RunnerPhase.Ready)
        {
            return (0, 0);
        }

        if (!Grounded)
        {
            return (0.18, -0.05);
        }

        return IsSliding ? (0, -0.3) : (Math.Max(0, Math.Cos(phase)) * 0.16, Math.Sin(phase) * 0.26);
    }

    private void DrawRoad(List<RenderObject> objects)
    {
        foreach (var hazard in _hazards)
        {
            if (hazard.Near <= _along + Ahead && hazard.Far >= _along - 20)
            {
                objects.Add(Drawn(hazard));
            }
        }

        foreach (var coin in _road)
        {
            if (coin.Along <= _along + Ahead && coin.Along >= _along - 20 && Drawn(coin) is { } drawn)
            {
                objects.Add(drawn);
            }
        }
    }

    private RenderObject Drawn(Hazard hazard)
    {
        var along = hazard.Kind == Kind.Container ? hazard.Near : (hazard.Near + hazard.Far) / 2;
        var transform = RunnerArt.Place((Lanes[hazard.Lane], 0, Z(along)), (1, 1, 1));
        // Knocked flat, if it was run through with crashing turned off.
        if (hazard.Struck && hazard.Kind != Kind.Container)
        {
            transform.RotateX(-Math.PI / 2 * 0.85);
        }

        return new RenderObject { Key = hazard.Key, Mesh = MeshOf(hazard.Kind), Transform = transform, Colour = White };
    }

    private static string MeshOf(Kind kind) => kind switch
    {
        Kind.Hurdle => RunnerArt.Hurdle,
        Kind.Bar => RunnerArt.Bar,
        Kind.Container => RunnerArt.Container,
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    /// <summary>A coin, or null once it has been taken and has finished flying into the runner.</summary>
    private RenderObject? Drawn(Coin coin)
    {
        var x = Lanes[coin.Lane];
        var y = coin.Height;
        var z = Z(coin.Along);
        var size = 1.0;
        if (coin.TakenAt is { } taken)
        {
            // Up and into the runner, and gone.
            var t = (_clock - taken) / 0.25;
            if (t >= 1)
            {
                return null;
            }

            x += (_x - x) * t;
            y += (_y + 1.6 - y) * t;
            z += (Z(_along) - z) * t;
            size = 1 - t * 0.7;
        }

        var transform = Matrix4.Identity();
        transform.TranslateByDouble(x, y, z, 1);
        transform.RotateY(_clock * 4 + coin.Along * 0.3);
        transform.ScaleByDouble(size, size, size, 1);
        return new RenderObject
        {
            Key = coin.Key,
            Mesh = RunnerArt.Coin,
            Material = 3,
            Transform = transform,
            Colour = White,
            // A coin floats, and the shadow of a thin disc with nothing plainly
            // above it reads as a stain on the road.
            CastShadows = false,
        };
    }

    private void Sparkle(Vector3 at)
    {
        var spark = _sparks[_nextSpark];
        _nextSpark = (_nextSpark + 1) % _sparks.Length;
        var angle = _chance.NextDouble() * Math.PI * 2;
        spark.At.SetFrom(at);
        spark.Velocity.SetValues(Math.Cos(angle) * 2.2, 2.5 + _chance.NextDouble() * 2, Math.Sin(angle) * 2.2 - _speed * 0.6);
        spark.Life = 0.5;
    }

    private void UpdateSparks(double dt)
    {
        foreach (var spark in _sparks)
        {
            if (spark.Life <= 0)
            {
                continue;
            }

            spark.Life -= dt;
            spark.Velocity.Y -= 9 * dt;
            spark.At.AddScaled(spark.Velocity, dt);
        }
    }

    /// <summary>
    /// The clouds stay the same distance ahead however far the runner goes, as
    /// real ones a kilometre off seem to, and only the wind moves them.
    /// </summary>
    /// <remarks>
    /// The sky's own clouds are marched a pixel at a time and left grainy for
    /// temporal anti-aliasing to smooth. This scene cannot have that, because
    /// everything in it moves fast enough to smear.
    /// </remarks>
    private void DrawClouds(List<RenderObject> objects)
    {
        const double across = 640.0;
        for (var i = 0; i < CloudPlaces.Length; i++)
        {
            var cloud = CloudPlaces[i];
            var drift = cloud.X + _clock * 1.2 + across / 2;
            var x = drift - Math.Floor(drift / across) * across - across / 2;
            objects.Add(new RenderObject
            {
                Key = 70 + i,
                Mesh = RunnerArt.Cloud,
                Transform = RunnerArt.Place((x, cloud.Y, Z(_along + cloud.Ahead)), (cloud.Size, cloud.Size * 0.8, cloud.Size * 0.6)),
                Colour = White,
                CastShadows = false,
            });
        }
    }

    private void DrawSparks(List<RenderObject> objects)
    {
        for (var i = 0; i < _sparks.Length; i++)
        {
            var spark = _sparks[i];
            if (spark.Life <= 0)
            {
                continue;
            }

            // A glint rather than a crumb: long and thin, and turning.
            var size = 0.07 * spark.Life / 0.5;
            var transform = Matrix4.Identity();
            transform.TranslateByDouble(spark.At.X, spark.At.Y, spark.At.Z, 1);
            transform.RotateY(spark.Life * 12);
            transform.RotateZ(Math.PI / 4);
            transform.ScaleByDouble(size * 0.45, size * 1.8, size * 0.45, 1);
            objects.Add(new RenderObject { Key = 20 + i, Transform = transform, Colour = White, Material = 4, CastShadows = false });
        }
    }

    /// <summary>
    /// A little behind and above, looking down the road. At the start it is
    /// round in front, looking at the runner, and swings behind when the run
    /// begins.
    /// </summary>
    private RenderCamera Camera()
    {
        var z = Z(_along);
        var eye = new Vector3(_x * 0.55, 3.2 + _y * 0.35, z + 6.8);
        var look = new Vector3(_x * 0.85, 1.3 + _y * 0.5, z - 9);
        var field = FieldOfView();
        if (Phase == RunnerPhase.Ready || _intro < 1)
        {
            var t = Phase == RunnerPhase.Ready ? 0.0 : _intro;
            var blend = t * t * (3 - 2 * t);
            var frontEye = new Vector3(_x + 3.4, 1.35, z - 3.6);
            var frontLook = new Vector3(_x - 0.3, 0.95, z + 0.6);
            eye = frontEye + (eye - frontEye) * blend;
            look = frontLook + (look - frontLook) * blend;
            field = 44 + (field - 44) * blend;
        }

        if (_shake > 0)
        {
            var amount = _shake * _shake * 0.25;
            eye.X += Math.Sin(_clock * 53) * amount;
            eye.Y += Math.Sin(_clock * 41 + 1) * amount;
        }

        return new RenderCamera { Position = eye, Target = look, FieldOfView = field };
    }

    /// <summary>
    /// Wider as it gets faster, which is most of what makes fast feel fast. And
    /// wider still on a screen taller than it is wide, so all three lanes are in
    /// it.
    /// </summary>
    private double FieldOfView()
    {
        var field = 56 + (Math.Max(_speed, 0) - 12) * 0.4;
        if (Aspect >= 1.2)
        {
            return field;
        }

        const double across = 64 * Math.PI / 180;
        var needed = 2 * Math.Atan(Math.Tan(across / 2) / Aspect);
        return Math.Max(field, Math.Min(95, needed * 180 / Math.PI));
    }

    private sealed class Spark
    {
        public Vector3 At { get; } = Vector3.Zero();

        public Vector3 Velocity { get; } = Vector3.Zero();

        public double Life { get; set; }
    }
}

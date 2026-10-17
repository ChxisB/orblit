import 'dart:math' as math;

import 'package:vector_math/vector_math_64.dart';

/// What a steering behaviour is given to decide with.
///
/// Deliberately not the agent itself. A behaviour that held a reference to
/// whatever is moving would be a behaviour that could only be used by that
/// kind of thing, and half the point of steering is that the same `seek` moves
/// a bird, a boat and a cursor.
class Steerable {
  Steerable({
    Vector3? position,
    Vector3? velocity,
    this.maxSpeed = 4,
    this.maxForce = 8,
    this.radius = 0.5,
    this.priority = 0,
  }) : position = position ?? Vector3.zero(),
       velocity = velocity ?? Vector3.zero();

  Vector3 position;
  Vector3 velocity;

  /// How fast it may end up going, in metres a second.
  final double maxSpeed;

  /// How hard it may be pushed, which is what turns a jerk into a turn.
  ///
  /// The difference between the two is the whole feel of a mover: a high
  /// force against a low speed is something nimble, and a low force against a
  /// high speed is something with mass.
  final double maxForce;

  /// How wide it is, for the behaviours that care about touching.
  final double radius;

  /// Who gives way. [Avoid] steps round a neighbour of the same priority or
  /// higher and ignores a lower one, which is expected to step round it.
  final int priority;

  double get speed => velocity.length;

  /// Where it is facing, or null when it is not moving.
  ///
  /// Null rather than a stale direction, because "which way is it pointing"
  /// has no answer for something standing still, and the usual fudge — keep
  /// the last one — makes a mover that stops and starts snap to an angle it
  /// had a minute ago.
  Vector3? get heading {
    final length = velocity.length;
    return length < 1e-9 ? null : velocity / length;
  }

  /// Moves by [force] over [seconds], clamped to what it is capable of.
  void integrate(Vector3 force, double seconds) {
    if (seconds <= 0) return;

    final push = _truncate(force, maxForce);
    velocity += push * seconds;
    velocity = _truncate(velocity, maxSpeed);
    position += velocity * seconds;
  }
}

/// A force, capped at a length.
Vector3 _truncate(Vector3 value, double most) {
  final length = value.length;
  if (length <= most || length < 1e-12) return value;
  return value * (most / length);
}

/// One reason to move.
///
/// A behaviour answers "which way, and how hard" and nothing else. It does
/// not move anything, it does not decide whether it should be running, and it
/// does not know about the others — which is what lets several of them be
/// added together, and what makes each one testable on its own.
abstract class Steering {
  const Steering();

  /// The force this behaviour wants applied, in world space.
  Vector3 force(Steerable self);
}

/// Straight at a point, at full speed.
class Seek extends Steering {
  const Seek(this.target);

  final Vector3 target;

  @override
  Vector3 force(Steerable self) {
    final offset = target - self.position;
    if (offset.length < 1e-9) return Vector3.zero();
    // The force is the difference between the velocity wanted and the one
    // there is, which is what makes a mover turn rather than stop and start
    // again in the new direction.
    return offset.normalized() * self.maxSpeed - self.velocity;
  }
}

/// Straight away from a point.
class Flee extends Steering {
  const Flee(this.from, {this.within = double.infinity});

  final Vector3 from;

  /// How close it has to be before this cares. Beyond it, nothing.
  ///
  /// Without a limit a thing being fled is a thing fled from for ever,
  /// including from the other side of the map, which reads as a mover that
  /// will not settle.
  final double within;

  @override
  Vector3 force(Steerable self) {
    final offset = self.position - from;
    final distance = offset.length;
    if (distance < 1e-9 || distance > within) return Vector3.zero();
    return offset.normalized() * self.maxSpeed - self.velocity;
  }
}

/// At a point, slowing to a stop on it.
class Arrive extends Steering {
  const Arrive(this.target, {this.slowingRadius = 3});

  final Vector3 target;

  /// How far out it starts slowing down.
  ///
  /// What separates arriving from seeking is the speed on approach, not
  /// whether it overshoots: a steering force is a difference between the
  /// velocity wanted and the one there is, applied over a frame, so it lags
  /// and something coming in fast will still sail past. What this buys is
  /// that it comes in slowly, and therefore sails past by little and settles
  /// instead of circling.
  ///
  /// Wide enough to stop in, then. The braking a ramp asks for is
  /// v²/2d — four metres a second wanting to stop in three metres asks for
  /// under three metres a second squared, which is well inside a default
  /// agent's means. Halve the radius and it is not.
  final double slowingRadius;

  @override
  Vector3 force(Steerable self) {
    final offset = target - self.position;
    final distance = offset.length;
    if (distance < 1e-9) return -self.velocity;

    final wanted = distance < slowingRadius && slowingRadius > 0
        ? self.maxSpeed * (distance / slowingRadius)
        : self.maxSpeed;
    return offset.normalized() * wanted - self.velocity;
  }
}

/// At where something will be, rather than where it is.
///
/// Looking ahead by how long it would take to get there, so a chase closes on
/// the target instead of trailing behind it — a pursuer that aims at the
/// present position is always one step late and ends up describing a curve
/// behind its quarry.
class Pursue extends Steering {
  const Pursue(this.quarry, {this.lookAhead = 1.0});

  final Steerable quarry;

  /// A ceiling on how far into the future to aim, in seconds.
  final double lookAhead;

  @override
  Vector3 force(Steerable self) {
    final offset = quarry.position - self.position;
    final closing = math.max(self.maxSpeed, 1e-6);
    final ahead = math.min(offset.length / closing, lookAhead);
    return Seek(quarry.position + quarry.velocity * ahead).force(self);
  }
}

/// Away from where something will be.
class Evade extends Steering {
  const Evade(this.hunter, {this.lookAhead = 1.0, this.within = 12});

  final Steerable hunter;
  final double lookAhead;
  final double within;

  @override
  Vector3 force(Steerable self) {
    final offset = hunter.position - self.position;
    final closing = math.max(self.maxSpeed, 1e-6);
    final ahead = math.min(offset.length / closing, lookAhead);
    return Flee(
      hunter.position + hunter.velocity * ahead,
      within: within,
    ).force(self);
  }
}

/// Nowhere in particular, but not at random either.
///
/// A force drawn from noise rather than from a fresh random number each tick.
/// That is the whole trick: independent random numbers average to nothing and
/// produce a mover that vibrates on the spot, while a value that walks
/// smoothly produces one that ambles. The walk is deterministic in [seed] and
/// the time it is given, so the same agent wanders the same way twice — which
/// is what makes a wandering crowd reproducible in a test and in a replay.
class Wander extends Steering {
  const Wander({
    required this.seed,
    required this.at,
    this.strength = 1,
    this.rate = 0.35,
  });

  /// This agent's own number, so two agents side by side do not wander in
  /// step.
  final int seed;

  /// The clock, in seconds.
  final double at;

  /// How hard it pushes, as a fraction of what the agent can manage.
  final double strength;

  /// How quickly the direction drifts.
  final double rate;

  @override
  Vector3 force(Steerable self) {
    final t = at * rate;
    // Three independent walks, one per axis, offset from each other so they
    // are not the same curve three times over.
    final direction = Vector3(
      _walk(seed, t),
      _walk(seed + 8191, t) * 0.25,
      _walk(seed + 16381, t),
    );
    if (direction.length < 1e-9) return Vector3.zero();
    return direction.normalized() * (self.maxForce * strength);
  }

  /// A value between -1 and 1 that moves smoothly with [t].
  ///
  /// Value noise: the integers either side are hashed to fixed numbers and
  /// what is between them is faded across with a curve whose slope is zero at
  /// both ends. Linear interpolation would leave a kink at every integer, and
  /// a kink in a steering force reads as the agent flinching once a second.
  static double _walk(int seed, double t) {
    final floor = t.floor();
    final fraction = t - floor;
    final a = _hash(seed, floor);
    final b = _hash(seed, floor + 1);
    final blend = fraction * fraction * (3 - 2 * fraction);
    return a + (b - a) * blend;
  }

  /// One integer to one number between -1 and 1, always the same one.
  static double _hash(int seed, int at) {
    var x = (at * 374761393 + seed * 668265263) & 0x7FFFFFFF;
    x = (x ^ (x >> 13)) * 1274126177 & 0x7FFFFFFF;
    x = x ^ (x >> 16);
    return (x % 20001) / 10000.0 - 1.0;
  }
}

/// Away from whatever is too close.
///
/// The force falls off with distance, so a neighbour just inside the radius
/// nudges and one about to be walked into shoves. A flat force instead makes
/// a crowd that shuffles as a block.
class Separate extends Steering {
  const Separate(this.neighbours, {this.radius = 2});

  final List<Steerable> neighbours;
  final double radius;

  @override
  Vector3 force(Steerable self) {
    var away = Vector3.zero();
    var found = 0;

    for (final other in neighbours) {
      if (identical(other, self)) continue;
      final offset = self.position - other.position;
      final distance = offset.length;
      if (distance < 1e-9 || distance > radius) continue;

      // How far inside the radius it is: nothing at the edge, everything at
      // contact. The magnitude has to survive to the end — normalising the
      // total away, which is the usual way this is written, throws the
      // falloff out and leaves a crowd that shuffles as a block.
      away += offset.normalized() * ((radius - distance) / radius);
      found++;
    }

    if (found == 0) return Vector3.zero();
    away /= found.toDouble();

    final urgency = away.length;
    if (urgency < 1e-9) return Vector3.zero();
    return away.normalized() * (self.maxSpeed * urgency.clamp(0.0, 1.0)) -
        self.velocity;
  }
}

/// Steps round whatever it would walk into.
///
/// Of the velocities it could take, it picks the one that stays nearest to
/// [preferred] and hits no neighbour within [horizon] seconds, or hits the
/// latest one. A neighbour is assumed to keep the velocity it has, so two
/// agents meeting head on both turn the same way about the vertical axis and
/// pass on opposite sides. The candidates and their order are fixed, so the
/// same crowd in the same order gives the same run.
///
/// [preferred] is the velocity the route wants, such as a [Seek] without the
/// subtraction. It is capped at the agent's top speed. Neighbours of lower
/// [Steerable.priority] are not avoided. The force is the velocity chosen
/// minus the one it has, as [Seek] gives, so it blends with the others.
///
/// shortcut: every neighbour in the list is looked at, so a crowd of n costs
/// n squared. Fine for a few hundred. Upgrade to a cell index for the
/// neighbours before a crowd is in the thousands.
final class Avoid extends Steering {
  Avoid(
    this.preferred,
    this.neighbours, {
    this.horizon = 2,
    this.turns = 8,
    this.weight = 2,
  }) {
    if (!(horizon > 0 && horizon.isFinite)) {
      throw ArgumentError.value(horizon, 'horizon', 'Must be above zero.');
    }
    if (turns < 1) {
      throw ArgumentError.value(turns, 'turns', 'Must be at least one.');
    }
    if (!(weight >= 0 && weight.isFinite)) {
      throw ArgumentError.value(weight, 'weight', 'Must be zero or more.');
    }
  }

  final Vector3 preferred;
  final List<Steerable> neighbours;

  /// How far ahead, in seconds, a hit is looked for.
  final double horizon;

  /// How many steps of turn it tries from straight on to straight back, each
  /// side.
  final int turns;

  /// How much a near hit counts against a change of velocity. The cost of a
  /// hit is this over the seconds until it, against the metres a second the
  /// velocity moved from [preferred].
  final double weight;

  @override
  Vector3 force(Steerable self) {
    final wanted = _truncate(preferred, self.maxSpeed);
    final threats = [
      for (final other in neighbours)
        if (_matters(self, other)) other,
    ];
    if (threats.isEmpty) return wanted - self.velocity;

    var best = wanted;
    var bestCost = double.infinity;
    for (final candidate in _candidates(wanted)) {
      final cost = _cost(self, candidate, wanted, threats);
      if (cost < bestCost) {
        best = candidate;
        bestCost = cost;
      }
    }
    return best - self.velocity;
  }

  bool _matters(Steerable self, Steerable other) {
    if (other.priority < self.priority) return false;
    final reach =
        (self.maxSpeed + other.maxSpeed) * horizon + self.radius + other.radius;
    return self.position.distanceTo(other.position) <= reach;
  }

  /// The velocity wanted first, then turns of it either side in growing
  /// steps, positive first, each at full speed and at half, then standing
  /// still. Positive turns an agent heading along +x towards -z, which is the
  /// hand every agent favours when two meet head on.
  Iterable<Vector3> _candidates(Vector3 wanted) sync* {
    yield wanted;
    for (var step = 1; step <= turns; step++) {
      for (final sign in const [1, -1]) {
        final turned = _turned(wanted, sign * step * math.pi / turns);
        yield turned;
        yield turned * 0.5;
      }
    }
    yield wanted * 0.5;
    yield Vector3.zero();
  }

  double _cost(
    Steerable self,
    Vector3 candidate,
    Vector3 wanted,
    List<Steerable> threats,
  ) {
    var soonest = double.infinity;
    for (final other in threats) {
      final hit = _timeToHit(self, other, candidate);
      if (hit < soonest) soonest = hit;
    }
    final change = candidate.distanceTo(wanted);
    if (soonest >= horizon) return change;
    // Floored so a hit this instant costs a large number. Divided by zero it
    // would be infinity, and a weight of zero would make it not a number.
    return change + weight / math.max(soonest, 1e-3);
  }

  /// Seconds until [self], moving at [velocity], touches [other], moving as
  /// it is, or infinity if it never does. Zero if they already touch and are
  /// closing.
  static double _timeToHit(Steerable self, Steerable other, Vector3 velocity) {
    final offset = other.position - self.position;
    final relative = velocity - other.velocity;
    final reach = self.radius + other.radius;
    final closing = offset.dot(relative);
    final gap = offset.dot(offset) - reach * reach;
    if (gap < 0) return closing > 0 ? 0 : double.infinity;

    final speedSquared = relative.dot(relative);
    if (closing <= 0 || speedSquared < 1e-12) return double.infinity;
    final discriminant = closing * closing - speedSquared * gap;
    if (discriminant <= 0) return double.infinity;
    return (closing - math.sqrt(discriminant)) / speedSquared;
  }

  /// [v] turned by [angle] about the vertical axis.
  static Vector3 _turned(Vector3 v, double angle) {
    final c = math.cos(angle);
    final s = math.sin(angle);
    return Vector3(v.x * c + v.z * s, v.y, -v.x * s + v.z * c);
  }
}

/// Facing the way the neighbours are facing.
class Align extends Steering {
  const Align(this.neighbours, {this.radius = 4});

  final List<Steerable> neighbours;
  final double radius;

  @override
  Vector3 force(Steerable self) {
    var heading = Vector3.zero();
    var found = 0;

    for (final other in neighbours) {
      if (identical(other, self)) continue;
      if ((other.position - self.position).length > radius) continue;
      heading += other.velocity;
      found++;
    }

    if (found == 0 || heading.length < 1e-9) return Vector3.zero();
    return heading.normalized() * self.maxSpeed - self.velocity;
  }
}

/// Towards where the neighbours are.
class Cohere extends Steering {
  const Cohere(this.neighbours, {this.radius = 5});

  final List<Steerable> neighbours;
  final double radius;

  @override
  Vector3 force(Steerable self) {
    var centre = Vector3.zero();
    var found = 0;

    for (final other in neighbours) {
      if (identical(other, self)) continue;
      if ((other.position - self.position).length > radius) continue;
      centre += other.position;
      found++;
    }

    if (found == 0) return Vector3.zero();
    return Seek(centre / found.toDouble()).force(self);
  }
}

/// Along a line of points, without stopping at each one.
///
/// Aims at the point after the one it is closest to reaching, so a path is
/// followed as a route rather than as a series of destinations — an agent
/// that arrives at every waypoint in turn stops dead at each corner.
class FollowPath extends Steering {
  const FollowPath(
    this.points, {
    required this.index,
    this.reached = 1.0,
    this.loop = false,
  });

  final List<Vector3> points;

  /// Which point it is heading for.
  final int index;

  /// How close counts as having got there.
  final double reached;

  final bool loop;

  /// The point to head for after this force has been applied.
  ///
  /// Returned rather than written, because a behaviour that advanced an index
  /// as a side effect of being asked for a force could not be asked twice —
  /// and being asked twice is what a test and a debug overlay both do.
  int nextIndex(Steerable self) {
    if (points.isEmpty) return index;
    final at = index.clamp(0, points.length - 1);
    if ((points[at] - self.position).length > reached) return at;
    if (at + 1 < points.length) return at + 1;
    return loop ? 0 : at;
  }

  /// Whether the path has been walked to its end.
  bool isDone(Steerable self) =>
      !loop &&
      points.isNotEmpty &&
      index >= points.length - 1 &&
      (points.last - self.position).length <= reached;

  @override
  Vector3 force(Steerable self) {
    if (points.isEmpty) return Vector3.zero();
    final at = index.clamp(0, points.length - 1);

    // The last point is arrived at rather than seeked, so a route ends with
    // the agent stopped on it instead of circling it.
    final isLast = !loop && at == points.length - 1;
    return isLast
        ? Arrive(
            points[at],
            slowingRadius: math.max(reached * 2, 1),
          ).force(self)
        : Seek(points[at]).force(self);
  }
}

/// Several reasons to move, weighed against each other.
///
/// Added together and truncated rather than picked between. Blending is what
/// makes a crowd read as a crowd: an agent that chose one behaviour per tick
/// snaps between them, and the snap is visible however good each behaviour is.
///
/// The order matters only for what survives the truncation, which is why the
/// urgent ones — separation, avoidance — belong first: everything is summed,
/// but if the total is over what the agent can push, what is left is
/// dominated by whatever contributed most.
class Blend extends Steering {
  const Blend(this.parts);

  /// Each behaviour and how much of it, as a fraction.
  final List<({Steering behaviour, double weight})> parts;

  @override
  Vector3 force(Steerable self) {
    var total = Vector3.zero();
    for (final part in parts) {
      if (part.weight == 0) continue;
      total += part.behaviour.force(self) * part.weight;
    }
    return _truncate(total, self.maxForce);
  }
}

part of 'runner.dart';

// Everything the scene is built from, once per frame: the runner
// itself, the road under it, the sky over it, and the eye on it.

extension _Draw on RunnerExample {
  void _drawRunner(List<OrblitObject> objects) {
    final z = _z(_along);
    final crashed = _phase == RunnerPhase.over;
    final ready = _phase == RunnerPhase.ready;
    final stride = _step;

    // Squash and stretch: a little with every step, flat when sliding,
    // tall on the way up and squashed on landing.
    var sy = 1 - 0.05 * math.cos(stride * 2);
    if (ready) sy = 1 + 0.03 * math.sin(_clock * 3);
    if (_isSliding) sy = 0.6;
    if (!_grounded && !crashed) sy = _vy > 0 ? 1.1 : 1.02;
    final sinceLanding = _clock - _landedAt;
    if (sinceLanding < 0.16 && !_isSliding && !crashed) {
      sy -= _impact * 0.28 * (1 - sinceLanding / 0.16);
    }
    final sx = _isSliding ? 1.25 : 1 / math.sqrt(sy);
    final bob = ready || crashed || !_grounded || _isSliding
        ? 0.0
        : math.sin(stride).abs() * 0.08;
    final bodyY = _y + 0.2 + 0.55 * sy + bob;

    // Leaning into a lane change, and forward into the run.
    final drift = _lanes[_lane] - _x;
    final roll = crashed ? 0.0 : (-drift * 0.12).clamp(-0.3, 0.3);
    final yaw = crashed ? 0.0 : (-drift * 0.1).clamp(-0.25, 0.25);
    final pitch = crashed
        ? -_spin
        : ready
        ? 0.0
        : _isSliding
        ? -0.25
        : -0.12;

    final frame = Matrix4.identity()
      ..translateByDouble(_x, bodyY, z, 1)
      ..rotateY(yaw)
      ..rotateZ(roll)
      ..rotateX(pitch);

    objects.add(
      OrblitObject(
        key: 1,
        mesh: RunnerArt.body,
        transform: frame.clone()..scaleByDouble(sx, sy, sx, 1),
        colour: _white,
      ),
    );

    // Hands: swinging opposite the boot on the same side, which is what a
    // run looks like; thrown up in the air, out in front on a slide, and
    // loose at the sides in a crash. They hang off `frame`, so they lean
    // with the body into a lane change.
    for (final side in const [1.0, -1.0]) {
      final phase = stride + (side > 0 ? math.pi : 0);
      var lift = 0.0, reach = 0.0;
      if (crashed) {
        lift = 0.1;
        reach = 0.12;
      } else if (!_grounded) {
        lift = 0.34;
        reach = -0.14;
      } else if (_isSliding) {
        lift = -0.16;
        reach = -0.34;
      } else if (ready) {
        // Not standing to attention: a slow idle, so that the runner looks
        // like it is waiting rather than switched off.
        reach = math.sin(_clock * 2.2) * 0.05;
        lift = 0.02;
      } else {
        reach = math.sin(phase) * 0.30;
        lift = math.max(0, math.cos(phase)) * 0.07;
      }
      objects.add(
        OrblitObject(
          key: side > 0 ? 2 : 3,
          mesh: RunnerArt.hand,
          transform: frame.clone()
            ..translateByDouble(side * 0.44 * sx, 0.08 * sy + lift, reach, 1),
          colour: _white,
        ),
      );
    }

    // Feet: striding on the ground, tucked in the air, out in front on a
    // slide, and wherever the body goes in a crash.
    for (final side in const [1.0, -1.0]) {
      final phase = stride + (side > 0 ? 0 : math.pi);
      final Matrix4 foot;
      if (crashed) {
        foot = frame.clone()..translateByDouble(side * 0.2, -0.5, 0, 1);
      } else {
        var lift = 0.0, reach = 0.0;
        if (ready) {
          lift = 0;
        } else if (!_grounded) {
          lift = 0.18;
          reach = -0.05;
        } else if (_isSliding) {
          reach = -0.3;
        } else {
          reach = math.sin(phase) * 0.26;
          lift = math.max(0, math.cos(phase)) * 0.16;
        }
        foot = Matrix4.identity()
          ..translateByDouble(_x, _y, z, 1)
          ..rotateY(yaw)
          ..translateByDouble(side * 0.2, 0.08 + lift, reach, 1);
      }
      objects.add(
        OrblitObject(
          key: side > 0 ? 4 : 5,
          mesh: RunnerArt.foot,
          transform: foot,
          colour: _white,
        ),
      );
    }
  }

  void _drawRoad(List<OrblitObject> objects) {
    for (final hazard in _hazards) {
      if (hazard.near > _along + _ahead || hazard.far < _along - 20) continue;
      final mesh = switch (hazard.kind) {
        _Kind.hurdle => RunnerArt.hurdle,
        _Kind.bar => RunnerArt.bar,
        _Kind.container => RunnerArt.container,
      };
      final along = hazard.kind == _Kind.container
          ? hazard.near
          : (hazard.near + hazard.far) / 2;
      final transform = place(_lanes[hazard.lane], 0, _z(along));
      // Knocked flat, if it was run through with crashing turned off.
      if (hazard.struck && hazard.kind != _Kind.container) {
        transform.rotateX(-math.pi / 2 * 0.85);
      }
      objects.add(
        OrblitObject(
          key: hazard.key,
          mesh: mesh,
          transform: transform,
          colour: _white,
        ),
      );
    }

    for (final coin in _road) {
      if (coin.along > _along + _ahead || coin.along < _along - 20) continue;
      var x = _lanes[coin.lane], y = coin.height, z = _z(coin.along);
      var size = 1.0;
      final taken = coin.takenAt;
      if (taken != null) {
        // Up and into the runner, and gone.
        final t = (_clock - taken) / 0.25;
        if (t >= 1) continue;
        x += (_x - x) * t;
        y += (_y + 1.6 - y) * t;
        z += (_z(_along) - z) * t;
        size = 1 - t * 0.7;
      }
      objects.add(
        OrblitObject(
          key: coin.key,
          mesh: RunnerArt.coin,
          material: 3,
          transform: Matrix4.identity()
            ..translateByDouble(x, y, z, 1)
            ..rotateY(_clock * 4 + coin.along * 0.3)
            ..scaleByDouble(size, size, size, 1),
          colour: _white,
          // A coin floats, and the shadow of a thin disc with nothing plainly
          // above it reads as a stain on the road.
          castShadows: false,
        ),
      );
    }
  }

  void _spark(Vector3 at) {
    final spark = _sparks[_nextSpark];
    _nextSpark = (_nextSpark + 1) % _sparks.length;
    final angle = _chance.nextDouble() * math.pi * 2;
    spark
      ..at.setFrom(at)
      ..velocity.setValues(
        math.cos(angle) * 2.2,
        2.5 + _chance.nextDouble() * 2,
        math.sin(angle) * 2.2 - _speed * 0.6,
      )
      ..life = 0.5;
  }

  void _updateSparks(double dt) {
    for (final spark in _sparks) {
      if (spark.life <= 0) continue;
      spark.life -= dt;
      spark.velocity.y -= 9 * dt;
      spark.at.addScaled(spark.velocity, dt);
    }
  }

  /// The clouds stay the same distance ahead however far the runner goes, as
  /// real ones a kilometre off seem to, and only the wind moves them.
  ///
  /// The sky's own clouds are marched a pixel at a time and left grainy for
  /// temporal anti-aliasing to smooth — which this scene cannot have, because
  /// everything in it moves fast enough to smear.
  void _drawClouds(List<OrblitObject> objects) {
    const across = 640.0;
    for (var i = 0; i < _clouds.length; i++) {
      final cloud = _clouds[i];
      final drift = cloud.x + _clock * 1.2 + across / 2;
      final x = drift - (drift / across).floor() * across - across / 2;
      objects.add(
        OrblitObject(
          key: 70 + i,
          mesh: RunnerArt.cloud,
          transform: place(
            x,
            cloud.y,
            _z(_along + cloud.ahead),
            sx: cloud.size,
            sy: cloud.size * 0.8,
            sz: cloud.size * 0.6,
          ),
          colour: _white,
          castShadows: false,
        ),
      );
    }
  }

  void _drawSparks(List<OrblitObject> objects) {
    for (var i = 0; i < _sparks.length; i++) {
      final spark = _sparks[i];
      if (spark.life <= 0) continue;
      // A glint rather than a crumb: long and thin, and turning.
      final size = 0.07 * spark.life / 0.5;
      objects.add(
        OrblitObject(
          key: 20 + i,
          transform: Matrix4.identity()
            ..translateByDouble(spark.at.x, spark.at.y, spark.at.z, 1)
            ..rotateY(spark.life * 12)
            ..rotateZ(math.pi / 4)
            ..scaleByDouble(size * 0.45, size * 1.8, size * 0.45, 1),
          colour: _white,
          material: 4,
          castShadows: false,
        ),
      );
    }
  }

  /// A little behind and above, looking down the road — and at the start,
  /// round in front, looking at the runner, swinging behind when the run
  /// begins.
  OrblitCamera _camera() {
    final z = _z(_along);
    final chaseEye = Vector3(_x * 0.55, 3.2 + _y * 0.35, z + 6.8);
    final chaseLook = Vector3(_x * 0.85, 1.3 + _y * 0.5, z - 9);
    // Wider as it gets faster, which is most of what makes fast feel fast.
    var field = 56 + (math.max(_speed, 0) - 12) * 0.4;
    // And wider still on a screen taller than it is wide, so all three lanes
    // are in it.
    if (_aspect < 1.2) {
      const across = 64 * math.pi / 180;
      final needed = 2 * math.atan(math.tan(across / 2) / _aspect);
      field = math.max(field, math.min(95, needed * 180 / math.pi));
    }

    final eye = Vector3.copy(chaseEye);
    final look = Vector3.copy(chaseLook);
    if (_phase == RunnerPhase.ready || _intro < 1) {
      final t = _phase == RunnerPhase.ready ? 0.0 : _intro;
      final blend = t * t * (3 - 2 * t);
      final frontEye = Vector3(_x + 3.4, 1.35, z - 3.6);
      final frontLook = Vector3(_x - 0.3, 0.95, z + 0.6);
      eye.setFrom(frontEye + (chaseEye - frontEye) * blend);
      look.setFrom(frontLook + (chaseLook - frontLook) * blend);
      field = 44 + (field - 44) * blend;
    }

    if (_shake > 0) {
      final amount = _shake * _shake * 0.25;
      eye.x += math.sin(_clock * 53) * amount;
      eye.y += math.sin(_clock * 41 + 1) * amount;
    }

    _eye = eye;
    _look = look;
    _fieldOfView = field;
    return OrblitCamera(position: eye, target: look, fieldOfView: field);
  }
}

class _Spark {
  final at = Vector3.zero();
  final velocity = Vector3.zero();
  double life = 0;
}

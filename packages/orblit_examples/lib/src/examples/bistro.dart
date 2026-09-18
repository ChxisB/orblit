import 'dart:convert';
import 'dart:math' as math;

import '../platform/io.dart';

import 'package:flutter/material.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:vector_math/vector_math_64.dart' hide Colors;

import '../example.dart';
import 'surface.dart' show linearOf;

/// The Amazon Lumberyard Bistro, lit by this engine.
///
/// Every other example builds its scene out of cubes, which is honest about
/// what it is demonstrating and useless for judging whether the lighting
/// looks right. A cube lit badly still looks like a cube. This is somebody
/// else's art, built for a different renderer, with reference images of how
/// it is supposed to look — which is the only way to find out whether a
/// lighting model is convincing rather than merely arithmetic.
///
/// It is also the heaviest thing here by a wide margin: 1,296 meshes, 132
/// materials and 2.8 million triangles in the exterior, against a hundred
/// point lights. What a frame costs on this is worth more than what it costs
/// on a hundred thousand cubes, because it is shaped like a real scene.
///
///   Amazon Lumberyard Bistro, Open Research Content Archive (ORCA)
///   https://developer.nvidia.com/orca/amazon-lumberyard-bistro
///   Amazon Lumberyard, CC BY 4.0
///
/// Fetched rather than shipped — it is half a gigabyte, and not ours:
///
///   ./tool/fetch_bistro.sh exterior
abstract class BistroExample extends Example {
  BistroExample();

  /// Which file, under the fetched directory.
  String get asset;

  /// Where the assets landed. The engine repository's own path by default,
  /// because that is where the fetch script puts them, and an override for
  /// everybody whose checkout is somewhere else.
  static String get directory =>
      Platform.environment['ORBLIT_BISTRO'] ?? '../orblit/assets/bistro';

  String get _model => '$directory/$asset.gltf';

  /// The fixtures, taken out of the scene's own emissive geometry when it was
  /// fetched. Read once: it is a hundred entries and the scene is rebuilt
  /// sixty times a second.
  late final List<BistroFixture> fixtures = _readFixtures();

  List<BistroFixture> _readFixtures() {
    final file = File('$directory/$asset.lights.json');
    if (!file.existsSync()) return const [];
    final rows = jsonDecode(file.readAsStringSync()) as List<Object?>;
    return [
      for (final row in rows.cast<Map<String, Object?>>())
        BistroFixture(
          kind: row['kind']! as String,
          at: Vector3(
            (row['at']! as List)[0] as double,
            (row['at']! as List)[1] as double,
            (row['at']! as List)[2] as double,
          ),
        ),
    ];
  }

  bool get ready => File(_model).existsSync();

  /// The prefiltered environment cmgen made when the scene was fetched.
  ///
  /// Empty when it has not been built, and the scene falls back to the flat
  /// ambient — which is worth saying out loud, because the difference between
  /// the two is most of the difference between a render and a photograph.
  String get radiance => _envIfPresent('bistro_ibl.ktx');
  String get skyboxMap => _envIfPresent('bistro_skybox.ktx');

  String _envIfPresent(String name) {
    final file = File('$directory/$name');
    return file.existsSync() ? file.absolute.path : '';
  }

  bool get hasEnvironment => radiance.isNotEmpty;

  /// The model, as one object. Its own root node carries the turn from Z-up
  /// and the scale into metres, so nothing is done to it here.
  OrblitObject get model => OrblitObject(
    key: 1,
    transform: Matrix4.identity(),
    colour: Vector3(0.8, 0.8, 0.8),
    mesh: File(_model).absolute.path,
  );

  @override
  Widget? overlay(BuildContext context, VoidCallback changed) {
    if (ready) return null;
    return const _Missing();
  }
}

/// One emissive fixture in the scene, and where it is.
class BistroFixture {
  const BistroFixture({required this.kind, required this.at});

  final String kind;
  final Vector3 at;
}

/// What each kind of fixture is, in units a fitting is sold in.
///
/// Lumens rather than a brightness between nought and one, because these are
/// real fittings in a real street: a sodium street lamp is a couple of
/// thousand lumens and a festoon bulb is under a hundred, and that ratio of
/// twenty-five to one is most of why the reference images read as evening
/// rather than as a stage set.
const _fittings = <String, ({Color colour, double lumens, double reach})>{
  // Reach is a cull distance as much as a physical one — beyond it the light
  // contributes nothing and the renderer can skip it. Set too short it is
  // visible as a hard edge where a pool of light stops, and set to what a
  // lamp really lights, the pools overlap the way they do in a street.
  'street': (colour: Color(0xFFFFB870), lumens: 5200, reach: 28),
  'spot': (colour: Color(0xFFFFD5A8), lumens: 1400, reach: 12),
  'sign': (colour: Color(0xFFFFE0B0), lumens: 900, reach: 9),
  'orange': (colour: Color(0xFFFF8A3D), lumens: 70, reach: 4),
  'red': (colour: Color(0xFFFF4A4A), lumens: 70, reach: 4),
  'white': (colour: Color(0xFFFFF2DC), lumens: 90, reach: 4),
  'pink': (colour: Color(0xFFFF7ACB), lumens: 70, reach: 4),
  'blue': (colour: Color(0xFF5AA6FF), lumens: 70, reach: 4),
  'green': (colour: Color(0xFF6BE86B), lumens: 70, reach: 4),
};

/// The street outside, at night or in daylight.
///
/// Your four reference images are two scenes: this is the first two of them.
/// Night is the interesting one — a hundred small sources, six colours of
/// festoon bulb, and almost no ambient — because that is the case a renderer
/// either sells or does not.
class BistroExteriorExample extends BistroExample {
  BistroExteriorExample();

  @override
  Downloadable? get needs => const Downloadable(
    what: 'the Bistro exterior',
    size: 'about 700 MB',
    from: 'Amazon Lumberyard Bistro, via ORCA',
    licence: 'CC BY 4.0',
    command: ['tool/fetch_bistro.sh', 'exterior'],
  );

  @override
  String get name => 'Bistro exterior';

  @override
  String get blurb =>
      'Somebody else\'s street, lit by this engine. A hundred lights at night.';

  @override
  String get asset => 'BistroExterior';

  @override
  ViewPoint get viewpoint =>
      const ViewPoint(distance: 22, pitch: 0.20, height: 3, yaw: 2.2);

  bool night = false;
  bool festoon = true;

  /// The film speed, which at night is the dial that decides whether there is
  /// a picture at all.
  double iso = 1600;

  /// The moon, in lux. A real full moon is about a quarter of one.
  double moon = 4;

  /// Walk the street, or stand still by the Vespa.
  ///
  /// An orbit is the right camera for looking at an object and the wrong one
  /// for a place. A street is meant to be walked down: the lamps pass
  /// overhead one at a time, the shopfronts come alongside, and the light on
  /// a wall changes because you moved rather than because the wall did. None
  /// of that is visible from a fixed point spinning around the middle.
  ///
  /// Off, it is a still of the Vespa rather than the orbit it used to hand
  /// back: see [_still].
  bool walking = true;

  /// Where the Vespa is: the middle of it, a little low.
  ///
  /// Read out of the model rather than found by eye. It stands at about
  /// forty-five degrees to the street with its headlight towards −x and −z,
  /// on pavement 0.37 m up.
  static final _vespa = Vector3(-7.4, 1.25, 0.5);

  /// The camera when not walking: a still of the Vespa, and how far away it
  /// is focused.
  ///
  /// Still, not an orbit — nothing turns unless somebody asks it to, and a
  /// camera that holds still is also what lets the temporal anti-aliasing
  /// settle into a clean picture. Framed the way a scooter is photographed:
  /// from the front three-quarters, low, which makes it stand up against the
  /// street rather than sit in it, and on a long lens a few metres off, so
  /// the shopfronts behind stay large and fall out of focus instead of
  /// shrinking into a wide shot.
  ///
  /// The numbers were chosen by looking, a few degrees at a time: straight
  /// down the headlight's line, it is all shield and no scooter; side on, it
  /// is a diagram. Just over forty degrees off its nose shows the shield, the
  /// curve of the body and both wheels at once.
  (Vector3, Vector3, double) _still() {
    const bearing = -168 * math.pi / 180; // which side of it the camera is on
    const distance = 6.0;
    const height = 1.05; // about knee height: 0.7 m above the pavement
    const lead = 0.5;

    final away = Vector3(math.cos(bearing), 0, math.sin(bearing));
    final eye = _vespa + away * distance
      ..y = height;
    // Aimed half a metre to the side it faces, which puts it right of centre
    // with the room in front of it rather than behind.
    final look = _vespa + Vector3(-away.z, 0, away.x) * lead;
    return (eye, look, (_vespa - eye).length);
  }

  /// Where the walk goes.
  ///
  /// Searched, not chosen, and then checked. Two earlier attempts were
  /// guesses dressed up as reasoning: the first followed the street lamps, on
  /// the argument that lamps stand along a street — they stand on the
  /// pavement, with the building between them, so it walked through the
  /// restaurant. The second read an occupancy map by eye at three-metre
  /// resolution and picked a corridor out of it, which clipped eighteen
  /// samples in a hundred.
  ///
  /// These come from a breadth-first search of the open ground: every
  /// primitive occupying the height a person does becomes a solid box, the
  /// free space around the plaza is flooded at half a metre with three
  /// quarters of a metre of clearance, and the longest route through it is
  /// what the walk follows. Two hundred and thirty-six square metres of
  /// walkable ground, and fifty-six metres of walk in it.
  ///
  /// Twelve waypoints rather than five because the curve between them bows
  /// outward, and a sparse set bows far enough to cut a corner into a wall —
  /// at nine points the tightest clearance was zero. At these the curve keeps
  /// half a metre from anything, measured at six hundred points along it,
  /// which is what makes this a checked path rather than a third guess.
  static final _path = <Vector3>[
    Vector3(-4.0, 1.7, -12.0),
    Vector3(-9.0, 1.7, -12.0),
    Vector3(-10.0, 1.7, -8.0),
    Vector3(-10.0, 1.7, -3.0),
    Vector3(-10.0, 1.7, 2.0),
    Vector3(-7.0, 1.7, 4.0),
    Vector3(-6.0, 1.7, 8.0),
    Vector3(-2.0, 1.7, 9.0),
    Vector3(1.5, 1.7, 10.5),
    Vector3(4.5, 1.7, 12.5),
    Vector3(8.5, 1.7, 13.5),
    Vector3(11.5, 1.7, 15.5),
  ];

  /// Walking pace, in metres a second.
  static const double _pace = 1.3;

  /// How long setting off and pulling up each take, in seconds.
  static const double _gather = 2.0;

  /// Half the stretch of path the heading is taken across, in metres.
  static const double _reach = 2.5;

  (Vector3, Vector3) _walk(double seconds) {
    // Long enough to read as a turn, not a spin. It was 3.2 s, and once the
    // rest of the walk was smoothed that made the half-turn at each end the
    // fastest the camera ever swung, at eighty-five degrees a second.
    const turnTime = 4.5;

    final total = _length;
    final legTime = total / _pace + _gather;
    final cycle = (legTime + turnTime) * 2;
    final t = seconds % cycle;

    // How far along, and how far through a turn. The yaw is built up as one
    // number that only ever increases through the cycle — heading, then
    // heading plus half a turn, then a whole one — because a yaw that jumps
    // back is exactly the snap this had before: the return leg faced one way
    // and the turn at the end of it started from the other.
    final double distance;
    var extraTurn = 0.0;
    if (t < legTime) {
      distance = _along(t, legTime);
    } else if (t < legTime + turnTime) {
      distance = total;
      extraTurn = _ease((t - legTime) / turnTime);
    } else if (t < legTime * 2 + turnTime) {
      distance = total - _along(t - legTime - turnTime, legTime);
      extraTurn = 1;
    } else {
      distance = 0;
      extraTurn = 1 + _ease((t - legTime * 2 - turnTime) / turnTime);
    }

    final eye = _at(distance);

    // The way the body faces: along the line from where the walk was a couple
    // of metres back to where it will be a couple of metres on.
    //
    // This used to be the direction of the path underfoot, read off whichever
    // quarter-metre piece of it the walk was on. That changes in a step at
    // every piece, and eight metres out, where the camera looks, a step of a
    // few degrees is a jump of half a metre, five times a second through
    // every bend. The line across five metres of path is the path's
    // direction averaged over them, so it turns smoothly and starts into a
    // bend a little before reaching it, as somebody walking does.
    //
    // On the return leg this still points the way the path was drawn, so it
    // is the half-turns that carry the direction — one of them says "walking
    // back", two says "round again".
    final heading = _at(distance + _reach) - _at(distance - _reach);
    final yaw = math.atan2(heading.z, heading.x) + math.pi * extraTurn;

    // Looking about, on top of that. Two slow waves so it never settles into
    // an obvious rhythm, and gentle enough not to fight the turn.
    final sweep =
        math.sin(seconds * 0.19) * 0.32 + math.sin(seconds * 0.081) * 0.14;

    // No bob. There was one, a couple of centimetres at two steps a second,
    // and it was the only part of this that moved faster than a slow wave:
    // on screen it read as the picture shaking rather than as walking. This
    // is a camera carried steady down the street, not a head.

    final look = yaw + sweep;
    // A little up: the interesting part of this street is above eye level —
    // the lamps, the signage, the balconies.
    final rise = 0.14 + math.sin(seconds * 0.13) * 0.09;
    return (
      eye,
      eye + Vector3(math.cos(look) * 8, rise * 8, math.sin(look) * 8),
    );
  }

  /// How far along the path a leg is, `t` seconds after setting off.
  ///
  /// The walk used to go from standing to full pace in one frame and stop the
  /// same way, which is a jolt however smooth the path is. Now each end of a
  /// leg eases over [_gather] seconds, the speed following a smoothstep, so
  /// not even the acceleration jumps. The distance an eased start covers is
  /// the smoothstep's integral, x³ − x⁴/2 — half of what full pace would
  /// cover in the same time, which is why a leg takes [_gather] seconds longer
  /// than the path at full pace.
  static double _along(double t, double legTime) {
    double eased(double t) {
      final x = (t / _gather).clamp(0.0, 1.0);
      return _pace * _gather * (x * x * x - x * x * x * x / 2);
    }

    if (t < _gather) return eased(t);
    if (t > legTime - _gather) return _length - eased(legTime - t);
    return _pace * (t - _gather / 2);
  }

  /// Smoothstep: starts and stops at zero speed.
  static double _ease(double t) {
    final c = t.clamp(0.0, 1.0);
    return c * c * (3 - 2 * c);
  }

  /// The path, as a curve rather than a set of corners.
  ///
  /// A polyline was the first attempt and it is where the snap came from:
  /// between waypoints the direction is constant, and at each one it changes
  /// instantly. Four waypoints is three sharp turns, however smoothly the
  /// ends of the walk are handled.
  ///
  /// Catmull-Rom passes through every point it is given and has a continuous
  /// tangent, so both where the camera is and where it is pointed change
  /// smoothly the whole way along.
  static Vector3 _spline(double u) {
    final n = _path.length;
    final scaled = u.clamp(0.0, 1.0) * (n - 1);
    final i = scaled.floor().clamp(0, n - 2);
    final f = scaled - i;
    // The ends are doubled up so the curve starts and finishes where the
    // waypoints do rather than overshooting past them.
    final p0 = _path[(i - 1).clamp(0, n - 1)];
    final p1 = _path[i];
    final p2 = _path[(i + 1).clamp(0, n - 1)];
    final p3 = _path[(i + 2).clamp(0, n - 1)];
    return (p1 * 2.0 +
            (p2 - p0) * f +
            (p0 * 2.0 - p1 * 5.0 + p2 * 4.0 - p3) * (f * f) +
            (p1 * 3.0 - p0 - p2 * 3.0 + p3) * (f * f * f)) *
        0.5;
  }

  /// The curve set out as stations an even distance apart along it, about
  /// five centimetres, and that distance.
  ///
  /// Walked at constant speed rather than at constant parameter: a spline's
  /// parameter is not its arc length, so stepping it evenly speeds up through
  /// the straights and dawdles round the bends. So the curve is measured once,
  /// finely enough that the chord between two samples is the curve, and
  /// staked out; where the walk is then comes from the two stations either
  /// side of it rather than from searching the curve every frame.
  static final (List<Vector3>, double) _measured = () {
    const fine = 4000;
    final points = [for (var i = 0; i <= fine; i++) _spline(i / fine)];
    final run = <double>[0];
    for (var i = 1; i <= fine; i++) {
      run.add(run.last + (points[i] - points[i - 1]).length);
    }
    final count = (run.last / 0.05).ceil();
    final spacing = run.last / count;
    final stations = <Vector3>[];
    var j = 0;
    for (var k = 0; k <= count; k++) {
      final want = k * spacing;
      while (j < fine - 1 && run[j + 1] < want) {
        j++;
      }
      final span = run[j + 1] - run[j];
      final f = span > 0 ? ((want - run[j]) / span).clamp(0.0, 1.0) : 0.0;
      stations.add(points[j] + (points[j + 1] - points[j]) * f);
    }
    return (stations, spacing);
  }();

  /// The curve's length.
  static double get _length => (_measured.$1.length - 1) * _measured.$2;

  /// Where the walk is `distance` metres along the path.
  ///
  /// Off either end the path carries straight on, so a heading taken near an
  /// end has somewhere to look.
  static Vector3 _at(double distance) {
    final (stations, spacing) = _measured;
    final last = stations.length - 1;
    if (distance <= 0) {
      final start = stations[0];
      return start + (stations[1] - start).normalized() * distance;
    }
    if (distance >= _length) {
      final end = stations[last];
      return end +
          (end - stations[last - 1]).normalized() * (distance - _length);
    }
    final along = distance / spacing;
    final i = along.floor().clamp(0, last - 1);
    return stations[i] + (stations[i + 1] - stations[i]) * (along - i);
  }

  @override
  OrblitScene scene(OrblitCamera camera, double seconds) {
    final lights = <OrblitLight>[];
    var key = 100;

    if (night) {
      for (final fixture in fixtures) {
        final fitting = _fittings[fixture.kind];
        if (fitting == null) continue;
        if (!festoon && fitting.lumens < 100) continue;
        lights.add(
          OrblitLight(
            key: key++,
            kind: OrblitLightKind.point,
            position: fixture.at,
            colour: linearOf(fitting.colour),
            intensity: fitting.lumens,
            falloffRadius: fitting.reach,
            // A hundred shadow-casting points is not a thing any real-time
            // renderer does, and it is not what makes this read: the shadows
            // that matter here are the ones the moon casts.
            castShadows: false,
          ),
        );
      }
      // The moon, at what a moon actually is.
      //
      // This was 900 lux, and that one number was most of why the night did
      // not read as night: it is roughly three thousand times a real full
      // moon, so it flooded every surface evenly and the hundred lamps —
      // which are the whole point — contributed almost nothing next to it.
      // A scene lit flat has no depth, and no amount of tuning the lamps
      // fixes a fill light that is drowning them.
      //
      // Three lux is generous for a full moon and leaves the street dark
      // enough that a lamp pools light on it, which is what the reference
      // images actually look like.
      lights.add(
        OrblitLight(
          key: 1,
          kind: OrblitLightKind.directional,
          direction: Vector3(-0.3, -1, 0.4)..normalize(),
          colour: linearOf(const Color(0xFF9FB4D8)),
          intensity: moon,
          castShadows: true,
        ),
      );
    } else {
      // Daylight is one light, which is the whole point of stating these in
      // lux: a hundred thousand of them is what the sun actually is.
      lights.add(
        OrblitLight(
          key: 1,
          kind: OrblitLightKind.directional,
          direction: Vector3(-0.45, -0.82, -0.35)..normalize(),
          colour: linearOf(const Color(0xFFFFF6E8)),
          intensity: 100000,
          castShadows: true,
        ),
      );
    }

    return OrblitScene(
      // The camera the gallery hands over is framed on sunny-16 — f/16 at a
      // hundred-and-twenty-fifth, ISO 100 — which is right for the daylight
      // setting and about seventeen stops too dark for the night one. That is
      // not a detail: on one fixed exposure either the night is black or the
      // day is white, which is exactly why these are stated as a real camera's
      // three numbers rather than as a brightness.
      camera: () {
        if (walking) {
          final (eye, look) = _walk(seconds);
          return OrblitCamera(
            position: eye,
            target: look,
            // Wider on foot. Fifty degrees is a portrait lens and a street
            // seen through one feels like a corridor; somebody actually
            // standing in this square sees most of it at once.
            fieldOfView: 65,
            aperture: night ? 2.0 : 16,
            shutterSpeed: night ? 1 / 30 : 1 / 125,
            sensitivity: night ? iso : 100,
          );
        }
        final (eye, look, _) = _still();
        return OrblitCamera(
          position: eye,
          target: look,
          // The long lens the still is framed for.
          fieldOfView: 34,
          // Wide open by day as well, for a background out of focus. f/2.8
          // at a four-thousandth lets in what f/16 at a hundred-and-twenty-
          // fifth does, so the exposure is the same and only the depth of
          // field changes.
          aperture: night ? 2.0 : 2.8,
          shutterSpeed: night ? 1 / 30 : 1 / 4000,
          sensitivity: night ? iso : 100,
        );
      }(),
      objects: [model],
      lights: lights,
      // A photograph of a real sky, prefiltered — the reflection in its mip
      // chain and the diffuse in its harmonics. Only by day: this is a bridge
      // at noon, and using it at night would light the street with sunshine.
      environment: night || !hasEnvironment
          ? const OrblitEnvironment()
          : OrblitEnvironment(
              radiance: radiance,
              skybox: skyboxMap,
              intensity: 30000,
            ),
      sky: night
          ? OrblitSky(
              zenith: linearOf(const Color(0xFF0B1224)),
              horizon: linearOf(const Color(0xFF243046)),
              // Not zero — a night sky still casts light, and shadows with
              // nothing in them read as holes. But close to it: this is the
              // sky's own glow, not a stage wash, and at anything like a
              // hundred lux it stops being night.
              ambient: 12,
              showBody: false,
            )
          : OrblitSky(
              zenith: linearOf(const Color(0xFF4E86C8)),
              horizon: linearOf(const Color(0xFFBFD4E8)),
              // Both off when there is an environment. A procedural sky and a
              // photographed one are two answers to the same question and the
              // procedural one wins, so leaving it on would hide the very
              // thing it was fetched for — and light the scene twice.
              ambient: hasEnvironment ? 0 : 22000,
              drawn: !hasEnvironment,
            ),
      pipeline: OrblitPipeline(
        // Holds the frame rate rather than the pixel count. What is in view
        // changes enormously as the walk turns — a wall a metre away, or a
        // hundred and seventy metres of street — and a fixed resolution means
        // the cost changes with it. A frame arriving at an uneven rate judders
        // however smooth the camera's own motion is, and this example exists
        // to be looked at while it moves.
        resolution: OrblitResolution(adaptive: true, minScale: 0.6),
        shadows: OrblitShadows(
          kind: OrblitShadowKind.soft,
          // Three at a thousand, not four at two thousand, which is what this
          // asked for before anybody measured it. Four cascades of two
          // thousand square is sixteen million shadow texels redrawn every
          // frame; with a fixed camera looking down the street it came to
          // thirty-eight milliseconds, and the fourth cascade covers ground
          // nothing is ever close enough to see the shadows on.
          cascades: 3,
          mapSize: 1024,
          // A hundred and seventy metres of street, so the shadows are told
          // to reach across it rather than left at the default.
          distance: 120,
          // Contact shadows are off here, and it is the single biggest thing
          // in the frame: twenty-four milliseconds on their own. They resolve
          // where a chair leg meets the cobbles, which is worth having in a
          // room and is not worth a third of the frame in a street nobody
          // stands still in. The interior keeps them.
          softness: 1.2,
        ),
        // Four samples. A street full of railings, shutters and thin lamp
        // posts is nothing but edges, and edges are what a single sample
        // makes a mess of.
        samples: 4,
      ),
      // Bloom at night only — it is what makes a small bright bulb read as a
      // light rather than as a white dot, and in daylight it only fogs the
      // image. Occlusion always: it is the cheapest stand-in for the contact
      // darkening that bounced light would give, and without it everything
      // sits on the ground rather than in it.
      post: OrblitPostProcess(
        // Temporal, not FXAA. It resolves an edge by sampling it in different
        // places on successive frames, which is why it is the best-looking of
        // the three and why it smears when something moves fast. A camera
        // walking at one and a third metres a second is exactly the case it
        // is good at, and a street of railings and shutters and thin lamp
        // posts is nothing but the edges it fixes.
        antiAliasing: AntiAliasing.temporal,
        bloom: OrblitBloom(enabled: night, strength: 0.22, levels: 7),
        // The cheapest stand-in for the contact darkening that bounced light
        // would give: without it everything sits on the ground rather than in
        // it.
        occlusion: OrblitOcclusion(enabled: true, quality: 2, radius: 0.4),
        // Wet cobbles and shop glass. Screen-space, so it can only reflect
        // what is already on screen — which is most of what a street reflects
        // anyway, since the thing above a pavement is usually the building
        // across from it.
        reflections: OrblitReflections(enabled: true, maxDistance: 6),
        // For the still only: the Vespa sharp and the street behind it soft.
        // The blur is the lens's own, from the aperture the still is shot
        // at, made a little stronger than life. The caps are what stop the
        // far end of the street dissolving; at the default of one pixel they
        // stopped the blur from showing at all.
        depthOfField: OrblitDepthOfField(
          enabled: !walking,
          focusDistance: walking ? 10 : _still().$3,
          blurScale: 3,
          maxForeground: 12,
          maxBackground: 16,
        ),
        // ACES rather than the plain filmic curve. More contrast and more
        // saturation, and the transform most films are graded through — which
        // matters most at night, where the difference between a lamp and the
        // dark it stands in is the whole picture.
        grading: OrblitGrading(
          enabled: true,
          toneMapping: ToneMapping.aces,
          contrast: night ? 1.06 : 1.0,
        ),
      ),
    );
  }

  @override
  Widget settings(BuildContext context, VoidCallback changed) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Toggle(
          label: 'Walk',
          value: walking,
          note: walking ? 'On foot, looking around' : 'Still, by the Vespa',
          onChanged: (value) {
            walking = value;
            changed();
          },
        ),
        Toggle(
          label: 'Night',
          value: night,
          note: night
              ? '${fixtures.length} point lights'
              : 'One sun at 100,000 lux',
          onChanged: (value) {
            night = value;
            changed();
          },
        ),
        Toggle(
          label: 'Festoon',
          value: festoon,
          note: 'The small coloured bulbs',
          enabled: night,
          onChanged: (value) {
            festoon = value;
            changed();
          },
        ),
        if (night) ...[
          const SizedBox(height: 8),
          Text(
            'Film speed — ISO ${iso.round()}',
            style: const TextStyle(fontSize: 12),
          ),
          Slider(
            value: iso,
            min: 100,
            max: 6400,
            onChanged: (value) {
              iso = value;
              changed();
            },
          ),
          const Text(
            'At ISO 100 this street is black. The lamps have not changed; '
            'the camera has.',
            style: TextStyle(fontSize: 11, height: 1.4),
          ),
        ],
      ],
    );
  }

  @override
  String get code => '''
// The fixtures come out of the scene's own emissive geometry, so the lights
// stand where the artist put the lamps.
for (final fixture in fixtures)
  OrblitLight(
    kind: OrblitLightKind.point,
    position: fixture.at,
    intensity: 2400,       // lumens — a street lamp
    falloffRadius: 14,     // metres
    castShadows: false,    // a hundred shadow casters is not a thing
  ),

// And the sky is still a light, even at night.
OrblitSky(zenith: Color(0xFF0B1224), horizon: Color(0xFF243046), ambient: 120)
''';
}

/// The room inside, which is the harder case.
///
/// Your third and fourth reference images. An interior is where a real-time
/// renderer is most obviously not a path tracer: almost none of the light in
/// a room like this arrives straight from a bulb, it arrives off the walls
/// and the ceiling. Filament has image-based lighting and screen-space
/// occlusion; it does not have bounced light. What that costs is visible
/// here and nowhere else in this gallery, which is the reason to have it.
class BistroInteriorExample extends BistroExample {
  BistroInteriorExample();

  @override
  Downloadable? get needs => const Downloadable(
    what: 'the Bistro interior',
    size: 'about 1.4 GB',
    from: 'Amazon Lumberyard Bistro, via ORCA',
    licence: 'CC BY 4.0',
    command: ['tool/fetch_bistro.sh', 'interior'],
  );

  @override
  String get name => 'Bistro interior';

  @override
  String get blurb =>
      'The room, and the one place the absence of bounced light shows.';

  @override
  String get asset => wine ? 'BistroInterior_Wine' : 'BistroInterior';

  @override
  ViewPoint get viewpoint =>
      const ViewPoint(distance: 9, pitch: 0.06, height: 1.7, yaw: 0.4);

  bool wine = false;
  double ambient = 400;

  @override
  OrblitScene scene(OrblitCamera camera, double seconds) {
    final lights = <OrblitLight>[];
    var key = 100;

    for (final fixture in fixtures) {
      final fitting = _fittings[fixture.kind];
      if (fitting == null) continue;
      lights.add(
        OrblitLight(
          key: key++,
          kind: OrblitLightKind.point,
          position: fixture.at,
          colour: linearOf(fitting.colour),
          intensity: fitting.lumens,
          falloffRadius: fitting.reach,
          castShadows: false,
        ),
      );
    }

    // If the room has no fixtures of its own, light it the way the reference
    // does: warm pendants over the tables, at the height they hang.
    if (lights.isEmpty) {
      for (var i = 0; i < 6; i++) {
        lights.add(
          OrblitLight(
            key: key++,
            kind: OrblitLightKind.point,
            position: Vector3(-4.0 + i * 1.8, 2.6, i.isEven ? -1.2 : 1.4),
            colour: linearOf(const Color(0xFFFFD9A8)),
            intensity: 900,
            falloffRadius: 6,
            castShadows: i == 0,
          ),
        );
      }
    }

    return OrblitScene(
      camera: camera,
      objects: [model],
      lights: lights,
      // Standing in for the light this renderer will not bounce. In a real
      // room the walls are half the lighting; here the ambient is the only
      // thing filling a shadow, so it is a dial rather than a constant.
      sky: OrblitSky(
        zenith: linearOf(const Color(0xFF2A1D18)),
        horizon: linearOf(const Color(0xFF3A2A20)),
        ambient: ambient,
        showBody: false,
        drawn: false,
      ),
      pipeline: OrblitPipeline(
        shadows: OrblitShadows(
          kind: OrblitShadowKind.soft,
          mapSize: 2048,
          // A room rather than a street, so the shadows only have to reach
          // across it — but not zero, which covers nothing at all.
          distance: 30,
          contact: true,
        ),
      ),
      post: OrblitPostProcess(
        antiAliasing: AntiAliasing.temporal,
        bloom: OrblitBloom(enabled: true, strength: 0.1),
        // Occlusion earns its place indoors more than anywhere. It is the
        // cheapest approximation of the contact darkening that bounced light
        // would give for free, and indoors bounced light is most of the
        // lighting.
        occlusion: OrblitOcclusion(enabled: true, quality: 2, radius: 0.4),
        grading: OrblitGrading(enabled: true, toneMapping: ToneMapping.aces),
      ),
    );
  }

  @override
  Widget settings(BuildContext context, VoidCallback changed) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Toggle(
          label: 'Wine',
          value: wine,
          note: 'The variant with the bottles and glasses',
          onChanged: (value) {
            wine = value;
            changed();
          },
        ),
        const SizedBox(height: 8),
        Text(
          'Ambient — ${ambient.round()} lux',
          style: const TextStyle(fontSize: 12),
        ),
        Slider(
          value: ambient,
          min: 0,
          max: 2000,
          onChanged: (value) {
            ambient = value;
            changed();
          },
        ),
      ],
    );
  }

  @override
  String get code => '''
// Indoors, the ambient is doing the job bounced light would do. Drag it to
// nothing and the shadows go black, which is exactly what a renderer without
// global illumination looks like when nothing stands in for it.
OrblitSky(
  zenith: linearOf(const Color(0xFF2A1D18)),
  ambient: 400,     // lux
  drawn: false,     // lighting only; there is no sky to see from in here
)

// And occlusion, the cheapest approximation of contact darkening.
OrblitPostProcess(occlusion: OrblitOcclusion(enabled: true))
''';
}

/// Shown over the viewport when the scene has not been fetched.
class _Missing extends StatelessWidget {
  const _Missing();

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Container(
        constraints: const BoxConstraints(maxWidth: 460),
        padding: const EdgeInsets.all(20),
        decoration: BoxDecoration(
          color: const Color(0xE6161A21),
          borderRadius: BorderRadius.circular(10),
        ),
        child: const Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              'The Bistro is not here yet.',
              style: TextStyle(fontSize: 15, fontWeight: FontWeight.w600),
            ),
            SizedBox(height: 8),
            Text(
              'It is half a gigabyte and belongs to somebody else, so it is '
              'fetched rather than shipped:\n\n'
              './tool/fetch_bistro.sh exterior\n\n'
              'Set ORBLIT_BISTRO if your checkout is somewhere else.',
              textAlign: TextAlign.center,
              style: TextStyle(fontSize: 12, height: 1.5),
            ),
          ],
        ),
      ),
    );
  }
}

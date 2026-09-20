import 'dart:math' as math;
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:vector_math/vector_math_64.dart' hide Colors;

import '../example.dart';
import 'surface.dart' show linearOf;

/// Which generated cloud to show, where no capture has been given.
enum SplatShape {
  /// A tree in a clearing: what a capture of somewhere looks like.
  garden('Garden'),

  /// A striped ring, whose two sides are different colours: what a capture
  /// of somewhere does not look like, and the only one of the two that shows
  /// plainly what the sort is for.
  ring('Ring');

  const SplatShape(this.label);

  final String label;
}

/// A place made of soft blobs rather than of surfaces.
///
/// A Gaussian splat capture is millions of small coloured ellipsoids, fitted
/// to photographs until, seen from where the photographs were taken, they
/// add up to the place. Each is drawn as an ellipse the size its ellipsoid
/// projects to, fading out as a Gaussian, blended over whatever is behind it.
///
/// Nothing needs downloading here: the cloud is generated, either way round.
/// [SplatShape.garden] is a tree in a clearing, built the way a capture is
/// put together rather than the way a modeller would build a tree, which is
/// what a million splats are actually for. [SplatShape.ring] is a torus of
/// flat, half-transparent discs, striped so that its near and far sides are
/// different colours: it is no place at all, and it is the one that teaches.
/// Where the two sides overlap on screen, the order they are blended in
/// decides which colour wins, so turning the sort off on the ring shows
/// exactly what a splat renderer that does not sort gets wrong — on the
/// garden the same fault is only a haze.
///
/// The pillar is solid geometry standing in the ring, to show the other rule:
/// splats test against the depth of the solid scene and never write their
/// own, so a wall hides a cloud and a cloud never hides a wall. It stands in
/// the ring and nowhere else — in the garden it would only be a monolith in
/// a meadow, and the ground the garden already stands on makes the same
/// point more quietly.
///
/// And the cloud is held to what the device can carry: no more splats than
/// its profile budgets, harmonics no higher than its degree, and a coarse
/// sort where it is low tier — which is how one scene is written once for a
/// desktop, a phone and a browser.
class SplatsExample extends Example {
  SplatsExample();

  @override
  String get name => 'Gaussian splats';

  @override
  ExampleSection get section => ExampleSection.content;

  @override
  String get blurb =>
      'A cloud of 3D Gaussians, drawn as ellipses, sorted back to front off '
      'the render thread and held to what the device can carry.';

  @override
  ViewPoint get viewpoint =>
      // Far enough back and high enough to hold the whole tree, which is the
      // taller of the two clouds by a good margin, and no further: the tree
      // is the picture and the lawn around it is not.
      const ViewPoint(yaw: 0.6, pitch: 0.13, distance: 8.0, height: 0.7);

  /// How many splats the generated ring has.
  int count = 300000;

  /// Whether they are sorted. Off is wrong on purpose, for comparison.
  bool sorted = true;

  /// Whether the solid pillar stands in the ring. It is only ever drawn
  /// there; the garden has no use for it.
  bool pillar = true;

  double opacity = 1;
  double brightness = 1;

  /// Which of the two generated clouds to show.
  SplatShape shape = SplatShape.garden;

  /// A real capture to show instead, `.ply` or `.splat`.
  String? path;

  /// How many spherical-harmonic bands of a capture's colour to read.
  ///
  /// Only reaches a `.ply` from [path]. The generated ring is packed into the
  /// compact 32-byte records, which have no room for any, so it is the same
  /// colour from every side however this is set.
  int harmonics = 2;

  /// Whether the device's own budgets apply: no more splats than
  /// [OrblitDeviceProfile.splatBudget], harmonics no higher than its degree,
  /// and a coarse sort where it is low tier.
  bool deviceLimits = true;

  /// A limit of the example's own, which wins over the device's. Null for
  /// none.
  int? limit;

  /// Whether to sort coarsely whatever the device says.
  bool coarseOrder = false;

  Uint8List? _data;
  int _builtFor = -1;
  SplatShape? _shapeFor;
  int? _limitFor;
  int _revision = 0;

  static const _counts = {'100k': 100000, '300k': 300000, '1M': 1000000};

  int? get _limit => limit ?? (deviceLimits ? device?.splatBudget : null);

  int get _harmonics {
    final profile = device;
    if (!deviceLimits || profile == null) return harmonics;
    return math.min(harmonics, profile.harmonicDegree);
  }

  bool get _coarse =>
      coarseOrder || (deviceLimits && (device?.coarseSplatOrder ?? false));

  @override
  OrblitScene scene(OrblitCamera camera, double seconds) {
    final file = path;
    final wanted = _limit;
    if (file == null &&
        (_builtFor != count || _shapeFor != shape || _limitFor != wanted)) {
      if (_builtFor != count || _shapeFor != shape) {
        _data = switch (shape) {
          SplatShape.garden => garden(count),
          SplatShape.ring => ring(count),
        };
        _builtFor = count;
        _shapeFor = shape;
      }
      // A limit is applied as a cloud is read, so a cloud held in memory has
      // to be sent again for a new one to reach it.
      _limitFor = wanted;
      _revision++;
    }

    return OrblitScene(
      objects: [
        OrblitObject(
          key: 1,
          transform: Matrix4.identity()
            ..setTranslation(Vector3(0, -0.5, 0))
            // Wide enough that its far edge is past the horizon: the
            // garden's own ground thins out into this rather than stopping
            // on a slab with a rim, and a cloud lit for daylight wants
            // ground under it rather than a hole. Lit, it comes out the
            // colour the far grass does -- which is the point of the number,
            // and why it looks nothing like grass written down.
            ..scaleByDouble(160, 0.1, 160, 1),
          colour: linearOf(const Color(0xFFABC960)),
        ),
        if (pillar && shape == SplatShape.ring)
          OrblitObject(
            key: 2,
            transform: Matrix4.identity()
              ..setTranslation(Vector3(0.9, 0.7, 1.2))
              ..scaleByDouble(0.3, 2.4, 0.3, 1),
            colour: linearOf(const Color(0xFFCFC7B6)),
          ),
      ],
      splats: [
        OrblitSplats(
          key: 1,
          path: file,
          data: file == null ? _data : null,
          // A capture comes out of structure-from-motion with y pointing
          // down, as the first photograph's camera had it, so one read from a
          // file is turned the right way up.
          transform: file == null ? null : Matrix4.rotationX(math.pi),
          opacity: opacity,
          brightness: brightness,
          sorted: sorted,
          harmonics: _harmonics,
          limit: wanted,
          coarseOrder: _coarse,
          revision: _revision,
        ),
      ],
      lights: [
        OrblitLight(
          key: 1,
          kind: OrblitLightKind.directional,
          direction: Vector3(-0.4, -0.8, -0.45)..normalize(),
          intensity: 60000,
          colour: Vector3(1, 0.97, 0.92),
        ),
      ],
      sky: OrblitSky(colour: linearOf(const Color(0xFF12151B)), ambient: 9000),
      // Straight through rather than filmic. A capture's colours are the
      // photographs' own, already graded by whatever camera took them, and a
      // second tone curve over them only greys them out.
      post: OrblitPostProcess(
        grading: OrblitGrading(toneMapping: ToneMapping.linear),
      ),
      camera: camera,
    );
  }

  /// A ring of [count] flat discs on a torus, as compact splat records.
  ///
  /// Discs rather than balls, oriented along the surface, which is what a
  /// trained capture's splats mostly are: flattened onto whatever surface they
  /// were fitted to. Half transparent, so both sides of the ring show where
  /// they overlap, and generated in order round the ring, so that drawing
  /// them unsorted is visibly wrong rather than accidentally right.
  static Uint8List ring(int count, {int seed = 7}) {
    final random = math.Random(seed);
    final positions = Float32List(count * 3);
    final scales = Float32List(count * 3);
    final colours = Float32List(count * 4);
    final rotations = Float32List(count * 4);

    const major = 1.6;
    const minor = 0.55;
    // Tilted, so the camera sees into the ring and across it at once.
    const tilt = 0.5;
    final ct = math.cos(tilt), st = math.sin(tilt);

    for (var i = 0; i < count; i++) {
      // In order round the ring, jittered.
      final u = (i + random.nextDouble()) / count * 2 * math.pi;
      final v = random.nextDouble() * 2 * math.pi;
      final cu = math.cos(u), su = math.sin(u);
      final cv = math.cos(v), sv = math.sin(v);

      // Position and frame on an untilted torus lying in the xz plane.
      final ring = major + minor * cv;
      var p = Vector3(ring * cu, minor * sv, ring * su);
      var along = Vector3(-su, 0, cu); // round the ring
      var around = Vector3(-sv * cu, cv, -sv * su); // round the tube
      var normal = Vector3(cv * cu, sv, cv * su);

      // Tilted about x, and lifted off the floor.
      Vector3 tilted(Vector3 a) =>
          Vector3(a.x, a.y * ct - a.z * st, a.y * st + a.z * ct);
      p = tilted(p)..y += 1.0;
      along = tilted(along);
      around = tilted(around);
      normal = tilted(normal);

      positions[i * 3] = p.x;
      positions[i * 3 + 1] = p.y;
      positions[i * 3 + 2] = p.z;

      // A few centimetres across and a few millimetres thick.
      final size = 0.018 + random.nextDouble() * 0.02;
      scales[i * 3] = size;
      scales[i * 3 + 1] = size * (0.6 + random.nextDouble() * 0.4);
      scales[i * 3 + 2] = 0.003;

      final q = Quaternion.fromRotation(Matrix3.columns(along, around, normal));
      rotations[i * 4] = q.w;
      rotations[i * 4 + 1] = q.x;
      rotations[i * 4 + 2] = q.y;
      rotations[i * 4 + 3] = q.z;

      // Stripes round the ring and bands round the tube, in two colours
      // that are easy to tell apart when one is seen through the other.
      final stripe = math.sin(u * 9) * math.sin(v * 2 + 0.4) > 0;
      final shade = 0.85 + random.nextDouble() * 0.15;
      final light = 0.6 + 0.4 * (0.5 + 0.5 * sv);
      colours[i * 4] = (stripe ? 0.95 : 0.12) * shade * light;
      colours[i * 4 + 1] = (stripe ? 0.55 : 0.70) * shade * light;
      colours[i * 4 + 2] = (stripe ? 0.18 : 0.85) * shade * light;
      colours[i * 4 + 3] = 0.55;
    }

    return OrblitSplats.pack(
      positions: positions,
      scales: scales,
      colours: colours,
      rotations: rotations,
    );
  }

  /// A tree in a clearing, as a cloud of splats.
  ///
  /// Nothing is downloaded for this and nothing was photographed: it is
  /// generated, like [ring] is. What it borrows from a real capture is the
  /// way one is put together, because that is what makes a capture look like
  /// one. Every splat is a flattened ellipsoid lying along the surface it
  /// belongs to. Nothing has an edge — a leaf is not a leaf but four or five
  /// soft discs, and the trunk is not a cylinder but a few thousand discs
  /// lying where a cylinder was. And the light is in the colours rather than
  /// on them: a capture is radiance already, so the sun here is arithmetic
  /// done once per splat while this is built, and the scene's own light never
  /// touches it.
  static Uint8List garden(int count, {int seed = 3}) {
    final random = math.Random(seed);
    final positions = Float32List(count * 3);
    final scales = Float32List(count * 3);
    final colours = Float32List(count * 4);
    final rotations = Float32List(count * 4);
    var at = 0;

    // Every record is written below, but a record that somehow was not would
    // carry the quaternion (0, 0, 0, 0), which is not a rotation and has no
    // sensible normalisation. Identity costs one pass and rules it out.
    for (var i = 0; i < count; i++) {
      rotations[i * 4] = 1;
    }

    /// Where the light came from, on the day this was not photographed.
    final sun = Vector3(-0.36, 0.86, 0.36)..normalize();

    Vector3 unit() {
      final z = random.nextDouble() * 2 - 1;
      final a = random.nextDouble() * 2 * math.pi;
      final r = math.sqrt(math.max(0, 1 - z * z));
      return Vector3(r * math.cos(a), z, r * math.sin(a));
    }

    /// Two directions across [n], for a splat's own axes to lie along.
    (Vector3, Vector3) across(Vector3 n, Vector3 hint) {
      var u = hint - n * hint.dot(n);
      if (u.length2 < 1e-9) {
        u = (n.y.abs() < 0.9 ? Vector3(0, 1, 0) : Vector3(1, 0, 0)).cross(n);
      }
      u.normalize();
      return (u, n.cross(u)..normalize());
    }

    /// One splat: a disc at [p], facing [n], stretched along [hint].
    void put(
      Vector3 p,
      Vector3 n,
      Vector3 hint,
      double long,
      double wide,
      double thin,
      Vector3 rgb,
      double alpha,
    ) {
      if (at >= count) return;
      final (u, v) = across(n, hint);
      positions[at * 3] = p.x;
      positions[at * 3 + 1] = p.y;
      positions[at * 3 + 2] = p.z;
      scales[at * 3] = long;
      scales[at * 3 + 1] = wide;
      scales[at * 3 + 2] = thin;
      final q = Quaternion.fromRotation(Matrix3.columns(u, v, n));
      rotations[at * 4] = q.w;
      rotations[at * 4 + 1] = q.x;
      rotations[at * 4 + 2] = q.y;
      rotations[at * 4 + 3] = q.z;
      colours[at * 4] = rgb.x;
      colours[at * 4 + 1] = rgb.y;
      colours[at * 4 + 2] = rgb.z;
      colours[at * 4 + 3] = alpha;
      at++;
    }

    /// [base] under the sun, facing [n], with [shade] of the sky reaching it.
    ///
    /// A key from the sun, a fill from the sky that favours whatever faces
    /// up, and a floor under both so that nothing in shadow goes to black —
    /// which is what the bounce off a bright ground does outdoors, and what
    /// its absence makes a generated cloud look like.
    Vector3 lit(Vector3 base, Vector3 n, double shade) {
      final key = math.max(0.0, n.dot(sun)) * 1.45;
      final sky = (0.5 + 0.5 * n.y) * 0.55;
      return base * ((key + sky) * shade + 0.22);
    }

    // ---- the tree ----------------------------------------------------
    //
    // A skeleton first, then splats scattered over it. Grown from the foot
    // of the trunk, each limb three quarters the length of the one it came
    // from, and a bundle of leaves left wherever the growing stops.

    final limbs = <(Vector3, Vector3, double)>[];
    final bunches = <(Vector3, double)>[];

    void grow(Vector3 from, Vector3 way, double length, double radius,
        int depth) {
      final to = from + way * length;
      limbs.add((from, to, radius));
      if (depth == 0) {
        bunches.add((to + way * 0.14, 0.20 + random.nextDouble() * 0.14));
        return;
      }
      for (var i = 0; i < (depth > 1 ? 3 : 2); i++) {
        // A trunk divides narrowly and twigs splay, which is the difference
        // between a tree and a bush.
        final lean = depth > 2
            ? 0.20 + random.nextDouble() * 0.22
            : 0.40 + random.nextDouble() * 0.45;
        final turn = random.nextDouble() * 2 * math.pi;
        final (u, v) = across(way, Vector3(1, 0, 0));
        final next =
            (way * math.cos(lean) +
                  (u * math.cos(turn) + v * math.sin(turn)) * math.sin(lean))
              ..normalize();
        // And everything reaches for the light, so nothing grows downwards.
        next.y += 0.22;
        next.normalize();
        grow(
          to,
          next,
          length * (0.70 + random.nextDouble() * 0.10),
          radius * 0.66,
          depth - 1,
        );
      }
    }

    const floor = -0.44;
    // Four rounds of splitting off a trunk half again as long as the first
    // branch: a clear stem with a crown over it, rather than the low bundle
    // that fewer, longer, floppier limbs come out as.
    grow(Vector3(0.05, floor, 0.1), Vector3(-0.03, 1, -0.02)..normalize(),
        1.5, 0.17, 4);

    // ---- how the splats are shared out --------------------------------
    //
    // Most of them go on the leaves, because that is where a real capture
    // spends them too: a surface needs as many splats as it has detail, and
    // a canopy is nothing but detail.
    final onGround = (count * 0.32).round();
    final onBark = (count * 0.11).round();
    final onBushes = (count * 0.08).round();
    final onFlowers = (count * 0.015).round();
    final onLeaves = count - onGround - onBark - onBushes - onFlowers;

    // ---- the ground ----------------------------------------------------
    //
    // Flat discs for the earth and upright slivers for the grass over it,
    // darkened under the tree, where less of the sky reaches.
    for (var placed = 0; placed < onGround; placed++) {
      final a = random.nextDouble() * 2 * math.pi;
      // Straight in the unit interval rather than its root, which would
      // spread them evenly over the area. A capture is densest where the
      // photographs were taken, and they were taken around the tree: this
      // packs the middle and lets the lawn thin out on its way to the rim,
      // which is both what one looks like and where the splats are worth
      // spending.
      final r = 7.5 * random.nextDouble();
      final p = Vector3(math.cos(a) * r, floor, math.sin(a) * r);
      final shadow = 1 - 0.48 * math.exp(-(r * r) / 2.2);
      final blade = random.nextDouble() < 0.55;
      // Thinning out towards the rim rather than ending at it. A capture is
      // only of where the photographs reached, and it gives out at the edge
      // instead of being cut off in a circle -- and a circle is exactly what
      // a disc of splats on a flat floor otherwise looks like.
      final edge = (1 - (r - 4.6) / 2.9).clamp(0.0, 1.0);
      if (random.nextDouble() > edge) {
        // Try again somewhere else rather than going one splat short: the
        // shares below are exact, and the leaves are owed what is left.
        placed--;
        continue;
      }

      final dry = random.nextDouble();
      final base = Vector3(0.105, 0.215, 0.055) * (1 - dry) +
          Vector3(0.300, 0.280, 0.090) * dry;

      if (blade) {
        final way = Vector3(random.nextDouble() - 0.5, 0, random.nextDouble() - 0.5)
          ..normalize();
        final tall = 0.045 + random.nextDouble() * 0.055;
        put(
          p + Vector3(0, tall * 0.5, 0),
          way,
          Vector3(0, 1, 0),
          tall,
          0.006 + random.nextDouble() * 0.005,
          0.003,
          // A blade catches the sky along its length however it is turned,
          // so it is lit as though it faced up rather than sideways.
          lit(base * 1.25, Vector3(0, 1, 0), shadow),
          0.82 + random.nextDouble() * 0.16,
        );
      } else {
        final n = (Vector3(0, 1, 0) + unit() * 0.22)..normalize();
        put(
          p,
          n,
          unit(),
          0.05 + random.nextDouble() * 0.05,
          0.04 + random.nextDouble() * 0.04,
          0.004,
          lit(base * 0.85, n, shadow),
          0.88 + random.nextDouble() * 0.12,
        );
      }
    }

    // ---- the bark --------------------------------------------------------
    //
    // Spread over the limbs by area, so the trunk gets what the trunk is
    // worth and a twig does not get the same as it.
    var area = 0.0;
    for (final (from, to, radius) in limbs) {
      area += (to - from).length * radius;
    }
    for (var i = 0; i < onBark; i++) {
      var pick = random.nextDouble() * area;
      var chosen = limbs.first;
      for (final limb in limbs) {
        pick -= (limb.$2 - limb.$1).length * limb.$3;
        if (pick <= 0) {
          chosen = limb;
          break;
        }
      }
      final (from, to, radius) = chosen;
      final way = (to - from)..normalize();
      final (u, v) = across(way, Vector3(1, 0, 0));
      final t = random.nextDouble();
      final turn = random.nextDouble() * 2 * math.pi;
      final out = (u * math.cos(turn) + v * math.sin(turn))..normalize();
      // Narrower towards the tip, and roughened, so the trunk has a grain
      // rather than a polish.
      final r = radius * (1 - 0.35 * t) * (0.92 + random.nextDouble() * 0.16);
      final p = from + (to - from) * t + out * r;
      final dark = 0.72 + random.nextDouble() * 0.28;
      put(
        p,
        out,
        way,
        0.030 + random.nextDouble() * 0.030,
        0.012 + random.nextDouble() * 0.010,
        0.004,
        lit(Vector3(0.265, 0.180, 0.115) * dark, out, 0.95),
        0.90 + random.nextDouble() * 0.10,
      );
    }

    // ---- the leaves ------------------------------------------------------
    //
    // A shell rather than a ball: the inside of a bunch of leaves is not
    // seen, and splats spent there are splats not spent on its edge, which
    // is the part that has to hold up against the sky.
    for (var i = 0; i < onLeaves; i++) {
      final (centre, radius) = bunches[random.nextInt(bunches.length)];
      final way = unit();
      final deep = 0.58 + 0.42 * math.pow(random.nextDouble(), 0.33).toDouble();
      final p = centre + way * (radius * deep) + unit() * 0.035;
      // Leaves face every way, but a little more outwards than not.
      final n = (way * 0.55 + unit())..normalize();

      final autumn = random.nextDouble();
      final green = Vector3(0.075, 0.215, 0.045) +
          Vector3(0.130, 0.185, 0.050) * random.nextDouble();
      final rust = Vector3(0.330, 0.160, 0.040);
      final base = autumn > 0.94 ? rust : green;
      // Deeper in the bunch is darker, which is the only shadow a cloud with
      // no lighting gets to have, and the one that makes it read as thick.
      final inside = 0.30 + 0.80 * deep;
      put(
        p,
        n,
        unit(),
        0.028 + random.nextDouble() * 0.024,
        0.020 + random.nextDouble() * 0.018,
        0.004,
        lit(base, n, inside),
        0.55 + random.nextDouble() * 0.35,
      );
    }

    // ---- the undergrowth -------------------------------------------------
    final bushes = [
      for (var i = 0; i < 5; i++)
        (
          Vector3(
            (random.nextDouble() * 2 - 1) * 2.6,
            floor,
            (random.nextDouble() * 2 - 1) * 2.6,
          ),
          0.22 + random.nextDouble() * 0.20,
        ),
    ];
    for (var i = 0; i < onBushes; i++) {
      final (foot, radius) = bushes[random.nextInt(bushes.length)];
      // Only the top half of the shell: the underside of a bush is on the
      // ground and nothing sees it.
      final way = unit();
      way.y = way.y.abs();
      final deep = 0.55 + 0.45 * math.pow(random.nextDouble(), 0.33).toDouble();
      final p = foot + Vector3(0, radius * 0.75, 0) + way * (radius * deep);
      final n = (way * 0.6 + unit())..normalize();
      put(
        p,
        n,
        unit(),
        0.026 + random.nextDouble() * 0.022,
        0.018 + random.nextDouble() * 0.016,
        0.004,
        lit(
          Vector3(0.060, 0.155, 0.048) +
              Vector3(0.095, 0.135, 0.030) * random.nextDouble(),
          n,
          0.34 + 0.66 * deep,
        ),
        0.62 + random.nextDouble() * 0.30,
      );
    }

    // ---- the flowers -----------------------------------------------------
    //
    // Two hundredths of the cloud, and the first thing anybody's eye goes
    // to. A capture of a garden always has some.
    final heads = [
      for (var i = 0; i < 40; i++)
        Vector3(
          (random.nextDouble() * 2 - 1) * 3.0,
          floor + 0.06 + random.nextDouble() * 0.09,
          (random.nextDouble() * 2 - 1) * 3.0,
        ),
    ];
    final petals = [
      Vector3(0.95, 0.72, 0.24),
      Vector3(0.92, 0.36, 0.30),
      Vector3(0.88, 0.84, 0.72),
      Vector3(0.62, 0.46, 0.86),
    ];
    for (var i = 0; i < onFlowers; i++) {
      final head = heads[random.nextInt(heads.length)];
      final n = (Vector3(0, 1, 0) + unit() * 0.5)..normalize();
      put(
        head + unit() * 0.028,
        n,
        unit(),
        0.014 + random.nextDouble() * 0.012,
        0.012 + random.nextDouble() * 0.010,
        0.004,
        petals[random.nextInt(petals.length)] * (0.55 + 0.45 * random.nextDouble()),
        0.88 + random.nextDouble() * 0.12,
      );
    }

    return OrblitSplats.pack(
      positions: positions,
      scales: scales,
      colours: colours,
      rotations: rotations,
    );
  }

  /// What the device limits come to, in a sentence.
  String get _deviceNote {
    final profile = device;
    if (profile == null) {
      return 'Waiting for the renderer to say what this device can do.';
    }
    final name = profile.tier.name;
    final tier = '${name[0].toUpperCase()}${name.substring(1)}';
    if (!deviceLimits) {
      return '$tier-tier device, ignored: every splat, at the degree chosen '
          'below.';
    }
    return '$tier-tier device: at most ${_grouped(profile.splatBudget)} '
        'splats, harmonics to degree ${profile.harmonicDegree}, '
        '${profile.coarseSplatOrder ? 'a coarse' : 'a full'} sort.';
  }

  static String _grouped(int value) => value.toString().replaceAllMapped(
    RegExp(r'\B(?=(\d{3})+(?!\d))'),
    (_) => ',',
  );

  @override
  Widget settings(BuildContext context, VoidCallback changed) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      Toggle(
        label: 'Sort back to front',
        value: sorted,
        note: sorted
            ? 'Re-sorted off the render thread whenever the view moves, '
                  'leaving out what the camera cannot see.'
            : 'Drawn in the order generated: the far side paints over the '
                  'near one wherever they overlap.',
        onChanged: (value) {
          sorted = value;
          changed();
        },
      ),
      Toggle(
        label: 'Coarse sort',
        value: coarseOrder,
        note:
            'Sixteen bits of depth rather than thirty-two: half the passes, '
            'for splats nearly as far away as one another in either order.',
        onChanged: (value) {
          coarseOrder = value;
          changed();
        },
      ),
      Toggle(
        label: 'Device limits',
        value: deviceLimits,
        note: _deviceNote,
        onChanged: (value) {
          deviceLimits = value;
          changed();
        },
      ),
      Toggle(
        label: 'Solid pillar',
        value: pillar && shape == SplatShape.ring,
        // It is the ring's pillar. Nothing to turn on or off anywhere else.
        enabled: shape == SplatShape.ring,
        onChanged: (value) {
          pillar = value;
          changed();
        },
      ),
      Choice(
        label: 'Cloud',
        options: [for (final one in SplatShape.values) one.label],
        selected: shape.label,
        // Nothing to choose between with a capture loaded: the capture is
        // the cloud.
        onSelect: path != null
            ? null
            : (option) {
                shape = SplatShape.values.firstWhere(
                  (one) => one.label == option,
                );
                changed();
              },
      ),
      Choice(
        label: 'Splats',
        options: _counts.keys.toList(),
        selected: _counts.entries
            .firstWhere(
              (entry) => entry.value == count,
              orElse: () => _counts.entries.elementAt(1),
            )
            .key,
        onSelect: path != null
            ? null
            : (option) {
                count = _counts[option]!;
                changed();
              },
      ),
      Choice(
        label: 'Harmonics',
        options: const ['0', '1', '2', '3'],
        selected: '$harmonics',
        // Greyed out with no capture loaded, because there is nothing for it
        // to act on: the generated ring is packed into the compact records,
        // which carry a splat's colour and no bands at all.
        onSelect: path == null
            ? null
            : (option) {
                harmonics = int.parse(option);
                changed();
              },
      ),
      Setting(
        label: 'Opacity',
        value: opacity,
        min: 0,
        max: 1,
        onChanged: (value) {
          opacity = value;
          changed();
        },
      ),
      Setting(
        label: 'Brightness',
        value: brightness,
        min: 0,
        max: 2,
        onChanged: (value) {
          brightness = value;
          changed();
        },
      ),
    ],
  );

  @override
  String get code => '''
// What this device can carry, asked of the view once its renderer is up.
final device = await OrblitView.profileOf(viewport);

// A capture from a file: the reference trainer's .ply, or a compact .splat.
OrblitSplats(
  key: 1,
  path: 'garden.ply',
  // Structure-from-motion puts y down. Turned the right way up here.
  transform: Matrix4.rotationX(math.pi),
  // How much of the capture's view-dependent colour to read: 16 bytes a splat
  // for each degree, so a small device reads less of it.
  harmonics: device.harmonicDegree,
  // Keeps the most opaque and largest splats, and never sends the rest to the
  // GPU at all.
  limit: device.splatBudget,
  // Sixteen bits of depth where a full sort would lag a turning camera.
  coarseOrder: device.coarseSplatOrder,
)

// Or a cloud made in Dart, packed into the same 32-byte layout.
final data = OrblitSplats.pack(
  positions: positions, // three floats a splat, metres
  scales: scales,       // three standard deviations a splat, metres
  colours: colours,     // RGBA, nought to one; alpha is peak opacity
  rotations: rotations, // quaternions, (w, x, y, z)
);
OrblitSplats(key: 1, data: data, revision: revision)

// Sorted back to front whenever the camera moves — on a thread of the
// renderer's own, or a Web Worker in a browser — leaving out what the camera
// cannot see. Drawn after the solid scene, tested against its depth, never
// writing any.
''';
}

import 'dart:math' as math;
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_noise/orblit_noise.dart';
import 'package:orblit_stage/orblit_stage.dart';
import 'package:orblit_terrain/orblit_terrain.dart';
import 'package:vector_math/vector_math_64.dart' hide Colors;

import '../example.dart';
import 'surface.dart' show linearOf;

/// Ground as data, drawn as one grid.
///
/// Half a kilometre of hills is sixteen regions of heights, a texel every two
/// metres, sent to the renderer once. What draws them is the same small grid a few times over,
/// each copy twice the size of the one inside it and all of them centred on
/// the camera, raised by the heights on the GPU — so the ground is a handful
/// of draws however far it reaches, and moving the camera sends nothing.
///
/// Nothing here says where the rock goes. Every texel is left automatic, and
/// the renderer chooses between the two sets by how steep and how high the
/// ground is, so the sliders that change that choice change only settings.
///
/// Nothing says where the grass, the stones and the trees go either. They are
/// rules kept with the terrain — how thick, on which set, on what slope — and
/// a placer works out where each one stands, a region at a time, again only
/// where the ground changes. Each is a block, so a layer in a region is one
/// population: tens of thousands of tufts are a draw or two.
///
/// The box that drives round it stands on the ground by asking the terrain
/// where the ground is: the same heights, read the same way the renderer reads
/// them, with no physics involved.
class TerrainExample extends Example {
  TerrainExample() {
    _paintSets();
    _build();
  }

  @override
  String get name => 'Terrain';

  @override
  ExampleSection get section => ExampleSection.showcases;

  @override
  String get blurb =>
      'Hills from sixteen regions of heights, textured by slope and height, '
      'with grass, stones and trees scattered by rule.';

  @override
  ViewPoint get viewpoint =>
      const ViewPoint(distance: 120, pitch: 0.36, height: 14, yaw: 0.9);

  /// How high the tallest ridge can reach, in metres.
  double relief = 60;

  /// How quickly slope hands the ground over to rock.
  double steepness = 1.2;

  /// How quickly height does, per hundred metres.
  double altitude = 0.2;

  /// How hard the edge between grass and rock is.
  double sharpness = 0.87;

  /// Whether rock is laid from three sides, so cliffs are not smeared.
  bool triplanar = true;

  /// How many times the grid is drawn, each twice the size of the last.
  double rings = 6;

  /// Whether the box drives round.
  bool rover = true;

  /// Whether the grass, stones and trees are drawn.
  bool scatter = true;

  int _seed = 1;

  /// The ground, as data: what is drawn, and what the rover stands on.
  Terrain get terrain => _terrain;
  late Terrain _terrain;
  final Map<String, Uint8List> _pictures = {};

  /// Where the scatter stands, kept between frames so that only a region
  /// whose ground changed is placed again, and what it last drew.
  final ScatterPlacer placer = ScatterPlacer();
  List<OrblitPopulation> _scattered = const [];
  int _scatteredRevision = -1;

  /// Grass on the grass set, stones on the rock, and trees — a trunk and a
  /// crown, two layers on one seed so that they stand together — on the
  /// gentler, lower grass.
  static const List<ScatterLayer> _scatter = [
    ScatterLayer(
      name: 'grass',
      seed: 1,
      density: 0.5,
      sets: [1],
      maxSlope: 35,
      size: (0.4, 0.3, 0.4),
      minScale: 0.5,
      maxScale: 1.4,
      lean: 0.7,
      lift: -0.08,
      colour: 0x5E8C3A,
      colourVariation: 0.25,
      range: 70,
    ),
    ScatterLayer(
      name: 'stones',
      seed: 2,
      density: 0.03,
      sets: [0],
      size: (1.2, 0.7, 0.9),
      minScale: 0.4,
      maxScale: 1.6,
      lean: 1,
      lift: -0.25,
      colour: 0x8A8580,
      colourVariation: 0.2,
      range: 160,
      castShadows: true,
    ),
    ScatterLayer(
      name: 'trunks',
      seed: 3,
      density: 0.004,
      sets: [1],
      maxSlope: 22,
      maxHeight: 30,
      size: (0.45, 4, 0.45),
      minScale: 0.7,
      maxScale: 1.3,
      lift: -0.3,
      colour: 0x5A3E28,
      castShadows: true,
    ),
    ScatterLayer(
      name: 'crowns',
      seed: 3,
      density: 0.004,
      sets: [1],
      maxSlope: 22,
      maxHeight: 30,
      size: (2.6, 3.4, 2.6),
      minScale: 0.7,
      maxScale: 1.3,
      // The trunk's top, less its sinking and a little more, so the crown
      // sits over the trunk rather than on it.
      lift: 3.2,
      colour: 0x2F5A2A,
      colourVariation: 0.15,
      castShadows: true,
    ),
  ];

  /// Texels a region is across, the regions each way from the middle, and
  /// metres between texels.
  static const int _regionSize = 64;
  static const int _regionsOut = 2;
  static const double _spacing = 2;

  /// How far the ground reaches each way from the middle, in metres.
  static const double reach = _regionSize * _regionsOut * _spacing;

  /// Texels a set's pictures are across.
  static const int _pictureSize = 128;

  /// Makes the ground again from [_seed] and [relief]. Every region it
  /// touches gets a new revision, so the next frame sends them all.
  void _build() {
    _terrain = Terrain(
      regionSize: _regionSize,
      spacing: _spacing,
      sets: const [
        TerrainSet(
          name: 'rock',
          albedo: 'rock.png',
          normal: 'rock_normal.png',
          tileSize: 12,
          triplanar: true,
        ),
        TerrainSet(
          name: 'grass',
          albedo: 'grass.png',
          normal: 'grass_normal.png',
          tileSize: 4,
        ),
      ],
      scatter: _scatter,
    );

    final swell = FractalNoise(GradientNoise(seed: _seed), octaves: 5);
    final ridges = FractalNoise(
      RidgedNoise(GradientNoise(seed: _seed + 17)),
      octaves: 5,
    );
    double height(double x, double z) {
      // Down to a plain towards the edges, so the ground ends in fog rather
      // than in a wall.
      final out = math.max(x.abs(), z.abs()) / reach;
      final keep = 1 - _smooth(0.5, 0.95, out);
      final ridge = math.pow(ridges.unit(x / 85, z / 85), 2.2).toDouble();
      final mountains = swell.unit(x / 260 + 4, z / 260 + 4);
      return relief *
          keep *
          (0.28 * swell.unit(x / 120, z / 120) + 0.72 * ridge * mountains);
    }

    for (var z = -_regionsOut; z < _regionsOut; z++) {
      for (var x = -_regionsOut; x < _regionsOut; x++) {
        _terrain.fillHeights(RegionKey(x, z), height);
      }
    }
  }

  /// Paints both sets' pictures in code: an albedo with a height in its
  /// alpha, and a normal map worked out from that same height.
  void _paintSets() {
    // Rock: slabs split by cracks, and tall, so it shows through the grass
    // where the two meet rather than fading into it. Ridged noise peaks in
    // thin lines, so turned over, the lines are the cracks.
    final slabs = FractalNoise(
      RidgedNoise(TilingNoise(GradientNoise(seed: 3), period: 3)),
      octaves: 2,
    );
    final grain = FractalNoise(
      TilingNoise(GradientNoise(seed: 5), period: 12),
      octaves: 2,
    );
    _paint(
      'rock',
      height: (u, v) =>
          0.2 +
          0.7 * (1 - math.pow(slabs.unit(u * 3, v * 3), 4).toDouble()) +
          0.1 * grain.unit(u * 12, v * 12),
      colour: (u, v, h) {
        final shade = 0.45 + 0.55 * h;
        return (132 * shade, 128 * shade, 124 * shade);
      },
      bumps: 3,
    );

    // Grass: low, so rock wins wherever there is any, and blotched, so a
    // hillside of it is not one flat green.
    final tufts = FractalNoise(
      TilingNoise(GradientNoise(seed: 9), period: 32),
      octaves: 3,
    );
    final patches = TilingNoise(GradientNoise(seed: 11), period: 4);
    _paint(
      'grass',
      height: (u, v) => 0.1 + 0.35 * tufts.unit(u * 32, v * 32),
      colour: (u, v, h) {
        final dry = patches.unit(u * 4, v * 4);
        final shade = 0.7 + 0.5 * h;
        return (
          (70 + 60 * dry) * shade,
          (104 + 30 * dry) * shade,
          (42 + 10 * dry) * shade,
        );
      },
      bumps: 2,
    );
  }

  void _paint(
    String name, {
    required double Function(double u, double v) height,
    required (double, double, double) Function(double u, double v, double h)
    colour,
    required double bumps,
  }) {
    const size = _pictureSize;
    final heights = Float64List(size * size);
    for (var y = 0; y < size; y++) {
      for (var x = 0; x < size; x++) {
        heights[y * size + x] = height(x / size, y / size);
      }
    }
    double at(int x, int y) => heights[(y % size) * size + (x % size)];

    final albedo = Uint8List(size * size * 4);
    final normal = Uint8List(size * size * 4);
    for (var y = 0; y < size; y++) {
      for (var x = 0; x < size; x++) {
        final h = at(x, y);
        final (r, g, b) = colour(x / size, y / size, h);
        final i = (y * size + x) * 4;
        albedo
          ..[i] = r.round().clamp(0, 255)
          ..[i + 1] = g.round().clamp(0, 255)
          ..[i + 2] = b.round().clamp(0, 255)
          ..[i + 3] = (h * 255).round().clamp(0, 255);

        // The slope of the same heights, wrapped at the edges so the
        // picture tiles.
        final n = Vector3(
          (at(x - 1 + size, y) - at(x + 1, y)) * bumps,
          (at(x, y - 1 + size) - at(x, y + 1)) * bumps,
          1,
        )..normalize();
        normal
          ..[i] = ((n.x * 0.5 + 0.5) * 255).round()
          ..[i + 1] = ((n.y * 0.5 + 0.5) * 255).round()
          ..[i + 2] = ((n.z * 0.5 + 0.5) * 255).round()
          // Rougher in the cracks than on the faces.
          ..[i + 3] = (255 * (0.95 - 0.25 * h)).round();
      }
    }
    _pictures['$name.png'] = albedo;
    _pictures['${name}_normal.png'] = normal;
  }

  static double _smooth(double from, double to, double value) {
    final t = ((value - from) / (to - from)).clamp(0.0, 1.0);
    return t * t * (3 - 2 * t);
  }

  @override
  OrblitScene scene(OrblitCamera camera, double seconds) {
    _terrain
      ..autoCover = AutoCover(
        steep: 0,
        flat: 1,
        slope: steepness,
        heightFalloff: altitude,
      )
      ..blendSharpness = sharpness;
    final sets = _terrain.sets;
    if (sets[0].triplanar != triplanar) {
      _terrain.sets[0] = TerrainSet(
        name: sets[0].name,
        albedo: sets[0].albedo,
        normal: sets[0].normal,
        tileSize: sets[0].tileSize,
        triplanar: triplanar,
      );
    }

    // Only where something changed: a region's ground, or the cover rule the
    // sliders above move, which takes the grass up or down the slopes.
    placer.update(_terrain);
    if (placer.revision != _scatteredRevision) {
      _scattered = scatterFrom(placer, key: 100).populations;
      _scatteredRevision = placer.revision;
    }

    return OrblitScene(
      camera: camera,
      // Built every frame, and cheap to: the regions' maps are handed over as
      // they are, and only one whose revision moved is sent.
      terrain: [
        terrainFrom(
          _terrain,
          key: 1,
          pixels: (path) => _pictures[path],
          levels: rings.round(),
        ),
      ],
      objects: [if (rover) _rover(seconds)],
      populations: [if (scatter) ..._scattered],
      lights: [
        OrblitLight(
          key: 1,
          kind: OrblitLightKind.directional,
          // Low, so the ridges throw shadows down their far sides.
          direction: Vector3(-0.6, -0.45, -0.4)..normalize(),
          colour: linearOf(const Color(0xFFFFEBD2)),
          intensity: 96000,
          castShadows: true,
        ),
      ],
      fog: OrblitFog(
        colour: linearOf(const Color(0xFFB9CCDD)),
        // Thick enough by the far edge that the ground ends in haze.
        density: 0.012,
        distance: 140,
        maximumOpacity: 1,
      ),
      sky: OrblitSky(
        zenith: linearOf(const Color(0xFF4A7FB8)),
        horizon: linearOf(const Color(0xFFB9CCDD)),
        ambient: 24000,
      ),
    );
  }

  static final Vector3 _roverHalf = Vector3(1.1, 0.8, 1.8);

  /// How far out the rover drives, in metres.
  static const double roverRadius = 60;

  /// A box driving a circle, standing on the ground and tilted with it.
  OrblitObject _rover(double seconds) {
    final turn = seconds * 0.12;
    final x = math.cos(turn) * roverRadius;
    final z = math.sin(turn) * roverRadius;
    final ground = _terrain.heightAt(x, z) ?? 0;
    final up = _terrain.normalAt(x, z) ?? Vector3(0, 1, 0);

    // Along the circle, then laid onto the slope.
    final heading = Vector3(-math.sin(turn), 0, math.cos(turn));
    final ahead = (heading - up * heading.dot(up))..normalize();
    final side = up.cross(ahead);
    // The cube runs from minus one to one, so these are half of 2.2 by 1.6 by
    // 3.6 metres, and it stands its half-height and a hand clear of the
    // ground along the ground's own up.
    final place = Vector3(x, ground, z) + up * (_roverHalf.y + 0.15);

    return OrblitObject(
      key: 2,
      transform: Matrix4.columns(
        Vector4(side.x, side.y, side.z, 0) * _roverHalf.x,
        Vector4(up.x, up.y, up.z, 0) * _roverHalf.y,
        Vector4(ahead.x, ahead.y, ahead.z, 0) * _roverHalf.z,
        Vector4(place.x, place.y, place.z, 1),
      ),
      colour: linearOf(const Color(0xFFE0A030)),
    );
  }

  @override
  Widget settings(BuildContext context, VoidCallback changed) {
    final regions = _terrain.regions.length;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          '$regions regions of $_regionSize × $_regionSize heights, sent once. '
          'Everything below changes settings only, except Relief and World.',
          style: Theme.of(context).textTheme.bodySmall,
        ),
        const SizedBox(height: 8),
        Setting(
          label: 'Relief',
          value: relief,
          min: 4,
          max: 80,
          decimals: 0,
          unit: ' m',
          // Once the handle is let go: this makes every region again.
          onChanged: (value) => relief = value,
          onSettled: (value) {
            relief = value;
            _build();
            changed();
          },
        ),
        Setting(
          label: 'Steepness',
          value: steepness,
          min: 0.5,
          max: 4,
          onChanged: (value) {
            steepness = value;
            changed();
          },
        ),
        Setting(
          label: 'Altitude',
          value: altitude,
          min: 0,
          max: 2,
          onChanged: (value) {
            altitude = value;
            changed();
          },
        ),
        Setting(
          label: 'Sharpness',
          value: sharpness,
          min: 0,
          max: 1,
          onChanged: (value) {
            sharpness = value;
            changed();
          },
        ),
        Setting(
          label: 'Rings',
          value: rings,
          min: 1,
          max: 8,
          decimals: 0,
          onChanged: (value) {
            rings = value;
            changed();
          },
        ),
        Toggle(
          label: 'Triplanar',
          value: triplanar,
          note: triplanar
              ? 'Rock laid from three sides'
              : 'Rock dropped from above, and stretched down cliffs',
          onChanged: (value) {
            triplanar = value;
            changed();
          },
        ),
        Toggle(
          label: 'Scatter',
          value: scatter,
          note: '${placer.count} grass, stones and trees, placed by rule',
          onChanged: (value) {
            scatter = value;
            changed();
          },
        ),
        Toggle(
          label: 'Rover',
          value: rover,
          note: 'Placed by heightAt and normalAt, with no physics',
          onChanged: (value) {
            rover = value;
            changed();
          },
        ),
        const SizedBox(height: 4),
        Choice(
          label: 'World',
          options: const ['1', '2', '3', '4'],
          selected: '$_seed',
          onSelect: (option) {
            _seed = int.parse(option);
            _build();
            changed();
          },
        ),
      ],
    );
  }

  @override
  String get code => '''
// Ground as data: regions of heights, made only where there is ground.
final terrain = Terrain(regionSize: 64, spacing: 2, sets: const [
  TerrainSet(name: 'rock', albedo: 'rock.png', normal: 'rock_normal.png',
      tileSize: 12, triplanar: true),
  TerrainSet(name: 'grass', albedo: 'grass.png', normal: 'grass_normal.png',
      tileSize: 4),
]);
for (final key in keys) {
  terrain.fillHeights(key, (x, z) => relief * hills.unit(x / 120, z / 120));
}

// New ground is automatic: rock where it is steep or high, grass elsewhere.
terrain.autoCover = const AutoCover(steep: 0, flat: 1, slope: 1.2);

// What grows on it, by rule. A trunk and a crown share a seed, so they stand
// together.
terrain.scatter.addAll(const [
  ScatterLayer(name: 'grass', seed: 1, density: 0.5, sets: [1],
      size: (0.4, 0.3, 0.4), lean: 0.7, colour: 0x5E8C3A, range: 70),
  ScatterLayer(name: 'stones', seed: 2, density: 0.03, sets: [0],
      size: (1.2, 0.7, 0.9), lean: 1, lift: -0.25, colour: 0x8A8580),
  ScatterLayer(name: 'trunks', seed: 3, density: 0.004, sets: [1],
      maxSlope: 22, size: (0.45, 4, 0.45), colour: 0x5A3E28),
  ScatterLayer(name: 'crowns', seed: 3, density: 0.004, sets: [1],
      maxSlope: 22, size: (2.6, 3.4, 2.6), lift: 3.2, colour: 0x2F5A2A),
]);

// Every frame. The maps are shared, and only a region whose revision moved
// crosses to the renderer; the placer places again only where it moved.
placer.update(terrain);
OrblitScene(
  terrain: [
    terrainFrom(terrain, key: 1, pixels: (path) => decoded[path]),
  ],
  populations: scatterFrom(placer, key: 100).populations,
  ...
)

// Standing on it needs no physics: the heights the renderer draws.
final y = terrain.heightAt(x, z);
final up = terrain.normalAt(x, z);
''';
}

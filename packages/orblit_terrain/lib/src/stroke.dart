import 'dart:math' as math;
import 'dart:typed_data';

import 'brush.dart';
import 'cover.dart';
import 'ground_colour.dart';
import 'patch.dart';
import 'region.dart';
import 'terrain.dart';

/// One press of a brush on a terrain, from the pointer going down to it
/// coming up.
///
/// Each [moveTo] carries the brush from where it was to somewhere new,
/// laying a dab every [Brush.spacing] of the way, and hands back what that
/// move changed as a [TerrainPatch]. Fold the patches together with
/// [TerrainPatch.followedBy] and the stroke is one step to undo, whatever
/// the number of moves it took.
///
/// Writes straight into the regions' maps and touches them, so a renderer
/// that watches [TerrainRegion.revision] sends only the regions the brush is
/// in. Ground with no region under it is left alone: a brush shapes the
/// ground that exists, and making more is a separate decision.
class TerrainStroke {
  TerrainStroke(
    this.terrain, {
    required this.tool,
    this.brush = const Brush(),
    this.invert = false,
    this.set = 0,
    this.colour = GroundColour.none,
    this.roughness = 0,
    this.height,
    int seed = 0,
    this.tileSize = TerrainPatch.defaultTileSize,
  }) : _random = math.Random(seed);

  final Terrain terrain;

  final BrushTool tool;

  final Brush brush;

  /// Whether the tool works backwards: lowers for [BrushTool.raise], fills
  /// for [BrushTool.hole], hands painted ground back for the paint tools.
  final bool invert;

  /// The set [BrushTool.cover] lays, by its index in [Terrain.sets].
  final int set;

  /// The colour [BrushTool.colour] tints towards. Its roughness is not
  /// painted with it.
  final GroundColour colour;

  /// The nudge [BrushTool.roughness] paints, −1 (a mirror) to +1 (chalk).
  final double roughness;

  /// The height [BrushTool.flatten] draws the ground towards, or null for
  /// the height where the stroke began.
  final double? height;

  /// Texels a tile of the undo record is across. See [TerrainPatch].
  final int tileSize;

  final math.Random _random;

  (double, double)? _start;
  double? _startHeight;
  (double, double)? _last;

  /// The far end of [BrushTool.slope]'s ramp: the dab farthest from the
  /// start so far, and the ground's height there when the brush reached it.
  (double, double)? _far;
  double? _farHeight;
  double _farDistance = -1;

  /// How far the brush has gone since its last dab.
  double _carry = 0;

  int _dabs = 0;

  /// How many dabs the stroke has laid.
  int get dabs => _dabs;

  /// Where the stroke began, or null before its first [moveTo].
  (double, double)? get start => _start;

  /// Carries the brush to world ([x], [z]) and says what that changed. The
  /// first move lays one dab where it lands; each after lays one every
  /// [Brush.spacing] along the way from the last, and none if the brush has
  /// not gone far enough since.
  TerrainPatch moveTo(double x, double z) {
    final recorder = TerrainRecorder(terrain, {tool.layer}, tileSize: tileSize);
    final last = _last;
    if (last == null) {
      _start = (x, z);
      _startHeight = _groundAt(x, z);
      _dab(x, z, recorder);
    } else {
      final (fromX, fromZ) = last;
      final dx = x - fromX;
      final dz = z - fromZ;
      final distance = math.sqrt(dx * dx + dz * dz);
      final step = _step;
      var along = step - _carry;
      while (along <= distance) {
        final t = along / distance;
        _dab(fromX + dx * t, fromZ + dz * t, recorder);
        along += step;
      }
      _carry = distance - (along - step);
    }
    _last = (x, z);
    return recorder.finish();
  }

  /// Metres between dabs: the brush's spacing, but never so close that a
  /// dab lands on the same quarter-texel as the last.
  double get _step => math.max(
    math.max(brush.spacing, Brush.minSpacing) * brush.size,
    terrain.spacing * 0.25,
  );

  /// How much of a reference dab each dab is, so [Brush.strength] means the
  /// same per pass at any spacing.
  double get _perDab => _step / (brush.size * Brush.referenceSpacing);

  /// How far one dab blends towards its target, where [weight] of it lands.
  double _share(double weight) =>
      1 -
      math
          .pow(1 - (brush.strength * weight).clamp(0.0, 1.0), _perDab)
          .toDouble();

  /// The ground's height at world ([x], [z]), or the nearest texel's where
  /// a hole leaves it none. Null off the terrain.
  double? _groundAt(double x, double z) =>
      terrain.heightAt(x, z) ??
      terrain.texelHeight(
        (x / terrain.spacing).round(),
        (z / terrain.spacing).round(),
      );

  void _dab(double atX, double atZ, TerrainRecorder recorder) {
    if (!(brush.size > 0)) return;
    _dabs++;
    var x = atX;
    var z = atZ;
    final radius = brush.radius;
    if (brush.jitter > 0) {
      final angle = _random.nextDouble() * 2 * math.pi;
      final reach =
          math.sqrt(_random.nextDouble()) *
          brush.jitter.clamp(0.0, 1.0) *
          radius;
      x += math.cos(angle) * reach;
      z += math.sin(angle) * reach;
    }
    final spacing = terrain.spacing;
    final area = (
      i0: ((x - radius) / spacing).ceil(),
      j0: ((z - radius) / spacing).ceil(),
      i1: ((x + radius) / spacing).floor(),
      j1: ((z + radius) / spacing).floor(),
    );
    if (area.i0 > area.i1 || area.j0 > area.j1) return;
    recorder.keep(area.i0, area.j0, area.i1, area.j1);

    switch (tool) {
      case BrushTool.raise || BrushTool.lower:
        final up = (tool == BrushTool.raise) != invert;
        final rise = (up ? 1 : -1) * brush.strength * _step / 4;
        _each(area, x, z, (region, at, i, j, weight) {
          region.heights[at] += rise * weight;
        });
      case BrushTool.smooth:
        _smooth(area, x, z);
      case BrushTool.flatten:
        final target = height ?? _startHeight;
        if (target == null) return;
        _each(area, x, z, (region, at, i, j, weight) {
          final was = region.heights[at];
          region.heights[at] = was + _share(weight) * (target - was);
        });
      case BrushTool.slope:
        _slope(area, x, z);
      case BrushTool.cover:
        _each(area, x, z, (region, at, i, j, weight) {
          final was = Cover(region.cover[at]);
          final now = invert
              ? (weight >= 0.5 ? was.withAutomatic(true) : was)
              : _laid(_placed(was, region, at, i, j), _share(weight));
          region.cover[at] = now.word;
        });
      case BrushTool.hole:
        _each(area, x, z, (region, at, i, j, weight) {
          if (weight < 0.5) return;
          region.cover[at] = Cover(region.cover[at]).withHole(!invert).word;
        });
      case BrushTool.colour:
        final target = invert ? GroundColour.none : colour;
        _each(area, x, z, (region, at, i, j, weight) {
          final share = _share(weight);
          final bytes = region.colour;
          final base = at * 4;
          bytes[base] = _towards(bytes[base], target.red, share);
          bytes[base + 1] = _towards(bytes[base + 1], target.green, share);
          bytes[base + 2] = _towards(bytes[base + 2], target.blue, share);
        });
      case BrushTool.roughness:
        final target = invert
            ? GroundColour.none.roughnessByte
            : GroundColour.of(roughness: roughness).roughnessByte;
        _each(area, x, z, (region, at, i, j, weight) {
          final bytes = region.colour;
          final base = at * 4 + 3;
          bytes[base] = _towards(bytes[base], target, _share(weight));
        });
    }
  }

  /// Visits every texel of [area] that is in a region and inside the dab
  /// centred on ([x], [z]), with how much of the dab reaches it, then
  /// touches the regions it visited.
  void _each(
    ({int i0, int j0, int i1, int j1}) area,
    double x,
    double z,
    void Function(TerrainRegion region, int at, int i, int j, double weight)
    visit,
  ) {
    final size = terrain.regionSize;
    final spacing = terrain.spacing;
    for (
      var rz = floorDiv(area.j0, size);
      rz <= floorDiv(area.j1, size);
      rz++
    ) {
      for (
        var rx = floorDiv(area.i0, size);
        rx <= floorDiv(area.i1, size);
        rx++
      ) {
        final region = terrain.regionAt(RegionKey(rx, rz));
        if (region == null) continue;
        var visited = false;
        final left = rx * size;
        final top = rz * size;
        for (
          var j = math.max(area.j0, top);
          j <= math.min(area.j1, top + size - 1);
          j++
        ) {
          final dz = j * spacing - z;
          for (
            var i = math.max(area.i0, left);
            i <= math.min(area.i1, left + size - 1);
            i++
          ) {
            final dx = i * spacing - x;
            final weight = brush.weightAt(math.sqrt(dx * dx + dz * dz));
            if (weight <= 0) continue;
            visit(region, (j - top) * size + (i - left), i, j, weight);
            visited = true;
          }
        }
        if (visited) region.touch();
      }
    }
  }

  /// Draws each height towards the mean of it and its eight neighbours, all
  /// read before any is written, so the brush has no direction.
  void _smooth(({int i0, int j0, int i1, int j1}) area, double x, double z) {
    final width = area.i1 - area.i0 + 3;
    final depth = area.j1 - area.j0 + 3;
    final before = Float64List(width * depth);
    for (var row = 0; row < depth; row++) {
      for (var column = 0; column < width; column++) {
        before[row * width + column] =
            terrain.texelHeight(area.i0 - 1 + column, area.j0 - 1 + row) ??
            double.nan;
      }
    }
    _each(area, x, z, (region, at, i, j, weight) {
      final was = region.heights[at];
      final column = i - area.i0 + 1;
      final row = j - area.j0 + 1;
      var sum = 0.0;
      var count = 0;
      for (var dj = -1; dj <= 1; dj++) {
        for (var di = -1; di <= 1; di++) {
          final height = before[(row + dj) * width + column + di];
          if (height.isNaN) continue;
          sum += height;
          count++;
        }
      }
      region.heights[at] = was + _share(weight) * (sum / count - was);
    });
  }

  /// Draws the ground towards a straight ramp from the stroke's start, at
  /// the height it had then, to the farthest dab from there, at the height
  /// it had when the brush got there. Ground beyond either end is left
  /// alone, so climbing onto a ledge does not level the ledge.
  ///
  /// On the way out the far end is the dab itself, and the ramp follows the
  /// ground the brush finds; on the way back it stays put, and the brush
  /// lays the ramp over the ground it crossed.
  void _slope(({int i0, int j0, int i1, int j1}) area, double x, double z) {
    final (startX, startZ) = _start!;
    final low = _startHeight;
    if (low == null) return;
    final outX = x - startX;
    final outZ = z - startZ;
    final out = outX * outX + outZ * outZ;
    if (out >= _farDistance) {
      final ground = _groundAt(x, z);
      if (ground == null) return;
      _far = (x, z);
      _farHeight = ground;
      _farDistance = out;
    }
    final (farX, farZ) = _far!;
    final high = _farHeight!;
    final runX = farX - startX;
    final runZ = farZ - startZ;
    final run = runX * runX + runZ * runZ;
    final spacing = terrain.spacing;
    // Too short to have a direction.
    if (run < spacing * spacing * 0.25) return;
    _each(area, x, z, (region, at, i, j, weight) {
      final along =
          ((i * spacing - startX) * runX + (j * spacing - startZ) * runZ) / run;
      if (along < 0 || along > 1) return;
      final target = low + along * (high - low);
      final was = region.heights[at];
      region.heights[at] = was + _share(weight) * (target - was);
    });
  }

  /// [was] as the sets it shows: automatic ground named as the sets its
  /// slope and height choose, so painting over it starts from what was seen
  /// rather than from set 0.
  Cover _placed(Cover was, TerrainRegion region, int at, int i, int j) {
    if (!was.automatic) return was;
    final spacing = terrain.spacing;
    final normal = terrain.normalAt(i * spacing, j * spacing);
    final chosen = terrain.autoCover.coverFor(
      normal?.y ?? 1,
      region.heights[at],
    );
    return was
        .withAutomatic(false)
        .withBase(chosen.base)
        .withOverlay(chosen.overlay)
        .withBlend(chosen.blend);
  }

  /// [was] with [share] more of [set] showing.
  ///
  /// A texel holds two sets. Where [set] is one of them the blend moves
  /// towards it; where it is neither, whichever of the two shows more stays
  /// as the base and [set] becomes the overlay. Whole steps of the blend, and
  /// at least one, so a light brush still gets all the way there.
  Cover _laid(Cover was, double share) {
    if (share <= 0) return was;
    if (was.overlay == set) {
      final left = Cover.blendSteps - was.blendStep;
      final step = was.blendStep + (share * left).ceil();
      if (step >= Cover.blendSteps) {
        return was.withBase(set).withBlend(0);
      }
      return was.withBlend(step / Cover.blendSteps);
    }
    if (was.base == set) {
      final step = was.blendStep - (share * was.blendStep).ceil();
      return was.withBlend(math.max(step, 0) / Cover.blendSteps);
    }
    final kept = was.blendStep * 2 > Cover.blendSteps ? was.overlay : was.base;
    return was.withBase(kept).withOverlay(set).withBlend(share);
  }

  static int _towards(int was, int target, double share) =>
      (was + (target - was) * share).round();
}

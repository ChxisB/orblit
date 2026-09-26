import 'dart:math' as math;
import 'dart:typed_data';

import 'package:vector_math/vector_math_64.dart';

import 'cover.dart';
import 'region.dart';
import 'scatter_layer.dart';
import 'terrain.dart';

/// What one [ScatterLayer] put down in one region: a transform and a colour
/// for each, in the flat buffers a renderer draws many copies from.
///
/// Made again, never changed: a region whose ground moves gets a new group
/// with a new [revision], so a renderer that remembers the revision it drew
/// knows to take the buffers again and otherwise leaves them where they are.
class ScatterGroup {
  ScatterGroup._({
    required this.key,
    required this.layer,
    required this.id,
    required this.transforms,
    required this.colours,
    required this.minimum,
    required this.maximum,
  }) : revision = ++_lastRevision;

  static int _lastRevision = 0;

  /// The region it stands in.
  final RegionKey key;

  /// Which of the terrain's [Terrain.scatter] layers it is, by index.
  final int layer;

  /// A number for this region and layer, the same for as long as both are
  /// there, and never shared with another group of the same placer. What a
  /// renderer keeps its buffers against.
  final int id;

  /// Sixteen floats each, column-major, in world space. For a block, each
  /// takes the cube from −1 to 1 to the box the block fills; for a model,
  /// the model's own space to where it stands.
  final Float32List transforms;

  /// Three floats each, linear RGB.
  final Float32List colours;

  /// A box around every one of them, in world space.
  final Vector3 minimum;
  final Vector3 maximum;

  /// Never the same for two groups.
  final int revision;

  /// How many there are.
  int get count => transforms.length ~/ 16;
}

/// Where a terrain's [Terrain.scatter] layers put things, kept region by
/// region and worked out again only where something changed.
///
/// Placement is a pattern laid over the whole world, not over each region:
/// the world is cut into squares one per [ScatterLayer.density], each square
/// has one spot in it chosen by the layer's seed and the square's place, and
/// the ground there decides whether anything stands on it. So the same
/// ground scatters the same every time, on every machine and whatever size
/// its regions are, and an edit in one region cannot move a stone in the
/// next.
///
/// Nothing is put where [Terrain.heightAt] has no height — over a hole, off
/// the edge — and a layer's density is thinned by how much of the ground is
/// its [ScatterLayer.sets], so painting scree over a meadow takes the grass
/// away with it.
class ScatterPlacer {
  final Map<RegionKey, _Placed> _placed = {};
  final Map<(RegionKey, int), int> _ids = {};
  int _nextId = 0;
  List<ScatterLayer> _layers = const [];
  (int, int, double, double)? _autoCover;
  List<ScatterGroup> _groups = const [];
  int _revision = 0;

  /// Moves whenever any group is placed again, added or taken away.
  int get revision => _revision;

  /// Every group, region by region along x and then z, and layer by layer
  /// within a region. Empty groups are included: they are how a renderer
  /// learns a layer has nothing left in a region.
  List<ScatterGroup> get groups => _groups;

  /// The layers as they were at the last [update], which a group's
  /// [ScatterGroup.layer] counts into. Kept here rather than read from the
  /// terrain, so the rules a group is drawn by are the ones it was placed by
  /// even when the terrain's have changed since.
  List<ScatterLayer> get layers => _layers;

  /// The groups in the region at [key], one per layer, or none.
  List<ScatterGroup> groupsIn(RegionKey key) =>
      _placed[key]?.groups ?? const [];

  /// How many things are placed in all.
  int get count => _groups.fold(0, (total, group) => total + group.count);

  /// Brings the placement up to date with [terrain]. Answers whether
  /// anything changed.
  ///
  /// A region is placed again when its own revision has moved, or when a
  /// neighbour has changed along the edge they share — the heights, slope
  /// and cover near an edge are read from both sides of it — or when a
  /// neighbour has come or gone. A layer is placed again everywhere when its
  /// rules change, and only that layer. Otherwise nothing is done, so asking
  /// every frame costs a few comparisons a region.
  bool update(Terrain terrain) {
    final layers = terrain.scatter;
    final auto = terrain.autoCover;
    final autoCover = (auto.steep, auto.flat, auto.slope, auto.heightFalloff);
    final allRules = autoCover != _autoCover;
    var changed = false;

    for (final key in _placed.keys.toList()) {
      if (terrain.regionAt(key) != null) continue;
      _placed.remove(key);
      _ids.removeWhere((id, _) => id.$1 == key);
      changed = true;
    }

    for (final region in terrain.regions) {
      final key = region.key;
      final placed = _placed[key];
      final ground = placed == null || _groundMoved(terrain, region, placed);
      final groups = <ScatterGroup>[];
      for (var layer = 0; layer < layers.length; layer++) {
        final kept = !ground && !allRules && layer < placed.groups.length
            ? placed.groups[layer]
            : null;
        if (kept != null &&
            layer < _layers.length &&
            _layers[layer] == layers[layer]) {
          groups.add(kept);
          continue;
        }
        final id = _ids.putIfAbsent((key, layer), () => _nextId++);
        groups.add(_place(terrain, layers[layer], layer, region, id));
        changed = true;
      }
      if (placed != null && placed.groups.length > layers.length) {
        _ids.removeWhere((id, _) => id.$1 == key && id.$2 >= layers.length);
        changed = true;
      }
      _placed[key] = _Placed(
        revision: region.revision,
        edges: ground ? _edges(terrain, key) : placed.edges,
        groups: groups,
      );
    }

    _layers = List.of(layers);
    _autoCover = autoCover;
    if (changed) {
      final keys = _placed.keys.toList()
        ..sort((a, b) => a.z != b.z ? a.z.compareTo(b.z) : a.x.compareTo(b.x));
      _groups = [for (final key in keys) ..._placed[key]!.groups];
      _revision++;
    }
    return changed;
  }

  /// Forgets everything placed, so the next [update] places it all again.
  void clear() {
    _placed.clear();
    _ids.clear();
    _layers = const [];
    _autoCover = null;
    _groups = const [];
    _revision++;
  }

  bool _groundMoved(Terrain terrain, TerrainRegion region, _Placed placed) {
    if (region.revision != placed.revision) return true;
    final edges = placed.edges;
    for (var n = 0; n < _around.length; n++) {
      final (dx, dz) = _around[n];
      final neighbour = terrain.regionAt(
        RegionKey(region.key.x + dx, region.key.z + dz),
      );
      final revision = neighbour?.revision ?? -1;
      if (revision == edges[n].$1) continue;
      // The neighbour changed somewhere; only its edge matters here.
      final print = neighbour == null ? 0 : _edgePrint(neighbour, -dx, -dz);
      if (print != edges[n].$2) return true;
      edges[n] = (revision, print);
    }
    return false;
  }

  static const List<(int, int)> _around = [
    (-1, -1),
    (0, -1),
    (1, -1),
    (-1, 0),
    (1, 0),
    (-1, 1),
    (0, 1),
    (1, 1),
  ];

  static List<(int, int)> _edges(Terrain terrain, RegionKey key) => [
    for (final (dx, dz) in _around)
      switch (terrain.regionAt(RegionKey(key.x + dx, key.z + dz))) {
        final neighbour? => (
          neighbour.revision,
          _edgePrint(neighbour, -dx, -dz),
        ),
        null => (-1, 0),
      },
  ];

  /// A fingerprint of the two rows of [region] nearest the side facing
  /// ([towardsX], [towardsZ]) — all three maps — which is everything a
  /// neighbour on that side reads from it while placing.
  static int _edgePrint(TerrainRegion region, int towardsX, int towardsZ) {
    final size = region.size;
    final heights = region.heights.buffer.asUint32List(
      region.heights.offsetInBytes,
      region.heights.length,
    );
    final colours = region.colour;
    // Along an axis, the rows at that side: the last two towards +,
    // the first two towards −, all of them where the side is a corner's.
    (int, int) span(int towards) => switch (towards) {
      1 => (size - 2, size),
      -1 => (0, 2),
      _ => (0, size),
    };
    final (iFrom, iTo) = span(towardsX);
    final (jFrom, jTo) = span(towardsZ);
    var print = 0x2545F491;
    for (var j = jFrom; j < jTo; j++) {
      for (var i = iFrom; i < iTo; i++) {
        final at = j * size + i;
        print = _mix(print ^ heights[at]);
        print = _mix(print ^ region.cover[at]);
        print = _mix(
          print ^
              colours[at * 4] << 24 ^
              colours[at * 4 + 1] << 16 ^
              colours[at * 4 + 2] << 8 ^
              colours[at * 4 + 3],
        );
      }
    }
    return print;
  }

  static ScatterGroup _place(
    Terrain terrain,
    ScatterLayer layer,
    int index,
    TerrainRegion region,
    int id,
  ) {
    final ground = _Ground(terrain, region);
    final spacing = terrain.spacing;
    final across = region.size * spacing;
    final x0 = region.key.x * across;
    final z0 = region.key.z * across;
    final cell = 1 / math.sqrt(layer.density);
    final firstX = (x0 / cell).floor();
    final lastX = ((x0 + across) / cell).floor();
    final firstZ = (z0 / cell).floor();
    final lastZ = ((z0 + across) / cell).floor();
    final seed = _mix(layer.seed & 0xFFFFFFFF);
    final (sizeX, sizeY, sizeZ) = layer.size;
    final block = layer.mesh == null;
    final base = _linear(layer.colour);

    final transforms = _Floats();
    final colours = _Floats();
    final minimum = Vector3.all(double.infinity);
    final maximum = Vector3.all(double.negativeInfinity);

    for (var cz = firstZ; cz <= lastZ; cz++) {
      for (var cx = firstX; cx <= lastX; cx++) {
        final spot = _mix(_mix(seed ^ (cx & 0xFFFFFFFF)) ^ (cz & 0xFFFFFFFF));
        final x = (cx + _draw(spot, 0)) * cell;
        final z = (cz + _draw(spot, 1)) * cell;
        if (!ground.holds(x, z)) continue;
        final height = ground.heightAt(x, z);
        if (height == null || height < layer.minHeight) continue;
        if (height > layer.maxHeight) continue;
        final normal = ground.normalAt(x, z);
        final slope = math.acos(normal.y.clamp(-1.0, 1.0)) * 180 / math.pi;
        if (slope < layer.minSlope || slope > layer.maxSlope) continue;
        final share = ground.share(layer, x, z, normal.y, height);
        if (share <= 0 || _draw(spot, 2) >= share) continue;

        final scale =
            layer.minScale + _draw(spot, 3) * (layer.maxScale - layer.minScale);
        final turn = layer.turn ? _draw(spot, 4) * 2 * math.pi : 0.0;
        final (ux, uy, uz) = _up(normal.x, normal.y, normal.z, layer.lean);
        final lift = layer.lift * scale;
        final sx = sizeX * scale;
        final sy = sizeY * scale;
        final sz = sizeZ * scale;
        // A block is the cube from −1 to 1 halved and stood on its bottom
        // face; a model already stands on its origin.
        final half = block ? 0.5 : 1.0;
        final rise = block ? sy / 2 : 0.0;
        _stand(
          transforms,
          ux,
          uy,
          uz,
          turn,
          sx * half,
          sy * half,
          sz * half,
          x + ux * (lift + rise),
          height + uy * (lift + rise),
          z + uz * (lift + rise),
        );

        final reach = math.sqrt(sx * sx + sy * sy + sz * sz) + lift.abs();
        minimum
          ..x = math.min(minimum.x, x - reach)
          ..y = math.min(minimum.y, height - reach)
          ..z = math.min(minimum.z, z - reach);
        maximum
          ..x = math.max(maximum.x, x + reach)
          ..y = math.max(maximum.y, height + reach)
          ..z = math.max(maximum.z, z + reach);

        final shade = 1 + layer.colourVariation * (_draw(spot, 5) * 2 - 1);
        var red = base.$1 * shade;
        var green = base.$2 * shade;
        var blue = base.$3 * shade;
        final tint = layer.groundTint;
        if (tint > 0) {
          final (r, g, b) = ground.colourAt(x, z);
          red *= 1 + (_channel(r) - 1) * tint;
          green *= 1 + (_channel(g) - 1) * tint;
          blue *= 1 + (_channel(b) - 1) * tint;
        }
        colours
          ..add(red)
          ..add(green)
          ..add(blue);
      }
    }

    if (colours.length == 0) {
      minimum.setZero();
      maximum.setZero();
    }
    return ScatterGroup._(
      key: region.key,
      layer: index,
      id: id,
      transforms: transforms.done(),
      colours: colours.done(),
      minimum: minimum,
      maximum: maximum,
    );
  }

  /// Writes one transform to [into]: turned [turn] radians about its own
  /// up, tilted so that up is ([ux], [uy], [uz]), scaled by ([sx], [sy],
  /// [sz]) along its own axes, and moved to ([x], [y], [z]).
  static void _stand(
    _Floats into,
    double ux,
    double uy,
    double uz,
    double turn,
    double sx,
    double sy,
    double sz,
    double x,
    double y,
    double z,
  ) {
    // The shortest rotation from +y to up: about the axis square to both,
    // by the angle between them. Its middle column is up itself.
    final sine2 = ux * ux + uz * uz;
    final cosine = uy;
    final (double ax, double az) = sine2 < 1e-18
        ? (0, 0)
        : (uz / math.sqrt(sine2), -ux / math.sqrt(sine2));
    final rest = 1 - cosine;
    final x0 = cosine + rest * ax * ax;
    final y0 = -ux;
    final z0 = rest * ax * az;
    final x2 = rest * ax * az;
    final y2 = -uz;
    final z2 = cosine + rest * az * az;
    // Then the turn about +y, which comes first: it mixes the tilt's first
    // and last columns and leaves up alone.
    final c = math.cos(turn);
    final s = math.sin(turn);
    into
      ..add((c * x0 - s * x2) * sx)
      ..add((c * y0 - s * y2) * sx)
      ..add((c * z0 - s * z2) * sx)
      ..add(0)
      ..add(ux * sy)
      ..add(uy * sy)
      ..add(uz * sy)
      ..add(0)
      ..add((s * x0 + c * x2) * sz)
      ..add((s * y0 + c * y2) * sz)
      ..add((s * z0 + c * z2) * sz)
      ..add(0)
      ..add(x)
      ..add(y)
      ..add(z)
      ..add(1);
  }

  /// Which way is up for something leaning [lean] of the way from straight
  /// up to the ground's normal ([nx], [ny], [nz]).
  static (double, double, double) _up(
    double nx,
    double ny,
    double nz,
    double lean,
  ) {
    final x = nx * lean;
    final y = 1 + (ny - 1) * lean;
    final z = nz * lean;
    final length = math.sqrt(x * x + y * y + z * z);
    return (x / length, y / length, z / length);
  }
}

/// The ground one region is placed on.
///
/// Read straight from the region's maps wherever everything a question
/// needs lies inside it, which is nearly everywhere, and through the terrain
/// near its edges. The answers are the terrain's own to the last bit — the
/// same sums in the same order — so where a spot falls against the edge of a
/// region never changes what stands on it.
final class _Ground {
  _Ground(this.terrain, TerrainRegion region)
    : size = region.size,
      firstI = region.key.x * region.size,
      firstJ = region.key.z * region.size,
      heights = region.heights,
      cover = region.cover,
      colour = region.colour,
      spacing = terrain.spacing;

  final Terrain terrain;
  final int size;
  final int firstI;
  final int firstJ;
  final Float32List heights;
  final Uint32List cover;
  final Uint8List colour;
  final double spacing;

  /// Whether world ([x], [z]) is in this region, as [Terrain.keyAt] decides.
  bool holds(double x, double z) {
    final i = (x / spacing).floor() - firstI;
    final j = (z / spacing).floor() - firstJ;
    return i >= 0 && i < size && j >= 0 && j < size;
  }

  /// Where texel ([i], [j]) is in the region's maps, or −1 unless every
  /// texel from one before it to two after it, both ways, is in the region.
  int _inner(int i, int j) {
    final li = i - firstI;
    final lj = j - firstJ;
    return li >= 1 && li <= size - 3 && lj >= 1 && lj <= size - 3
        ? lj * size + li
        : -1;
  }

  double? _solid(int at) => Cover(cover[at]).hole ? null : heights[at];

  /// [Terrain.heightAt].
  double? heightAt(double x, double z) {
    final u = x / spacing;
    final v = z / spacing;
    final i = u.floor();
    final j = v.floor();
    final at = _inner(i, j);
    if (at < 0) return terrain.heightAt(x, z);
    final fu = u - i;
    final fv = v - j;
    final h00 = _solid(at);
    final h11 = _solid(at + size + 1);
    if (h00 == null || h11 == null) return null;
    if (fu >= fv) {
      final h10 = _solid(at + 1);
      if (h10 == null) return null;
      return h00 + fu * (h10 - h00) + fv * (h11 - h10);
    }
    final h01 = _solid(at + size);
    if (h01 == null) return null;
    return h00 + fv * (h01 - h00) + fu * (h11 - h01);
  }

  /// [Terrain.normalAt], where there is ground.
  Vector3 normalAt(double x, double z) {
    final u = x / spacing;
    final v = z / spacing;
    final i = u.floor();
    final j = v.floor();
    final at = _inner(i, j);
    if (at < 0) return terrain.normalAt(x, z)!;
    final fu = u - i;
    final fv = v - j;
    var gx = 0.0;
    var gz = 0.0;
    var total = 0.0;
    void corner(int at, double weight) {
      if (weight == 0) return;
      gx += weight * (heights[at + 1] - heights[at - 1]);
      gz += weight * (heights[at + size] - heights[at - size]);
      total += weight;
    }

    corner(at, (1 - fu) * (1 - fv));
    corner(at + 1, fu * (1 - fv));
    corner(at + size, (1 - fu) * fv);
    corner(at + size + 1, fu * fv);
    final scale = 1 / (total * 2 * spacing);
    return Vector3(-gx * scale, 1, -gz * scale)..normalize();
  }

  Cover? _coverAt(int i, int j) {
    final at = _inner(i, j);
    return at < 0 ? terrain.texelCover(i, j) : Cover(cover[at]);
  }

  /// How much of the ground at world ([x], [z]) is sets [layer] grows on,
  /// 0 to 1: the share at each of the four nearest texels, blended the way
  /// the ground's own sets blend between them.
  double share(
    ScatterLayer layer,
    double x,
    double z,
    double normalY,
    double height,
  ) {
    if (layer.sets.isEmpty) return 1;
    final u = x / spacing;
    final v = z / spacing;
    final i = u.floor();
    final j = v.floor();
    final fu = u - i;
    final fv = v - j;
    final auto = terrain.autoCover;
    final flat = auto.flatness(normalY, height);

    double at(int ci, int cj) {
      final cover = _coverAt(ci, cj);
      if (cover == null || cover.hole) return 0;
      final (int low, int high, double blend) = cover.automatic
          ? (auto.steep, auto.flat, flat)
          : (cover.base, cover.overlay, cover.blend);
      if (low == high) return layer.growsOn(low) ? 1 : 0;
      return (layer.growsOn(low) ? 1 - blend : 0) +
          (layer.growsOn(high) ? blend : 0);
    }

    return at(i, j) * (1 - fu) * (1 - fv) +
        at(i + 1, j) * fu * (1 - fv) +
        at(i, j + 1) * (1 - fu) * fv +
        at(i + 1, j + 1) * fu * fv;
  }

  /// The ground's colour at the texel nearest world ([x], [z]), as sRGB
  /// bytes: white where there is none.
  (int, int, int) colourAt(double x, double z) {
    final i = (x / spacing).round();
    final j = (z / spacing).round();
    final at = _inner(i, j);
    if (at < 0) {
      final found = terrain.texelColour(i, j);
      return found == null
          ? (255, 255, 255)
          : (found.red, found.green, found.blue);
    }
    return (colour[at * 4], colour[at * 4 + 1], colour[at * 4 + 2]);
  }
}

/// A growing run of floats, kept unboxed: a region of grass is millions.
final class _Floats {
  Float32List _data = Float32List(256);
  int length = 0;

  void add(double value) {
    if (length == _data.length) {
      _data = Float32List(_data.length * 2)..setRange(0, length, _data);
    }
    _data[length++] = value;
  }

  /// The floats added, in a list of their own length.
  Float32List done() => _data.sublist(0, length);
}

/// What a region was placed against: its own revision, and for each of the
/// eight around it a revision and a fingerprint of the edge it shares.
class _Placed {
  _Placed({required this.revision, required this.edges, required this.groups});

  final int revision;
  final List<(int, int)> edges;
  final List<ScatterGroup> groups;
}

/// The [n]th number between 0 and 1 drawn for the spot [spot]. Each is its
/// own hash rather than the next of a sequence, so a layer that draws fewer
/// does not shift what the rest draw.
double _draw(int spot, int n) =>
    _mix(spot ^ ((n + 1) * 0x9E3779B9 & 0xFFFFFFFF)) / 4294967296.0;

/// Stirs a 32-bit number so that each bit in depends on every bit in.
///
/// Written with multiplications that stay exact in a double, so the web and
/// the native build lay the same pattern.
int _mix(int x) {
  var h = x & 0xFFFFFFFF;
  h ^= h >>> 16;
  h = _times(h, 0x7FEB352D);
  h ^= h >>> 15;
  h = _times(h, 0x846CA68B);
  h ^= h >>> 16;
  return h;
}

/// [a] × [b], both 32 bits, modulo 2³². In halves, because the whole product
/// is past what a double holds exactly.
int _times(int a, int b) =>
    (a * (b & 0xFFFF) + ((a * (b >>> 16)) & 0xFFFF) * 0x10000) & 0xFFFFFFFF;

/// An sRGB `0xRRGGBB` colour as linear RGB.
(double, double, double) _linear(int colour) => (
  _channel((colour >> 16) & 0xFF),
  _channel((colour >> 8) & 0xFF),
  _channel(colour & 0xFF),
);

/// One sRGB byte as a linear value, 0 to 1.
double _channel(int byte) {
  final value = byte / 255;
  return value <= 0.04045
      ? value / 12.92
      : math.pow((value + 0.055) / 1.055, 2.4).toDouble();
}

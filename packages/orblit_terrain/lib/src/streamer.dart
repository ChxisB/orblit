import 'dart:async';
import 'dart:math' as math;
import 'dart:typed_data';

import 'region.dart';
import 'terrain.dart';

/// Where a streamed terrain's regions are kept.
///
/// Declared here, beside the [TerrainStreamer] that calls it, and
/// implemented by whatever holds the files: a folder, a pack, a server, or a
/// function that makes ground from noise.
abstract interface class RegionSource {
  /// The regions there are. One that is not listed is never asked for.
  Iterable<RegionKey> get keys;

  /// The bytes of the region's file, as [TerrainRegion.decode] reads them.
  Future<Uint8List> read(RegionKey key);
}

/// Keeps the regions of a [Terrain] that are near a point and lets go of
/// the rest.
///
/// The renderer draws at most 256 regions, so a world of more cannot be
/// handed over whole. A streamer holds the ones within [loadRadius] of the
/// focus and puts them in and out of the terrain as the focus moves. A
/// renderer and a [ScatterPlacer] read the terrain's regions as they are
/// each frame, so they follow without being told.
///
/// Regions that were in the terrain before the streamer, and regions that
/// were edited after it loaded them, are the caller's. They are never
/// removed, and they count against [maxResident].
final class TerrainStreamer {
  TerrainStreamer({
    required this.terrain,
    required RegionSource source,
    this.loadRadius = 512,
    double? keepRadius,
    this.maxResident = 256,
    this.concurrency = 2,
  }) : keepRadius =
           keepRadius ?? loadRadius + terrain.regionSize * terrain.spacing,
       _keys = List.unmodifiable(source.keys),
       _source = source {
    if (!(loadRadius > 0) || !loadRadius.isFinite) {
      throw ArgumentError.value(loadRadius, 'loadRadius', 'Must be above 0.');
    }
    if (!(this.keepRadius >= loadRadius) || !this.keepRadius.isFinite) {
      throw ArgumentError.value(
        this.keepRadius,
        'keepRadius',
        'Must be at least the load radius, $loadRadius, or a region would be '
            'let go of as soon as it was loaded.',
      );
    }
    if (maxResident < 1) {
      throw ArgumentError.value(
        maxResident,
        'maxResident',
        'Must be 1 or more.',
      );
    }
    if (concurrency < 1) {
      throw ArgumentError.value(
        concurrency,
        'concurrency',
        'Must be 1 or more.',
      );
    }
  }

  final Terrain terrain;

  /// How far from the focus, in metres, a region's nearest edge may be and
  /// still be loaded.
  final double loadRadius;

  /// How far a loaded region may drift before it is let go of. Above
  /// [loadRadius], so a focus pacing a boundary does not load and drop the
  /// same region every frame.
  final double keepRadius;

  /// The most regions the terrain may hold, the caller's own included. The
  /// renderer's limit is 256.
  final int maxResident;

  /// How many reads are out at once.
  final int concurrency;

  final RegionSource _source;
  final List<RegionKey> _keys;

  /// The regions this streamer put in, each with the revision it had when it
  /// went in. A revision that has moved is an edit, and the region is then
  /// the caller's.
  final Map<RegionKey, int> _streamed = {};
  final Set<RegionKey> _loading = {};
  final Map<RegionKey, String> _failed = {};

  double _x = 0;
  double _z = 0;
  bool _draining = false;
  Future<void> _running = Future<void>.value();

  /// The regions this streamer is holding.
  Set<RegionKey> get resident => Set.unmodifiable(_streamed.keys);

  /// How many reads are out.
  int get loading => _loading.length;

  /// The regions that could not be read or were not a region, with why.
  /// They are not asked for again until [retryFailed].
  Map<RegionKey, String> get failures => Map.unmodifiable(_failed);

  /// Moves the focus to world ([x], [z]), lets go of what is out of range
  /// and starts reading what is in it, nearest first.
  ///
  /// Completes when everything within range is in or has failed. Calling it
  /// again while it runs moves the focus and returns the same run, so a
  /// frame loop may call it every frame without awaiting.
  Future<void> follow(double x, double z) {
    if (!x.isFinite || !z.isFinite) {
      throw ArgumentError('The focus must be a finite point: ($x, $z).');
    }
    _x = x;
    _z = z;
    _letGo();
    if (_draining) return _running;
    return _running = _drain();
  }

  /// Makes the regions that failed eligible to be read again at the next
  /// [follow].
  void retryFailed() => _failed.clear();

  Future<void> _drain() async {
    _draining = true;
    final reads = <RegionKey, Future<void>>{};
    try {
      while (true) {
        final missing = _missing();
        for (final key in missing) {
          if (reads.length >= concurrency) break;
          // A block body: `remove` returns this very future, and a
          // whenComplete callback that returns a future is waited on.
          reads[key] = _load(key).whenComplete(() {
            reads.remove(key);
          });
        }
        if (reads.isEmpty) break;
        await Future.any(reads.values);
      }
    } finally {
      _draining = false;
    }
  }

  Future<void> _load(RegionKey key) async {
    _loading.add(key);
    try {
      final bytes = await _source.read(key);
      _accept(key, TerrainRegion.decode(bytes).region);
    } on RegionFormatException catch (error) {
      _failed[key] = error.message;
    } on Exception catch (error) {
      // A source can fail in ways of its own: a dropped connection, a
      // missing file. Each is one region's failure, not the stream's.
      _failed[key] = '$error';
    } finally {
      _loading.remove(key);
    }
  }

  void _accept(RegionKey key, TerrainRegion region) {
    if (region.key != key) {
      _failed[key] = 'The file for $key holds the region at ${region.key}.';
    } else if (region.size != terrain.regionSize) {
      _failed[key] =
          'The file for $key is ${region.size} texels across and this '
          'terrain\'s regions are ${terrain.regionSize}.';
    } else if (terrain.regionAt(key) == null && _target().contains(key)) {
      // A read that lands after the focus has moved on is dropped, and a
      // region put in by someone else meanwhile is left alone.
      terrain.putRegion(region);
      _streamed[key] = region.revision;
    }
  }

  /// What the terrain should hold, nearest the focus first: everything in
  /// load range, then what is already held and still in keep range, cut to
  /// the room left once the caller's own regions are counted.
  List<RegionKey> _target() {
    final room = math.max(0, maxResident - _callersRegions());
    final near = <(double, RegionKey)>[];
    for (final key in _keys) {
      if (_failed.containsKey(key)) continue;
      final distance = _distanceTo(key);
      final range = _streamed.containsKey(key) ? keepRadius : loadRadius;
      if (distance <= range) near.add((distance, key));
    }
    near.sort(_nearestFirst);
    return [for (final (_, key) in near.take(room)) key];
  }

  int _callersRegions() {
    var count = 0;
    for (final region in terrain.regions) {
      if (!_streamed.containsKey(region.key)) count++;
    }
    return count;
  }

  /// What is wanted and neither held nor being read.
  List<RegionKey> _missing() => [
    for (final key in _target())
      if (!_streamed.containsKey(key) &&
          !_loading.contains(key) &&
          terrain.regionAt(key) == null)
        key,
  ];

  void _letGo() {
    final keep = _target().toSet();
    for (final key in _streamed.keys.toList()) {
      final region = terrain.regionAt(key);
      if (region != null && keep.contains(key)) continue;
      // Gone already, or edited: either way it is not the streamer's to
      // remove any more.
      if (region != null && region.revision == _streamed[key]) {
        terrain.removeRegion(key);
      }
      _streamed.remove(key);
    }
  }

  /// Metres from the focus to the nearest point of the region's square.
  double _distanceTo(RegionKey key) {
    final across = terrain.regionSize * terrain.spacing;
    final dx = _outside(_x, key.x * across, (key.x + 1) * across);
    final dz = _outside(_z, key.z * across, (key.z + 1) * across);
    return math.sqrt(dx * dx + dz * dz);
  }

  static double _outside(double at, double low, double high) =>
      at < low ? low - at : (at > high ? at - high : 0);

  static int _nearestFirst((double, RegionKey) a, (double, RegionKey) b) {
    final byDistance = a.$1.compareTo(b.$1);
    if (byDistance != 0) return byDistance;
    final byX = a.$2.x.compareTo(b.$2.x);
    return byX != 0 ? byX : a.$2.z.compareTo(b.$2.z);
  }
}

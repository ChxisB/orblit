import 'dart:async';
import 'dart:typed_data';

import 'package:orblit_terrain/orblit_terrain.dart';
import 'package:test/test.dart';

/// Regions 4 m across, so region x spans metres 4x to 4x + 4.
const int _size = 4;

Uint8List _file(RegionKey key, {int size = _size, RegionKey? named}) {
  final region = TerrainRegion(named ?? key, size);
  region.heights.fillRange(0, region.heights.length, key.x + 1.0);
  region.touch();
  return region.encode();
}

final class _Offline implements Exception {
  const _Offline();
}

final class _Source implements RegionSource {
  _Source(Iterable<RegionKey> keys, {this.hold = false})
    : files = {for (final key in keys) key: _file(key)};

  final Map<RegionKey, Uint8List> files;

  /// When set, a read waits for [release].
  final bool hold;

  final List<RegionKey> asked = [];
  final Map<RegionKey, Completer<Uint8List>> _held = {};
  final Set<RegionKey> offline = {};

  @override
  Iterable<RegionKey> get keys => files.keys;

  @override
  Future<Uint8List> read(RegionKey key) {
    asked.add(key);
    if (offline.contains(key)) return Future.error(const _Offline());
    if (!hold) return Future.value(files[key]);
    return (_held[key] = Completer<Uint8List>()).future;
  }

  void release(RegionKey key) => _held.remove(key)!.complete(files[key]);

  List<RegionKey> get waiting => _held.keys.toList();
}

Iterable<RegionKey> _line(int count) => [
  for (var x = 0; x < count; x++) RegionKey(x, 0),
];

Terrain _terrain() => Terrain(regionSize: _size);

Set<int> _columns(Terrain terrain) => {
  for (final region in terrain.regions) region.key.x,
};

void main() {
  group('following a focus', () {
    test('loads what is in range, nearest first, and nothing else', () async {
      final terrain = _terrain();
      final source = _Source(_line(10));
      final streamer = TerrainStreamer(
        terrain: terrain,
        source: source,
        loadRadius: 5,
        concurrency: 1,
      );

      await streamer.follow(2, 2);

      expect(_columns(terrain), {0, 1});
      expect(source.asked, [const RegionKey(0, 0), const RegionKey(1, 0)]);
      expect(streamer.resident, {const RegionKey(0, 0), const RegionKey(1, 0)});
    });

    test('puts in the regions the files describe', () async {
      final terrain = _terrain();
      final streamer = TerrainStreamer(
        terrain: terrain,
        source: _Source(_line(3)),
        loadRadius: 5,
      );

      await streamer.follow(2, 2);

      expect(terrain.regionAt(const RegionKey(1, 0))!.heightAt(0, 0), 2);
      expect(terrain.heightAt(5, 1), 2);
    });

    test('has at most `concurrency` reads out at once', () async {
      final terrain = _terrain();
      final source = _Source(_line(10), hold: true);
      final streamer = TerrainStreamer(
        terrain: terrain,
        source: source,
        loadRadius: 17,
        concurrency: 2,
      );

      final done = streamer.follow(2, 2);
      await pumpEventQueue();
      expect(source.waiting, hasLength(2));
      expect(streamer.loading, 2);

      source.release(const RegionKey(0, 0));
      await pumpEventQueue();
      expect(source.waiting, hasLength(2));
      expect(source.asked, hasLength(3));

      while (source.waiting.isNotEmpty) {
        source.release(source.waiting.first);
        await pumpEventQueue();
      }
      await done;
      expect(_columns(terrain), {0, 1, 2, 3, 4});
    });

    test('keeps a region between the load and keep radii', () async {
      final terrain = _terrain();
      final streamer = TerrainStreamer(
        terrain: terrain,
        source: _Source(_line(10)),
        loadRadius: 5,
        keepRadius: 9,
      );
      await streamer.follow(2, 2);
      expect(_columns(terrain), {0, 1});

      await streamer.follow(10, 2);
      // Region 0 is 6 m off: past the load radius, inside the keep radius.
      expect(_columns(terrain), {0, 1, 2, 3});

      await streamer.follow(16, 2);
      expect(_columns(terrain), {1, 2, 3, 4, 5});
    });

    test('does not read a region again that it is still holding', () async {
      final source = _Source(_line(10));
      final streamer = TerrainStreamer(
        terrain: _terrain(),
        source: source,
        loadRadius: 5,
      );
      await streamer.follow(2, 2);
      await streamer.follow(1, 2);
      await streamer.follow(2, 2);

      expect(source.asked, hasLength(2));
    });

    test('reads nothing when everything in range is held', () async {
      final source = _Source(_line(3));
      final streamer = TerrainStreamer(
        terrain: _terrain(),
        source: source,
        loadRadius: 50,
      );
      await streamer.follow(0, 0);
      source.asked.clear();

      await streamer.follow(1, 1);

      expect(source.asked, isEmpty);
    });
  });

  group('the limit', () {
    test('counts regions that were already there', () async {
      final terrain = _terrain()..addRegion(const RegionKey(-5, 0));
      final streamer = TerrainStreamer(
        terrain: terrain,
        source: _Source(_line(10)),
        loadRadius: 50,
        maxResident: 3,
      );

      await streamer.follow(2, 2);

      expect(_columns(terrain), {-5, 0, 1});
    });

    test('lets go of the far ones to make room for the near ones', () async {
      final terrain = _terrain();
      final streamer = TerrainStreamer(
        terrain: terrain,
        source: _Source(_line(10)),
        loadRadius: 50,
        maxResident: 3,
      );
      await streamer.follow(2, 2);
      expect(_columns(terrain), {0, 1, 2});

      await streamer.follow(34, 2);

      expect(_columns(terrain), {7, 8, 9});
    });

    test('is never exceeded while regions are on their way', () async {
      final terrain = _terrain();
      final source = _Source(_line(10), hold: true);
      final streamer = TerrainStreamer(
        terrain: terrain,
        source: source,
        loadRadius: 50,
        maxResident: 2,
        concurrency: 4,
      );

      final done = streamer.follow(2, 2);
      await pumpEventQueue();
      expect(source.waiting, hasLength(2));
      while (source.waiting.isNotEmpty) {
        source.release(source.waiting.first);
        await pumpEventQueue();
        expect(terrain.regions.length, lessThanOrEqualTo(2));
      }
      await done;
    });
  });

  group('what is not the streamer\'s', () {
    test('a region that was there first is left alone', () async {
      final own = TerrainRegion(const RegionKey(0, 0), _size);
      final terrain = _terrain()..putRegion(own);
      final source = _Source(_line(3));
      final streamer = TerrainStreamer(
        terrain: terrain,
        source: source,
        loadRadius: 5,
      );

      await streamer.follow(2, 2);
      await streamer.follow(1000, 2);

      expect(source.asked, [const RegionKey(1, 0)]);
      expect(terrain.regionAt(const RegionKey(0, 0)), same(own));
    });

    test('a region that was edited is kept when the focus leaves', () async {
      final terrain = _terrain();
      final streamer = TerrainStreamer(
        terrain: terrain,
        source: _Source(_line(10)),
        loadRadius: 5,
      );
      await streamer.follow(2, 2);
      terrain.regionAt(const RegionKey(0, 0))!.setHeight(1, 1, 9);

      await streamer.follow(1000, 2);

      expect(_columns(terrain), {0});
      expect(streamer.resident, isEmpty);
      expect(terrain.regionAt(const RegionKey(0, 0))!.heightAt(1, 1), 9);
    });

    test('an edited region is not read over when the focus returns', () async {
      final terrain = _terrain();
      final source = _Source(_line(10));
      final streamer = TerrainStreamer(
        terrain: terrain,
        source: source,
        loadRadius: 5,
      );
      await streamer.follow(2, 2);
      terrain.regionAt(const RegionKey(0, 0))!.setHeight(1, 1, 9);
      await streamer.follow(1000, 2);
      source.asked.clear();

      await streamer.follow(2, 2);

      expect(source.asked, isNot(contains(const RegionKey(0, 0))));
      expect(terrain.regionAt(const RegionKey(0, 0))!.heightAt(1, 1), 9);
    });

    test('a region the caller removes is read again', () async {
      final terrain = _terrain();
      final source = _Source(_line(3));
      final streamer = TerrainStreamer(
        terrain: terrain,
        source: source,
        loadRadius: 5,
      );
      await streamer.follow(2, 2);
      terrain.removeRegion(const RegionKey(0, 0));

      await streamer.follow(2, 2);

      expect(terrain.regionAt(const RegionKey(0, 0)), isNotNull);
      expect(source.asked.where((key) => key.x == 0), hasLength(2));
    });
  });

  group('a read that arrives late', () {
    test('is dropped when the focus has moved on', () async {
      final terrain = _terrain();
      final source = _Source(_line(10), hold: true);
      final streamer = TerrainStreamer(
        terrain: terrain,
        source: source,
        loadRadius: 5,
        concurrency: 1,
      );
      final first = streamer.follow(2, 2);
      await pumpEventQueue();
      expect(source.waiting, [const RegionKey(0, 0)]);

      final second = streamer.follow(1000, 2);
      source.release(const RegionKey(0, 0));
      await Future.wait([first, second]);

      expect(terrain.regions, isEmpty);
      expect(streamer.resident, isEmpty);
    });

    test('follows the focus that was given while it ran', () async {
      final terrain = _terrain();
      final source = _Source(_line(10), hold: true);
      final streamer = TerrainStreamer(
        terrain: terrain,
        source: source,
        loadRadius: 3,
        concurrency: 1,
      );
      final first = streamer.follow(2, 2);
      await pumpEventQueue();
      final second = streamer.follow(34, 2);

      while (source.waiting.isNotEmpty) {
        source.release(source.waiting.first);
        await pumpEventQueue();
      }
      await Future.wait([first, second]);

      expect(_columns(terrain), {7, 8, 9});
    });

    test('is dropped when the caller has put a region there', () async {
      final terrain = _terrain();
      final source = _Source(_line(3), hold: true);
      final streamer = TerrainStreamer(
        terrain: terrain,
        source: source,
        loadRadius: 5,
        concurrency: 1,
      );
      final done = streamer.follow(2, 2);
      await pumpEventQueue();
      final own = TerrainRegion(const RegionKey(0, 0), _size);
      terrain.putRegion(own);

      while (source.waiting.isNotEmpty) {
        source.release(source.waiting.first);
        await pumpEventQueue();
      }
      await done;

      expect(terrain.regionAt(const RegionKey(0, 0)), same(own));
      expect(streamer.resident, isNot(contains(const RegionKey(0, 0))));
    });
  });

  group('failures', () {
    test('a read that fails is one region\'s failure', () async {
      final terrain = _terrain();
      final source = _Source(_line(3))..offline.add(const RegionKey(0, 0));
      final streamer = TerrainStreamer(
        terrain: terrain,
        source: source,
        loadRadius: 50,
      );

      await streamer.follow(2, 2);

      expect(_columns(terrain), {1, 2});
      expect(streamer.failures.keys, [const RegionKey(0, 0)]);
    });

    test('is not asked for again until told to retry', () async {
      final source = _Source(_line(3))..offline.add(const RegionKey(0, 0));
      final streamer = TerrainStreamer(
        terrain: _terrain(),
        source: source,
        loadRadius: 50,
      );
      await streamer.follow(2, 2);
      await streamer.follow(2, 2);
      expect(source.asked.where((key) => key.x == 0), hasLength(1));

      source.offline.clear();
      streamer.retryFailed();
      await streamer.follow(2, 2);

      expect(source.asked.where((key) => key.x == 0), hasLength(2));
      expect(streamer.failures, isEmpty);
    });

    test('bytes that are not a region are a failure that says so', () async {
      final terrain = _terrain();
      final source = _Source(_line(2));
      source.files[const RegionKey(0, 0)] = Uint8List.fromList([1, 2, 3]);
      final streamer = TerrainStreamer(
        terrain: terrain,
        source: source,
        loadRadius: 50,
      );

      await streamer.follow(2, 2);

      expect(_columns(terrain), {1});
      expect(
        streamer.failures[const RegionKey(0, 0)],
        contains('not a region'),
      );
    });

    test('a file for another region is refused', () async {
      final terrain = _terrain();
      final source = _Source(_line(2));
      source.files[const RegionKey(0, 0)] = _file(
        const RegionKey(0, 0),
        named: const RegionKey(7, 7),
      );
      final streamer = TerrainStreamer(
        terrain: terrain,
        source: source,
        loadRadius: 50,
      );

      await streamer.follow(2, 2);

      expect(terrain.regionAt(const RegionKey(0, 0)), isNull);
      expect(streamer.failures[const RegionKey(0, 0)], contains('(7, 7)'));
    });

    test('a file of another size is refused', () async {
      final terrain = _terrain();
      final source = _Source(_line(2));
      source.files[const RegionKey(0, 0)] = _file(
        const RegionKey(0, 0),
        size: 8,
      );
      final streamer = TerrainStreamer(
        terrain: terrain,
        source: source,
        loadRadius: 50,
      );

      await streamer.follow(2, 2);

      expect(terrain.regionAt(const RegionKey(0, 0)), isNull);
      expect(streamer.failures[const RegionKey(0, 0)], contains('8 texels'));
    });
  });

  group('asking for something it cannot do', () {
    test('refuses a focus that is not a point', () {
      final streamer = TerrainStreamer(
        terrain: _terrain(),
        source: _Source(_line(1)),
      );
      expect(() => streamer.follow(double.nan, 0), throwsArgumentError);
      expect(() => streamer.follow(0, double.infinity), throwsArgumentError);
    });

    test('refuses radii and limits that cannot work', () {
      final source = _Source(_line(1));
      TerrainStreamer make({
        double radius = 10,
        double? keep,
        int resident = 8,
        int concurrency = 1,
      }) => TerrainStreamer(
        terrain: _terrain(),
        source: source,
        loadRadius: radius,
        keepRadius: keep,
        maxResident: resident,
        concurrency: concurrency,
      );

      expect(() => make(radius: 0), throwsArgumentError);
      expect(() => make(radius: double.infinity), throwsArgumentError);
      expect(() => make(keep: 9), throwsArgumentError);
      expect(() => make(resident: 0), throwsArgumentError);
      expect(() => make(concurrency: 0), throwsArgumentError);
    });

    test('keeps a region one region wide past the load radius by default', () {
      final streamer = TerrainStreamer(
        terrain: Terrain(regionSize: 8, spacing: 2),
        source: _Source(const []),
        loadRadius: 100,
      );
      expect(streamer.keepRadius, 116);
    });
  });

  group('the metres', () {
    test('measure from the nearest edge of a region, not its corner', () async {
      final terrain = Terrain(regionSize: _size, spacing: 2);
      final source = _Source([
        const RegionKey(0, 0),
        const RegionKey(1, 0),
        const RegionKey(2, 0),
        const RegionKey(-1, 0),
      ]);
      final streamer = TerrainStreamer(
        terrain: terrain,
        source: source,
        loadRadius: 3,
      );

      // A region is 8 m across here. The focus is 1 m inside region 0, so
      // region -1 is 1 m off and region 1 is 7 m off.
      await streamer.follow(1, 4);

      expect(_columns(terrain), {-1, 0});
    });

    test('count across as well as along', () async {
      final terrain = _terrain();
      final source = _Source([
        const RegionKey(0, 0),
        const RegionKey(0, 1),
        const RegionKey(0, 5),
      ]);
      final streamer = TerrainStreamer(
        terrain: terrain,
        source: source,
        loadRadius: 5,
      );

      await streamer.follow(2, 2);

      expect(terrain.regionAt(const RegionKey(0, 1)), isNotNull);
      expect(terrain.regionAt(const RegionKey(0, 5)), isNull);
    });
  });
}

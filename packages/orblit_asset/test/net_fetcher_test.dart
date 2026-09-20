import 'dart:async';
import 'dart:convert';
import 'dart:math';
import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:test/test.dart';

import 'net_ktx2_fixture.dart';

void main() {
  final origin = AssetOrigin.parse('https://cdn.example.com/game/');
  AssetId id(String path) => AssetId.parse(path);
  Uri url(String path) => origin.urlOf(id(path));
  List<int> body(String text) => utf8.encode(text);

  // No noise in the backoff, so a test that counts waits gets the same wait
  // every run.
  final steady = _Steady();

  AssetFetcher fetcherFor(
    MapTransport transport, {
    ContentStore? store,
    FetchRecords? records,
    AssetManifest? manifest,
    int concurrent = 4,
    int attempts = 3,
    AssetOrigin? from,
  }) => AssetFetcher(
    origin: from ?? origin,
    transport: transport,
    store: store,
    records: records,
    manifest: manifest,
    concurrent: concurrent,
    attempts: attempts,
    backoff: Duration.zero,
    jitter: steady,
  );

  group('fetching', () {
    test('hands over the bytes the server sent', () async {
      final transport = MapTransport({
        url('models/robot.glb'): TransportPage(body('a robot')),
      });
      final fetcher = fetcherFor(transport);

      expect(
        utf8.decode(await fetcher.read(id('models/robot.glb'))),
        'a robot',
      );
      expect(transport.sent, hasLength(1));
      expect(transport.sent.single.url, url('models/robot.glb'));
    });

    test('asks once however many callers want the same thing', () async {
      final transport = MapTransport({
        url('t/wall.ktx2'): TransportPage(body('bricks')),
      });
      final fetcher = fetcherFor(transport);

      final together = await Future.wait([
        fetcher.read(id('t/wall.ktx2')),
        fetcher.read(id('t/wall.ktx2')),
        fetcher.read(id('t/wall.ktx2')),
      ]);

      expect(together.map(utf8.decode), everyElement('bricks'));
      expect(
        transport.sent,
        hasLength(1),
        reason: 'three callers, one download',
      );
    });

    test('a 404 becomes a failure that names the status', () async {
      final fetcher = fetcherFor(MapTransport({}));

      await expectLater(
        fetcher.read(id('models/missing.glb')),
        throwsA(
          isA<FetchFailed>()
              .having((f) => f.cause, 'cause', 404)
              .having((f) => f.attempts, 'attempts', 1),
        ),
      );
    });

    test('reports progress, ending at the whole', () async {
      final transport = MapTransport({
        url('big.bin'): TransportPage(List.filled(400, 7), cut: 100),
      });
      final fetcher = fetcherFor(transport);

      final seen = <FetchProgress>[];
      await fetcher.fetch(id('big.bin'), onProgress: seen.add).bytes;

      expect(seen.map((p) => p.received), [100, 200, 300, 400]);
      expect(seen.last.total, 400);
      expect(seen.last.fraction, 1.0);
    });
  });

  group('the queue', () {
    test('takes what is on screen before what is not', () async {
      final transport = MapTransport({
        for (final name in ['a', 'b', 'c'])
          url('$name.bin'): TransportPage(body(name)),
      });
      final fetcher = fetcherFor(transport, concurrent: 1);

      // The first one starts at once and holds the only slot; the other two
      // queue behind it, and the urgent one goes first whatever the order
      // they were asked for in.
      final jobs = [
        fetcher.fetch(id('a.bin'), urgency: FetchUrgency.eventually),
        fetcher.fetch(id('b.bin'), urgency: FetchUrgency.eventually),
        fetcher.fetch(id('c.bin'), urgency: FetchUrgency.onScreen),
      ];
      await Future.wait(jobs.map((j) => j.bytes));

      expect(transport.sent.map((r) => r.url.pathSegments.last), [
        'a.bin',
        'c.bin',
        'b.bin',
      ]);
    });

    test('something already queued can jump the queue', () async {
      final transport = MapTransport({
        for (final name in ['a', 'b', 'c'])
          url('$name.bin'): TransportPage(body(name)),
      });
      final fetcher = fetcherFor(transport, concurrent: 1);

      final held = fetcher.fetch(id('a.bin'), urgency: FetchUrgency.onScreen);
      final later = fetcher.fetch(
        id('b.bin'),
        urgency: FetchUrgency.eventually,
      );
      final next = fetcher.fetch(id('c.bin'), urgency: FetchUrgency.eventually);
      // It came into view while it was waiting.
      final again = fetcher.fetch(id('c.bin'), urgency: FetchUrgency.onScreen);

      await Future.wait([held.bytes, later.bytes, next.bytes, again.bytes]);

      expect(transport.sent.map((r) => r.url.pathSegments.last), [
        'a.bin',
        'c.bin',
        'b.bin',
      ]);
    });

    test('counts what is waiting and what is running', () async {
      final transport = MapTransport({
        for (final name in ['a', 'b', 'c'])
          url('$name.bin'): TransportPage(body(name)),
      });
      final fetcher = fetcherFor(transport, concurrent: 1);

      final jobs = [
        for (final name in ['a', 'b', 'c']) fetcher.fetch(id('$name.bin')),
      ];
      expect(fetcher.running, 1);
      expect(fetcher.waiting, 2);

      await Future.wait(jobs.map((j) => j.bytes));
      expect(fetcher.running, 0);
      expect(fetcher.waiting, 0);
    });
  });

  group('giving up on one', () {
    test('one caller leaving does not stop the others', () async {
      final transport = MapTransport({
        url('shared.bin'): TransportPage(body('kept')),
      });
      final fetcher = fetcherFor(transport);

      final leaving = fetcher.fetch(id('shared.bin'));
      final staying = fetcher.fetch(id('shared.bin'));
      leaving.cancel();

      expect(utf8.decode(await staying.bytes), 'kept');
      await expectLater(leaving.bytes, throwsA(isA<FetchCancelled>()));
      expect(transport.sent, hasLength(1));
    });

    test('a download nobody waits for any more is never sent', () async {
      final transport = MapTransport({
        for (final name in ['a', 'b'])
          url('$name.bin'): TransportPage(body(name)),
      });
      final fetcher = fetcherFor(transport, concurrent: 1);

      final first = fetcher.fetch(id('a.bin'));
      final dropped = fetcher.fetch(id('b.bin'));
      dropped.cancel();

      await first.bytes;
      await pumpEventQueue();

      expect(transport.sent.map((r) => r.url.pathSegments.last), ['a.bin']);
      expect(fetcher.waiting, 0);
    });

    test('an abandoned download gives its slot back', () async {
      // The bug this is here for: cancelling mid-body left the read waiting
      // on a future nobody would complete, so the slot it held was never
      // returned and everything queued behind it stopped.
      final held = _HeldTransport(body('slow'));
      final fetcher = AssetFetcher(
        origin: origin,
        transport: held,
        concurrent: 1,
        backoff: Duration.zero,
        jitter: steady,
      );

      final slow = fetcher.fetchUrl(url('slow.bin'));
      await pumpEventQueue();
      expect(held.asked, hasLength(1));

      final behind = fetcher.fetchUrl(url('quick.bin'));
      expect(fetcher.waiting, 1);

      slow.cancel();
      await expectLater(slow.bytes, throwsA(isA<FetchCancelled>()));
      await pumpEventQueue();

      held.finish('quick.bin', body('quick'));
      expect(utf8.decode(await behind.bytes), 'quick');
    });

    test('asking again after everyone gave up starts a new download', () async {
      // Guards the other half of the same bug: a download left in the live
      // map after it was abandoned would be joined by the next caller, who
      // would then wait for bytes nobody was fetching.
      final transport = MapTransport({
        url('again.bin'): TransportPage(body('second time')),
      });
      final fetcher = fetcherFor(transport);

      fetcher.fetch(id('again.bin')).cancel();
      await pumpEventQueue();

      expect(utf8.decode(await fetcher.read(id('again.bin'))), 'second time');
    });

    test('closing fails everything in flight', () async {
      final held = _HeldTransport(body('never'));
      final fetcher = AssetFetcher(
        origin: origin,
        transport: held,
        backoff: Duration.zero,
        jitter: steady,
      );

      final job = fetcher.fetchUrl(url('gone.bin'));
      await pumpEventQueue();
      fetcher.close();

      await expectLater(job.bytes, throwsA(isA<FetchCancelled>()));
      expect(() => fetcher.fetchUrl(url('gone.bin')), throwsStateError);
    });
  });

  group('trying again', () {
    test('a lost connection is tried again', () async {
      final transport = MapTransport({
        url('flaky.bin'): TransportPage(
          body('got there'),
          faults: [Fault.thrown(), Fault.thrown()],
        ),
      });
      final fetcher = fetcherFor(transport, attempts: 3);

      expect(utf8.decode(await fetcher.read(id('flaky.bin'))), 'got there');
      expect(transport.sent, hasLength(3));
    });

    test('a 503 is tried again; a 403 is not', () async {
      final transport = MapTransport({
        url('busy.bin'): TransportPage(
          body('eventually'),
          faults: [Fault.status(503)],
        ),
        url('shut.bin'): TransportPage(
          body('never seen'),
          faults: [Fault.status(403)],
        ),
      });
      final fetcher = fetcherFor(transport, attempts: 3);

      expect(utf8.decode(await fetcher.read(id('busy.bin'))), 'eventually');
      await expectLater(
        fetcher.read(id('shut.bin')),
        throwsA(isA<FetchFailed>().having((f) => f.cause, 'cause', 403)),
      );
      expect(
        transport.sent.where((r) => r.url == url('shut.bin')),
        hasLength(1),
        reason: 'a 403 is an answer, not a bad moment',
      );
    });

    test('running out of attempts says how many there were', () async {
      final transport = MapTransport({
        url('down.bin'): TransportPage(
          body('unreachable'),
          faults: [Fault.status(500), Fault.status(500)],
        ),
      });
      final fetcher = fetcherFor(transport, attempts: 2);

      await expectLater(
        fetcher.read(id('down.bin')),
        throwsA(
          isA<FetchFailed>()
              .having((f) => f.cause, 'cause', 500)
              .having((f) => f.attempts, 'attempts', 2),
        ),
      );
    });

    test('waits longer before each attempt', () async {
      final transport = MapTransport({
        url('slowly.bin'): TransportPage(
          body('at last'),
          faults: [Fault.thrown(), Fault.thrown()],
        ),
      });
      final fetcher = AssetFetcher(
        origin: origin,
        transport: transport,
        attempts: 3,
        backoff: const Duration(milliseconds: 20),
        jitter: steady,
      );

      final started = DateTime.now();
      await fetcher.read(id('slowly.bin'));
      final took = DateTime.now().difference(started);

      // 20ms then 40ms, so at least 60ms whatever else the machine is doing.
      expect(took, greaterThanOrEqualTo(const Duration(milliseconds: 55)));
    });
  });

  group('resuming', () {
    test('picks up where the connection dropped', () async {
      final whole = List.generate(500, (i) => i % 251);
      final transport = MapTransport({
        url('capture.spz'): TransportPage(whole, cut: 100, stopAfter: 300),
      });
      final fetcher = fetcherFor(transport, attempts: 3);

      expect(await fetcher.read(id('capture.spz')), whole);
      expect(transport.sent, hasLength(2));
      expect(
        transport.sent[1].from,
        300,
        reason: 'the second request asks for the rest, not the whole thing',
      );
    });

    test('starts over when the server ignores the range', () async {
      // A 200 to a resumed request means the bytes already held are the start
      // of something else. Keeping them would splice two files together.
      final whole = List.generate(300, (i) => i % 251);
      final page = _IgnoresRanges(whole, stopAfter: 120);
      final fetcher = AssetFetcher(
        origin: origin,
        transport: page,
        attempts: 3,
        backoff: Duration.zero,
        jitter: steady,
      );

      expect(await fetcher.fetchUrl(url('whole.bin')).bytes, whole);
    });
  });

  group('asking whether it changed', () {
    test('sends the tag it holds and takes a 304 from the cache', () async {
      final store = MemoryContentStore();
      final records = MemoryFetchRecords();
      final page = TransportPage(body('version one'), etag: '"v1"');

      final first = fetcherFor(
        MapTransport({url('tagged.bin'): page}),
        store: store,
        records: records,
      );
      expect(utf8.decode(await first.read(id('tagged.bin'))), 'version one');

      final again = MapTransport({url('tagged.bin'): page});
      final second = fetcherFor(again, store: store, records: records);

      expect(utf8.decode(await second.read(id('tagged.bin'))), 'version one');
      expect(again.sent.single.ifNoneMatch, '"v1"');
    });

    test('a 304 with the bytes gone fetches them again', () async {
      final records = MemoryFetchRecords();
      final page = TransportPage(body('still here'), etag: '"v1"');

      final first = fetcherFor(
        MapTransport({url('tagged.bin'): page}),
        store: MemoryContentStore(),
        records: records,
      );
      await first.read(id('tagged.bin'));

      // A new store: the system emptied the cache but the note survived.
      final again = MapTransport({url('tagged.bin'): page});
      final second = fetcherFor(
        again,
        store: MemoryContentStore(),
        records: records,
      );

      expect(utf8.decode(await second.read(id('tagged.bin'))), 'still here');
      expect(again.sent, hasLength(2));
      expect(again.sent[1].ifNoneMatch, isNull);
    });
  });

  group('checking against the manifest', () {
    AssetManifest manifestOf(String path, List<int> bytes) => AssetManifest(
      entries: {id(path): AssetEntry(ContentHash.of(bytes), bytes.length)},
    );

    test('a copy the manifest vouches for needs no request', () async {
      final bytes = body('already here');
      final store = MemoryContentStore();
      await store.put(bytes);

      final transport = MapTransport({
        url('known.bin'): TransportPage(body('would be fetched')),
      });
      final fetcher = fetcherFor(
        transport,
        store: store,
        manifest: manifestOf('known.bin', bytes),
      );

      expect(utf8.decode(await fetcher.read(id('known.bin'))), 'already here');
      expect(
        transport.sent,
        isEmpty,
        reason: 'the hash is a promise about the bytes, not about the URL',
      );
    });

    test('bytes that do not match are refused and not tried again', () async {
      final transport = MapTransport({
        url('swapped.bin'): TransportPage(body('something else')),
      });
      final fetcher = fetcherFor(
        transport,
        manifest: manifestOf('swapped.bin', body('what was promised')),
        attempts: 3,
      );

      await expectLater(
        fetcher.read(id('swapped.bin')),
        throwsA(
          isA<FetchCorrupt>()
              .having(
                (f) => f.wanted,
                'wanted',
                ContentHash.of(body('what was promised')),
              )
              .having(
                (f) => f.got,
                'got',
                ContentHash.of(body('something else')),
              ),
        ),
      );
      expect(
        transport.sent,
        hasLength(1),
        reason: 'asking the same server again gets the same wrong bytes',
      );
    });
  });

  group('being offline', () {
    test('falls back to the copy already held', () async {
      final store = MemoryContentStore();
      final records = MemoryFetchRecords();
      final warm = fetcherFor(
        MapTransport({url('cached.bin'): TransportPage(body('from before'))}),
        store: store,
        records: records,
      );
      await warm.read(id('cached.bin'));

      final gone = MapTransport({
        url('cached.bin'): TransportPage(
          body('unreachable'),
          faults: [Fault.thrown(), Fault.thrown(), Fault.thrown()],
        ),
      });
      final offline = fetcherFor(
        gone,
        store: store,
        records: records,
        attempts: 3,
      );

      expect(utf8.decode(await offline.read(id('cached.bin'))), 'from before');
      expect(gone.sent, hasLength(3));
    });

    test('with nothing held it fails rather than hanging', () async {
      final transport = MapTransport({
        url('cold.bin'): TransportPage(
          body('unreachable'),
          faults: [Fault.thrown(), Fault.thrown()],
        ),
      });
      final fetcher = fetcherFor(transport, attempts: 2);

      await expectLater(
        fetcher.read(id('cold.bin')),
        throwsA(
          isA<FetchFailed>().having(
            (f) => f.cause,
            'cause',
            isA<SocketFailure>(),
          ),
        ),
      );
    });
  });

  group('the limits', () {
    test(
      'a declared length over the limit is refused before reading',
      () async {
        final small = AssetOrigin.parse(
          'https://cdn.example.com/game/',
          policy: const FetchPolicy(maxBytes: 16),
        );
        final transport = MapTransport({
          small.urlOf(id('huge.bin')): TransportPage(List.filled(64, 1)),
        });
        final fetcher = fetcherFor(transport, from: small);

        await expectLater(
          fetcher.read(id('huge.bin')),
          throwsA(
            isA<FetchRefused>().having(
              (f) => f.reason,
              'reason',
              contains('64 bytes and the policy allows 16'),
            ),
          ),
        );
        expect(transport.sent, hasLength(1), reason: 'never retried');
      },
    );

    test('a body that outgrows the limit is stopped partway', () async {
      final small = AssetOrigin.parse(
        'https://cdn.example.com/game/',
        policy: const FetchPolicy(maxBytes: 100),
      );
      // No declared length, so the only place to catch it is while it arrives.
      final transport = _Undeclared(List.filled(1000, 1), cut: 50);
      final fetcher = AssetFetcher(
        origin: small,
        transport: transport,
        attempts: 1,
        backoff: Duration.zero,
        jitter: steady,
      );

      await expectLater(
        fetcher.fetchUrl(small.urlOf(id('stream.bin'))).bytes,
        throwsA(isA<FetchRefused>()),
      );
      expect(
        transport.handed,
        lessThan(1000),
        reason: 'it stopped at the byte that passed the limit',
      );
    });

    test('a picture too big to decode is refused from its header', () async {
      final small = AssetOrigin.parse(
        'https://cdn.example.com/game/',
        policy: const FetchPolicy(maxPixels: 1000),
      );
      final transport = _Undeclared(_pngHeader(40000, 40000, 4000), cut: 64);
      final fetcher = AssetFetcher(
        origin: small,
        transport: transport,
        attempts: 1,
        backoff: Duration.zero,
        jitter: steady,
      );

      await expectLater(
        fetcher.fetchUrl(small.urlOf(id('vast.png'))).bytes,
        throwsA(
          isA<FetchRefused>().having(
            (f) => f.reason,
            'reason',
            contains('40000×40000'),
          ),
        ),
      );
      expect(
        transport.handed,
        lessThanOrEqualTo(128),
        reason: 'the header is enough; the rest is never pulled',
      );
    });
  });

  group('the coarse levels first', () {
    test('hands them over before the rest of the file has arrived', () async {
      final whole = mippedKtx2(width: 512, height: 512, levels: 10);
      final chain = Ktx2Chain.read(whole)!;
      final transport = MapTransport({
        url('t/wall.ktx2'): TransportPage(whole, cut: 1024),
      });
      final fetcher = fetcherFor(transport);

      Uint8List? rough;
      var roughAt = -1;
      var received = 0;
      final job = fetcher.fetch(
        id('t/wall.ktx2'),
        roughSize: 64,
        onProgress: (progress) => received = progress.received,
        onRough: (bytes) {
          rough = bytes;
          roughAt = received;
        },
      );
      final all = await job.bytes;

      expect(rough, isNotNull, reason: 'a mipped texture has coarse levels');
      // Handed over part way, not at the end: the point of the exercise.
      expect(roughAt, lessThan(all.length));
      expect(rough!.length, lessThan(whole.length));

      final smaller = Ktx2Chain.read(rough!)!;
      expect(smaller.width, 64);
      expect(smaller.levels, hasLength(10 - chain.levelAtLeast(64)!));
      expect(all, whole);
    });

    test('costs no extra request', () async {
      final whole = mippedKtx2(width: 512, height: 512, levels: 10);
      final transport = MapTransport({
        url('t/wall.ktx2'): TransportPage(whole, cut: 1024),
      });
      final fetcher = fetcherFor(transport);

      await fetcher.fetch(id('t/wall.ktx2'), onRough: (_) {}).bytes;
      expect(transport.sent, hasLength(1));
    });

    test('nothing for a texture that arrived in one piece', () async {
      // A stand-in for something already here is a wasted upload and a
      // visible flash.
      final whole = mippedKtx2(width: 512, height: 512, levels: 10);
      final transport = MapTransport({
        url('t/wall.ktx2'): TransportPage(whole),
      });
      final fetcher = fetcherFor(transport);

      var offered = false;
      await fetcher
          .fetch(id('t/wall.ktx2'), onRough: (_) => offered = true)
          .bytes;
      expect(offered, isFalse);
    });

    test('nothing for something that is not a mipped texture', () async {
      final transport = MapTransport({
        url('models/robot.glb'): TransportPage(
          List<int>.filled(40000, 7),
          cut: 1024,
        ),
      });
      final fetcher = fetcherFor(transport);

      var offered = false;
      await fetcher
          .fetch(id('models/robot.glb'), onRough: (_) => offered = true)
          .bytes;
      expect(offered, isFalse);
    });

    test('once, however many chunks follow', () async {
      final whole = mippedKtx2(width: 512, height: 512, levels: 10);
      final transport = MapTransport({
        url('t/wall.ktx2'): TransportPage(whole, cut: 256),
      });
      final fetcher = fetcherFor(transport);

      var offers = 0;
      await fetcher
          .fetch(id('t/wall.ktx2'), roughSize: 32, onRough: (_) => offers++)
          .bytes;
      expect(offers, 1);
    });

    test('every caller sharing the download gets them', () async {
      final whole = mippedKtx2(width: 512, height: 512, levels: 10);
      final transport = MapTransport({
        url('t/wall.ktx2'): TransportPage(whole, cut: 512),
      });
      final fetcher = fetcherFor(transport);

      final offered = <int>[];
      final first = fetcher.fetch(
        id('t/wall.ktx2'),
        roughSize: 64,
        onRough: (bytes) => offered.add(bytes.length),
      );
      final second = fetcher.fetch(
        id('t/wall.ktx2'),
        roughSize: 64,
        onRough: (bytes) => offered.add(bytes.length),
      );
      await Future.wait([first.bytes, second.bytes]);

      expect(offered, hasLength(2));
      expect(offered.first, offered.last);
      expect(transport.sent, hasLength(1));
    });

    test('the smallest anybody asked for is the one that is built', () async {
      // It arrives first, and it is still better than nothing for whoever
      // wanted a sharper one.
      final whole = mippedKtx2(width: 512, height: 512, levels: 10);
      final transport = MapTransport({
        url('t/wall.ktx2'): TransportPage(whole, cut: 512),
      });
      final fetcher = fetcherFor(transport);

      Uint8List? built;
      final sharp = fetcher.fetch(
        id('t/wall.ktx2'),
        roughSize: 256,
        onRough: (bytes) => built = bytes,
      );
      final blurry = fetcher.fetch(
        id('t/wall.ktx2'),
        roughSize: 32,
        onRough: (_) {},
      );
      await Future.wait([sharp.bytes, blurry.bytes]);

      expect(Ktx2Chain.read(built!)!.width, 32);
    });
  });

  group('as an asset source', () {
    test(
      'a missing asset is an AssetNotFound, so layers fall through',
      () async {
        final transport = MapTransport({
          url('there.bin'): TransportPage(body('found')),
        });
        final network = NetworkAssetSource(fetcherFor(transport));

        expect(utf8.decode(await network.read(id('there.bin'))), 'found');
        await expectLater(
          network.read(id('nowhere.bin')),
          throwsA(isA<AssetNotFound>()),
        );
      },
    );

    test('a server having a moment is not a missing asset', () async {
      // The distinction that matters to LayeredAssetSource: falling through
      // on a 500 would quietly serve a stale layer whenever a CDN hiccupped.
      final transport = MapTransport({
        url('wobbly.bin'): TransportPage(
          body('never seen'),
          faults: [Fault.status(500), Fault.status(500)],
        ),
      });
      final network = NetworkAssetSource(fetcherFor(transport, attempts: 2));

      await expectLater(
        network.read(id('wobbly.bin')),
        throwsA(isA<FetchFailed>()),
      );
    });
  });
}

/// A jitter that does not jitter, so a wait is the wait that was asked for.
class _Steady implements Random {
  @override
  double nextDouble() => 0.5;

  @override
  bool nextBool() => false;

  @override
  int nextInt(int max) => 0;
}

/// A transport whose replies finish when the test says so.
class _HeldTransport implements AssetTransport {
  _HeldTransport(this.bytes);

  final List<int> bytes;
  final List<FetchRequest> asked = [];
  final Map<String, StreamController<List<int>>> _open = {};

  @override
  Future<FetchReply> send(FetchRequest request) async {
    asked.add(request);
    final name = request.url.pathSegments.last;
    final tap = _open[name] = StreamController<List<int>>();
    return FetchReply(status: 200, body: tap.stream, length: bytes.length);
  }

  void finish(String name, List<int> sending) {
    final tap = _open.remove(name)!;
    tap.add(sending);
    unawaited(tap.close());
  }

  @override
  void close() {
    for (final tap in _open.values) {
      unawaited(tap.close());
    }
    _open.clear();
  }
}

/// A transport that answers 200 to a resumed request, as a server with no
/// range support does.
class _IgnoresRanges implements AssetTransport {
  _IgnoresRanges(this.bytes, {required this.stopAfter});

  final List<int> bytes;
  int? stopAfter;

  @override
  Future<FetchReply> send(FetchRequest request) async {
    final cut = stopAfter;
    stopAfter = null;
    return FetchReply(
      status: 200,
      length: bytes.length,
      body: cut == null ? Stream.value(bytes) : _short(bytes, cut),
    );
  }

  static Stream<List<int>> _short(List<int> bytes, int after) async* {
    yield bytes.sublist(0, after);
    throw const SocketFailure('dropped');
  }

  @override
  void close() {}
}

/// A transport that declares no length and counts what was actually pulled.
class _Undeclared implements AssetTransport {
  _Undeclared(this.bytes, {required this.cut});

  final List<int> bytes;
  final int cut;
  var handed = 0;

  @override
  Future<FetchReply> send(FetchRequest request) async =>
      FetchReply(status: 200, body: _feed());

  Stream<List<int>> _feed() async* {
    for (var at = 0; at < bytes.length; at += cut) {
      final end = min(at + cut, bytes.length);
      handed = end;
      yield bytes.sublist(at, end);
    }
  }

  @override
  void close() {}
}

/// A PNG that says it is [width] by [height], padded out to [length] bytes.
Uint8List _pngHeader(int width, int height, int length) {
  final bytes = Uint8List(length);
  bytes.setAll(0, const [137, 80, 78, 71, 13, 10, 26, 10]);
  final view = ByteData.view(bytes.buffer);
  view.setUint32(8, 13); // IHDR length
  bytes.setAll(12, utf8.encode('IHDR'));
  view.setUint32(16, width);
  view.setUint32(20, height);
  return bytes;
}

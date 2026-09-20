import 'dart:async';
import 'dart:convert';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:test/test.dart';

void main() {
  final origin = AssetOrigin.parse('https://cdn.example.com/game/');
  AssetId id(String path) => AssetId.parse(path);
  Uri url(String path) => origin.urlOf(id(path));
  List<int> body(String text) => utf8.encode(text);

  AssetFetcher fetcherFor(MapTransport transport, {ContentStore? store}) =>
      AssetFetcher(
        origin: origin,
        transport: transport,
        store: store,
        attempts: 1,
        backoff: Duration.zero,
      );

  MapTransport pages({int cut = 0}) => MapTransport({
    url('wall.ktx2'): TransportPage(body('the sharp one'), cut: cut),
    url('wall_low.ktx2'): TransportPage(body('the blurry one'), cut: cut),
  });

  test('draws the stand-in first, then the real thing', () async {
    // The big one is held back, because that is the only case a stand-in is
    // for: a wait long enough that something blurry beats nothing at all.
    final slow = _Staggered(
      quick: {'wall_low.ktx2': body('the blurry one')},
      held: {'wall.ktx2': body('the sharp one')},
    );
    final fetcher = AssetFetcher(
      origin: origin,
      transport: slow,
      attempts: 1,
      backoff: Duration.zero,
    );

    final stages = <AssetStage>[];
    final watching = fetcher
        .fetchInStages(
          id('wall.ktx2'),
          standIn: id('wall_low.ktx2'),
          standInAfter: Duration.zero,
        )
        .listen(stages.add);

    await pumpEventQueue();
    expect(stages.map((s) => utf8.decode(s.bytes)), [
      'the blurry one',
    ], reason: 'something to draw while the big one is still coming');
    expect(stages.single.isFinal, isFalse);
    expect(stages.single.id, id('wall_low.ktx2'));

    slow.release('wall.ktx2');
    await pumpEventQueue();
    await watching.cancel();

    expect(stages.map((s) => utf8.decode(s.bytes)), [
      'the blurry one',
      'the sharp one',
    ]);
    expect(stages.last.isFinal, isTrue);
    expect(stages.last.id, id('wall.ktx2'));
  });

  test('a load that finishes at once shows no stand-in', () async {
    // Both arrive in the same breath, as they do from a warm cache or a fast
    // link. Flashing a blurry texture for one frame on the way to a sharp one
    // is worse than never showing it.
    final fetcher = fetcherFor(pages());

    final stages = await fetcher
        .fetchInStages(
          id('wall.ktx2'),
          standIn: id('wall_low.ktx2'),
          standInAfter: Duration.zero,
        )
        .toList();

    expect(stages.map((s) => utf8.decode(s.bytes)), ['the sharp one']);
  });

  test('with no stand-in it is one stage', () async {
    final fetcher = fetcherFor(pages());

    final stages = await fetcher.fetchInStages(id('wall.ktx2')).toList();
    expect(stages, hasLength(1));
    expect(stages.single.isFinal, isTrue);
  });

  test('skips the stand-in when the real one is already there', () async {
    // The reason for the delay: a second launch reads from the cache in less
    // time than a frame, and a stand-in shown for one frame is a flicker,
    // not a help.
    final store = MemoryContentStore();
    final warm = fetcherFor(pages(), store: store);
    await warm.read(id('wall.ktx2'));
    await warm.read(id('wall_low.ktx2'));

    final transport = pages();
    final fetcher = fetcherFor(transport, store: store);
    final stages = await fetcher
        .fetchInStages(
          id('wall.ktx2'),
          standIn: id('wall_low.ktx2'),
          standInAfter: const Duration(milliseconds: 50),
        )
        .toList();

    expect(stages.map((s) => utf8.decode(s.bytes)), ['the sharp one']);
  });

  test('a stand-in that will not load is not a failure', () async {
    final fetcher = fetcherFor(
      MapTransport({url('wall.ktx2'): TransportPage(body('the sharp one'))}),
    );

    final stages = await fetcher
        .fetchInStages(
          id('wall.ktx2'),
          standIn: id('nothing.ktx2'),
          standInAfter: Duration.zero,
        )
        .toList();

    expect(stages.map((s) => utf8.decode(s.bytes)), ['the sharp one']);
  });

  test('the real one failing ends the stream with that failure', () async {
    final fetcher = fetcherFor(
      MapTransport({
        url('wall_low.ktx2'): TransportPage(body('the blurry one')),
      }),
    );

    await expectLater(
      fetcher
          .fetchInStages(
            id('wall.ktx2'),
            standIn: id('wall_low.ktx2'),
            standInAfter: Duration.zero,
          )
          .toList(),
      throwsA(isA<FetchFailed>().having((f) => f.cause, 'cause', 404)),
    );
  });

  test('walking away cancels both fetches', () async {
    final transport = pages();
    final fetcher = fetcherFor(transport);

    final watching = fetcher
        .fetchInStages(
          id('wall.ktx2'),
          standIn: id('wall_low.ktx2'),
          standInAfter: const Duration(seconds: 10),
        )
        .listen((_) {});
    await watching.cancel();
    await pumpEventQueue();

    // Nothing is left running, and nothing is left waiting to be drawn.
    expect(fetcher.running, 0);
    expect(fetcher.waiting, 0);
  });

  test('nothing is fetched until somebody is listening', () async {
    final transport = pages();
    final fetcher = fetcherFor(transport);

    fetcher.fetchInStages(id('wall.ktx2'), standIn: id('wall_low.ktx2'));
    await pumpEventQueue();

    expect(transport.sent, isEmpty);
  });

  test('progress is reported for the one being waited on', () async {
    final fetcher = fetcherFor(pages(cut: 4));

    final seen = <int>[];
    await fetcher
        .fetchInStages(
          id('wall.ktx2'),
          standIn: id('wall_low.ktx2'),
          standInAfter: Duration.zero,
          onProgress: (p) => seen.add(p.received),
        )
        .toList();

    expect(seen, isNotEmpty);
    expect(seen.last, body('the sharp one').length);
  });
}

/// A transport that answers some names at once and holds the rest back until
/// the test lets them go.
class _Staggered implements AssetTransport {
  _Staggered({required this.quick, required this.held});

  final Map<String, List<int>> quick;
  final Map<String, List<int>> held;
  final Map<String, StreamController<List<int>>> _open = {};

  @override
  Future<FetchReply> send(FetchRequest request) async {
    final name = request.url.pathSegments.last;
    final atOnce = quick[name];
    if (atOnce != null) {
      return FetchReply(
        status: 200,
        body: Stream.value(atOnce),
        length: atOnce.length,
      );
    }
    final waiting = held[name];
    if (waiting == null) return FetchReply.empty(404);
    final tap = _open[name] = StreamController<List<int>>();
    return FetchReply(status: 200, body: tap.stream, length: waiting.length);
  }

  void release(String name) {
    final tap = _open.remove(name)!;
    tap.add(held[name]!);
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

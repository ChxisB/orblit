@TestOn('vm')
library;

import 'dart:async';
import 'dart:io';
import 'dart:typed_data';

import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:orblit_asset/orblit_asset.dart';
import 'package:orblit_asset/src/pack/pack_format.dart';
import 'package:test/test.dart';

void main() {
  late Directory temp;
  var built = 0;

  setUp(() async {
    temp = await Directory.systemTemp.createTemp('orblit_range_');
  });

  tearDown(() => temp.delete(recursive: true));

  final url = Uri.parse('https://cdn.example.com/game.web.opack');

  /// Bytes for one asset, long enough that a blob is not the header.
  Uint8List content(String name, {int bytes = 300}) {
    final seed = name.codeUnits.fold<int>(0, (sum, unit) => sum * 31 + unit);
    return Uint8List.fromList([
      for (var i = 0; i < bytes; i++) (i * 7 + seed) & 0xff,
    ]);
  }

  /// A pack holding [files], each a path and its bytes.
  Future<Uint8List> pack(Map<String, Uint8List> files) async {
    final folder = [
      temp.path,
      'bundle_${built++}',
    ].join(Platform.pathSeparator);
    final entries = <AssetId, AssetEntry>{};
    for (final MapEntry(:key, :value) in files.entries) {
      final file = File(
        [folder, ...key.split('/')].join(Platform.pathSeparator),
      );
      await file.parent.create(recursive: true);
      await file.writeAsBytes(value);
      entries[AssetId.parse(key)] = AssetEntry(
        ContentHash.of(value),
        value.length,
      );
    }
    await File(
      [folder, 'manifest.json'].join(Platform.pathSeparator),
    ).writeAsString(AssetManifest(entries: entries).encode());
    final out = [temp.path, 'pack_$built.opack'].join(Platform.pathSeparator);
    await packBundle(
      bundlePath: folder,
      outPath: out,
      target: CookTargets.find('web')!,
    );
    return File(out).readAsBytes();
  }

  Map<String, Uint8List> game({String texture = 'a'}) => {
    'textures/wall.ktx2': content('wall$texture'),
    'models/robot.glb': content('robot', bytes: 700),
    'scenes/main.oscene': content('main', bytes: 120),
  };

  MapTransport serving(Uint8List bytes) =>
      MapTransport({url: TransportPage(bytes)});

  group('MapTransport', () {
    test('answers a bounded range with the bytes between, inclusive', () async {
      final transport = serving(
        Uint8List.fromList(List.generate(100, (i) => i)),
      );

      final reply = await transport.send(
        FetchRequest(url, from: 10, through: 19),
      );
      final body = [await for (final chunk in reply.body) ...chunk];

      expect(reply.status, 206);
      expect(reply.from, 10);
      expect(reply.length, 100);
      expect(body, List.generate(10, (i) => i + 10));
    });

    test('says 206 for a range that starts at nought', () async {
      final transport = serving(Uint8List(100));

      final reply = await transport.send(
        FetchRequest(url, from: 0, through: 31),
      );

      expect(reply.status, 206);
      expect(reply.from, 0);
    });

    test('cuts a range that runs past the end at the end', () async {
      final transport = serving(
        Uint8List.fromList(List.generate(40, (i) => i)),
      );

      final reply = await transport.send(
        FetchRequest(url, from: 30, through: 99),
      );
      final body = [await for (final chunk in reply.body) ...chunk];

      expect(body, List.generate(10, (i) => i + 30));
    });

    test('refuses a range that starts after the end', () async {
      final transport = serving(Uint8List(10));

      final reply = await transport.send(
        FetchRequest(url, from: 11, through: 12),
      );

      expect(reply.status, 416);
    });

    test('an end without a start is a mistake', () {
      expect(
        () => FetchRequest(url, through: 5),
        throwsA(isA<AssertionError>()),
      );
    });

    test('an end before the start is a mistake', () {
      expect(
        () => FetchRequest(url, from: 5, through: 4),
        throwsA(isA<AssertionError>()),
      );
    });
  });

  group('HttpTransport', () {
    test('sends both ends of a bounded range', () async {
      late http.BaseRequest seen;
      final transport = HttpTransport(
        client: MockClient.streaming((request, _) async {
          seen = request;
          return http.StreamedResponse(
            Stream.value([1, 2, 3]),
            206,
            headers: {'content-range': 'bytes 10-12/100'},
          );
        }),
      );

      final reply = await transport.send(
        FetchRequest(url, from: 10, through: 12),
      );

      expect(seen.headers['range'], 'bytes=10-12');
      expect(reply.status, 206);
      expect(reply.from, 10);
      expect(reply.length, 100);
    });

    test('leaves the end open for a resume', () async {
      late http.BaseRequest seen;
      final transport = HttpTransport(
        client: MockClient.streaming((request, _) async {
          seen = request;
          return http.StreamedResponse(
            const Stream.empty(),
            206,
            headers: {'content-range': 'bytes 900-999/1000'},
          );
        }),
      );

      await transport.send(FetchRequest(url, from: 900));

      expect(seen.headers['range'], 'bytes=900-');
    });

    test(
      'reports where the server started, not where it was asked to',
      () async {
        final transport = HttpTransport(
          client: MockClient.streaming(
            (request, _) async => http.StreamedResponse(
              const Stream.empty(),
              206,
              headers: {'content-range': 'bytes 0-31/500'},
            ),
          ),
        );

        final reply = await transport.send(
          FetchRequest(url, from: 64, through: 95),
        );

        expect(reply.from, 0);
      },
    );

    test('gives no length for a 206 that hides its content-range', () async {
      final transport = HttpTransport(
        client: MockClient.streaming(
          (request, _) async => http.StreamedResponse(
            Stream.value([1, 2, 3]),
            206,
            contentLength: 3,
          ),
        ),
      );

      final reply = await transport.send(
        FetchRequest(url, from: 0, through: 2),
      );

      expect(reply.length, isNull);
    });

    test('takes the content-length of a 200 for the length', () async {
      final transport = HttpTransport(
        client: MockClient.streaming(
          (request, _) async => http.StreamedResponse(
            Stream.value([1, 2, 3]),
            200,
            contentLength: 3,
          ),
        ),
      );

      final reply = await transport.send(FetchRequest(url));

      expect(reply.length, 3);
    });
  });

  group('RangePackBytes', () {
    test(
      'reads the header, the directory and three blobs and nothing else',
      () async {
        final bytes = await pack(game());
        final transport = serving(bytes);

        final opened = await AssetPack.open(RangePackBytes(transport, url));
        for (final path in game().keys) {
          await opened.read(AssetId.parse(path));
        }

        final header = PackHeader.read(bytes);
        expect(transport.sent, hasLength(5));
        expect(transport.sent.map((r) => r.from), everyElement(isNotNull));
        expect(transport.sent.map((r) => r.through), everyElement(isNotNull));
        expect(transport.sent[0].from, 0);
        expect(transport.sent[0].through, PackHeader.size - 1);
        expect(transport.sent[1].from, header.directoryAt);
        expect(
          transport.sent[1].through,
          header.directoryAt + header.directoryBytes - 1,
        );
        for (final blob in transport.sent.skip(2)) {
          expect(blob.from, greaterThanOrEqualTo(PackHeader.size));
          expect(blob.through!, lessThan(header.directoryAt));
        }
      },
    );

    test('reads the same bytes the pack holds', () async {
      final files = game();
      final opened = await AssetPack.open(
        RangePackBytes(serving(await pack(files)), url),
      );

      for (final MapEntry(:key, :value) in files.entries) {
        expect(await opened.read(AssetId.parse(key)), value);
      }
      expect(await opened.findCorrupt(), isEmpty);
    });

    test('learns its length from the first request and asks no more', () async {
      final bytes = await pack(game());
      final transport = serving(bytes);
      final ranged = RangePackBytes(transport, url);

      expect(await ranged.length(), bytes.length);
      expect(
        await ranged.read(0, PackHeader.size),
        bytes.sublist(0, PackHeader.size),
      );
      expect(await ranged.length(), bytes.length);

      expect(transport.sent, hasLength(1));
    });

    test('shares one first request between callers who ask together', () async {
      final transport = serving(await pack(game()));
      final ranged = RangePackBytes(transport, url);

      await Future.wait([ranged.length(), ranged.length(), ranged.read(0, 8)]);

      expect(transport.sent, hasLength(1));
    });

    test('asks again after the first request failed', () async {
      final bytes = await pack(game());
      final transport = MapTransport({
        url: TransportPage(bytes, faults: [const Fault.thrown()]),
      });
      final ranged = RangePackBytes(transport, url);

      await expectLater(ranged.length(), throwsA(isA<SocketFailure>()));
      expect(await ranged.length(), bytes.length);
    });

    test('refuses reading past the end', () async {
      final bytes = await pack(game());
      final ranged = RangePackBytes(serving(bytes), url);

      await expectLater(
        ranged.read(bytes.length - 4, 8),
        throwsA(isA<RangeError>()),
      );
    });

    test(
      'says so when the host ignores the range, and stops the download',
      () async {
        final bytes = await pack(game());
        final transport = _IgnoresRanges(bytes);
        final ranged = RangePackBytes(transport, url);

        await expectLater(
          ranged.length(),
          throwsA(
            isA<PackRangeFailed>().having(
              (e) => e.reason,
              'reason',
              contains('ignored the range'),
            ),
          ),
        );
        expect(transport.cancelled, isTrue);
      },
    );

    test('takes a whole-file range answered with 200', () async {
      final bytes = await pack(game());
      final ranged = RangePackBytes(_WholeAs200(serving(bytes)), url);

      expect(await ranged.read(0, bytes.length), bytes);
    });

    test('refuses an answer that starts somewhere else', () async {
      final bytes = await pack(game());
      final ranged = RangePackBytes(_Shifted(serving(bytes)), url);

      await expectLater(
        AssetPack.open(ranged),
        throwsA(isA<PackRangeFailed>()),
      );
    });

    test('refuses a pack that was replaced while it was being read', () async {
      final first = await pack(game());
      final second = await pack(
        game(texture: 'b')..['extra/new.bin'] = content('x'),
      );
      final pages = {url: TransportPage(first)};
      final transport = MapTransport(pages);
      final ranged = RangePackBytes(transport, url);
      final opened = await AssetPack.open(ranged);

      pages[url] = TransportPage(second);

      await expectLater(
        opened.read(AssetId.parse('models/robot.glb')),
        throwsA(
          isA<PackRangeFailed>().having(
            (e) => e.reason,
            'reason',
            contains('changed'),
          ),
        ),
      );
    });

    test('names the header a cross-origin host has to expose', () async {
      final bytes = await pack(game());
      final ranged = RangePackBytes(_HidesLength(serving(bytes)), url);

      await expectLater(
        ranged.length(),
        throwsA(
          isA<PackRangeFailed>().having(
            (e) => e.reason,
            'reason',
            contains('Access-Control-Expose-Headers'),
          ),
        ),
      );
    });

    test('says what a missing file did', () async {
      final ranged = RangePackBytes(MapTransport({}), url);

      await expectLater(
        ranged.length(),
        throwsA(
          isA<PackRangeFailed>().having(
            (e) => e.reason,
            'reason',
            contains('404'),
          ),
        ),
      );
    });
  });

  group('a pack read over the network with a store', () {
    final wall = AssetId.parse('textures/wall.ktx2');
    final robot = AssetId.parse('models/robot.glb');
    final main = AssetId.parse('scenes/main.oscene');

    Future<void> readAll(AssetPack opened) async {
      for (final id in [wall, robot, main]) {
        await opened.read(id);
      }
    }

    test('a second visit fetches the directory and no blob', () async {
      final bytes = await pack(game());
      final store = MemoryContentStore();
      await readAll(
        await AssetPack.open(RangePackBytes(serving(bytes), url), cache: store),
      );

      final second = serving(bytes);
      final opened = await AssetPack.open(
        RangePackBytes(second, url),
        cache: store,
      );
      await readAll(opened);

      expect(second.sent, hasLength(2));
      expect(await opened.read(robot), game()['models/robot.glb']);
    });

    test('a rebuilt pack fetches only the blob that changed', () async {
      final store = MemoryContentStore();
      await readAll(
        await AssetPack.open(
          RangePackBytes(serving(await pack(game())), url),
          cache: store,
        ),
      );

      final rebuiltBytes = await pack(game(texture: 'b'));
      final rebuilt = serving(rebuiltBytes);
      final opened = await AssetPack.open(
        RangePackBytes(rebuilt, url),
        cache: store,
      );
      await readAll(opened);

      final header = PackHeader.read(rebuiltBytes);
      expect(rebuilt.sent, hasLength(3));
      expect(rebuilt.sent.last.from, lessThan(header.directoryAt));
      expect(await opened.read(wall), game(texture: 'b')['textures/wall.ktx2']);
    });

    test('without a store every read goes to the server', () async {
      final transport = serving(await pack(game()));
      final opened = await AssetPack.open(RangePackBytes(transport, url));

      await opened.read(robot);
      await opened.read(robot);

      expect(transport.sent, hasLength(4));
    });

    test(
      'bytes that are not the blob are refused and not kept as it',
      () async {
        final bytes = await pack(game());
        final header = PackHeader.read(bytes);
        final plain = await AssetPack.open(MemoryPackBytes(bytes));
        final hash = plain.manifest.entries[robot]!.hash;
        // The first blob starts at the first aligned byte after the header.
        final damaged = Uint8List.fromList(bytes);
        for (var i = PackHeader.size; i < header.directoryAt; i++) {
          damaged[i] ^= 0xff;
        }
        final store = MemoryContentStore();
        final opened = await AssetPack.open(
          RangePackBytes(serving(damaged), url),
          cache: store,
        );

        await expectLater(opened.read(robot), throwsA(isA<FormatException>()));
        expect(await store.contains(hash), isFalse);
      },
    );
  });
}

/// A host that does not know about ranges and sends the file.
class _IgnoresRanges implements AssetTransport {
  _IgnoresRanges(this._bytes);

  final Uint8List _bytes;
  var cancelled = false;

  @override
  Future<FetchReply> send(FetchRequest request) async {
    late final StreamController<List<int>> controller;
    controller = StreamController<List<int>>(
      onListen: () => controller.add(_bytes),
      onCancel: () => cancelled = true,
    );
    return FetchReply(
      status: 200,
      body: controller.stream,
      length: _bytes.length,
    );
  }

  @override
  void close() {}
}

/// A host that answers a range that covers the whole file with a 200.
class _WholeAs200 implements AssetTransport {
  _WholeAs200(this._inner);

  final AssetTransport _inner;

  @override
  Future<FetchReply> send(FetchRequest request) async {
    final reply = await _inner.send(request);
    if (reply.status != 206 || reply.from != 0) return reply;
    if (reply.length != request.through! + 1) return reply;
    return FetchReply(status: 200, body: reply.body, length: reply.length);
  }

  @override
  void close() => _inner.close();
}

/// A host whose content-range a page on another origin cannot read.
class _HidesLength implements AssetTransport {
  _HidesLength(this._inner);

  final AssetTransport _inner;

  @override
  Future<FetchReply> send(FetchRequest request) async {
    final reply = await _inner.send(request);
    return FetchReply(status: reply.status, body: reply.body, from: reply.from);
  }

  @override
  void close() => _inner.close();
}

/// A host that answers from the start of the file whatever was asked for.
class _Shifted implements AssetTransport {
  _Shifted(this._inner);

  final AssetTransport _inner;

  @override
  Future<FetchReply> send(FetchRequest request) {
    final length = request.through! - request.from! + 1;
    return _inner.send(FetchRequest(request.url, from: 0, through: length - 1));
  }

  @override
  void close() => _inner.close();
}

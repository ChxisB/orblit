@TestOn('vm')
library;

import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'dart:math';
import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:test/test.dart';

import 'pack_fixture.dart';

/// Answers every request with [status], and keeps what was asked.
final class _Answers implements AssetTransport {
  _Answers(this.status);

  final int status;
  final List<FetchRequest> sent = [];

  @override
  Future<FetchReply> send(FetchRequest request) async {
    sent.add(request);
    return FetchReply.empty(status);
  }

  @override
  void close() {}
}

/// A server that sends the whole file whatever range it is asked for.
final class _IgnoresRanges implements AssetTransport {
  _IgnoresRanges(this._inner);

  final MapTransport _inner;

  @override
  Future<FetchReply> send(FetchRequest request) =>
      _inner.send(FetchRequest(request.url));

  @override
  void close() => _inner.close();
}

/// A server that answers a range from a byte a little before the one asked
/// for, and says so.
final class _ResumesEarly implements AssetTransport {
  _ResumesEarly(this._inner);

  static const by = 10;

  final MapTransport _inner;

  @override
  Future<FetchReply> send(FetchRequest request) {
    final from = request.from;
    if (from == null || from < by) return _inner.send(request);
    return _inner.send(FetchRequest(request.url, from: from - by));
  }

  @override
  void close() => _inner.close();
}

void main() {
  late PackFixture fixture;
  late Directory folderDirectory;
  late PackFolder folder;

  setUp(() async {
    fixture = await PackFixture.create();
    folderDirectory = Directory(fixture.at('downloads'));
    folder = PackFolder(folderDirectory.path);
  });

  tearDown(() => fixture.dispose());

  final url = Uri.parse('https://cdn.example.com/patches/one.opack');
  final origin = AssetOrigin(Uri.parse('https://cdn.example.com/assets/'));

  /// Long enough that half of it is well inside the blobs.
  String body(String name) => List.filled(400, name).join(' ');

  Map<String, String> state({String wall = 'wall v1'}) => {
    'textures/wall.ktx2': body(wall),
    'models/tree.glb': body('tree'),
    'scenes/town.oscene': body('town'),
  };

  Future<({BuiltPack shipped, BuiltPack patch})> pair() async {
    final shipped = await fixture.build('game', state());
    final patch = await fixture.build(
      'patch',
      state(wall: 'wall v2'),
      base: [shipped.pack],
    );
    return (shipped: shipped, patch: patch);
  }

  File packFile(ContentHash hash) =>
      File('${folderDirectory.path}${Platform.pathSeparator}${hash.hex}.opack');

  File partFile(ContentHash hash) =>
      File('${folderDirectory.path}${Platform.pathSeparator}${hash.hex}.part');

  Future<AssetPack> download(
    BuiltPack built,
    AssetTransport transport, {
    PackFolder? into,
    int attempts = 3,
    void Function(FetchProgress)? onProgress,
    AssetOrigin? from,
  }) => (into ?? folder).download(
    PackOffer(url, built.hash),
    transport: transport,
    origin: from ?? origin,
    attempts: attempts,
    backoff: Duration.zero,
    jitter: Random(1),
    onProgress: onProgress,
  );

  Future<String> text(AssetSource source, String id) async =>
      utf8.decode(await source.read(AssetId.parse(id)));

  group('download', () {
    test('files a whole pack under its hash', () async {
      final built = (await pair()).patch;
      final transport = MapTransport({url: TransportPage(built.bytes)});

      final pack = await download(built, transport);

      expect(pack.hash, built.hash);
      expect(await packFile(built.hash).readAsBytes(), built.bytes);
      expect(partFile(built.hash).existsSync(), isFalse);
      expect(transport.sent, hasLength(1));
      expect(transport.sent.single.from, isNull);
    });

    test('is cut off halfway, resumes, checks it and mounts it', () async {
      final both = await pair();
      final half = both.patch.bytes.length ~/ 2;
      final transport = MapTransport({
        url: TransportPage(both.patch.bytes, stopAfter: half, cut: 512),
      });

      final pack = await download(both.patch, transport);

      expect(transport.sent, hasLength(2));
      expect(transport.sent[1].from, half);
      expect(pack.hash, both.patch.hash);
      expect(partFile(both.patch.hash).existsSync(), isFalse);

      final packs = MountedPacks(target: CookTargets.find('android')!)
        ..start(both.shipped.pack)
        ..patch(pack);
      expect(await text(packs, 'textures/wall.ktx2'), body('wall v2'));
      expect(await text(packs, 'models/tree.glb'), body('tree'));
    });

    test('resumes from a part file an earlier run left', () async {
      final built = (await pair()).patch;
      await folderDirectory.create(recursive: true);
      final held = built.bytes.length ~/ 3;
      await partFile(built.hash).writeAsBytes(built.bytes.sublist(0, held));
      final transport = MapTransport({url: TransportPage(built.bytes)});

      final pack = await download(
        built,
        transport,
        into: PackFolder(folderDirectory.path),
      );

      expect(transport.sent.single.from, held);
      expect(pack.hash, built.hash);
      expect(await packFile(built.hash).readAsBytes(), built.bytes);
    });

    test(
      'takes a part that is already whole when the server says 416',
      () async {
        final built = (await pair()).patch;
        await folderDirectory.create(recursive: true);
        await partFile(built.hash).writeAsBytes(built.bytes);
        final transport = _Answers(416);

        final pack = await download(built, transport);

        expect(transport.sent.single.from, built.bytes.length);
        expect(pack.hash, built.hash);
        expect(partFile(built.hash).existsSync(), isFalse);
      },
    );

    test(
      'drops a part the server has no byte for when it is not a pack',
      () async {
        final built = (await pair()).patch;
        await folderDirectory.create(recursive: true);
        await partFile(
          built.hash,
        ).writeAsBytes(Uint8List(built.bytes.length + 50));
        final transport = _Answers(416);

        await expectLater(
          download(built, transport, attempts: 2),
          throwsA(isA<FetchFailed>()),
        );

        expect(partFile(built.hash).existsSync(), isFalse);
        expect(transport.sent.map((r) => r.from), [
          built.bytes.length + 50,
          null,
        ]);
      },
    );

    test('starts over when the server ignores the range', () async {
      final both = await pair();
      final half = both.patch.bytes.length ~/ 2;
      await folderDirectory.create(recursive: true);
      await partFile(
        both.patch.hash,
      ).writeAsBytes(both.patch.bytes.sublist(0, half));
      final transport = MapTransport({url: TransportPage(both.patch.bytes)});

      final pack = await download(both.patch, _IgnoresRanges(transport));

      expect(pack.hash, both.patch.hash);
      expect(await packFile(both.patch.hash).readAsBytes(), both.patch.bytes);
      expect(transport.sent, hasLength(1));
    });

    test('does not trust a server that resumes at another byte', () async {
      final both = await pair();
      final held = both.patch.bytes.length ~/ 2;
      await folderDirectory.create(recursive: true);
      await partFile(
        both.patch.hash,
      ).writeAsBytes(both.patch.bytes.sublist(0, held));
      final transport = MapTransport({url: TransportPage(both.patch.bytes)});

      final seen = <int>[];

      final pack = await download(
        both.patch,
        _ResumesEarly(transport),
        onProgress: (progress) => seen.add(progress.received),
      );

      expect(pack.hash, both.patch.hash);
      expect(await packFile(both.patch.hash).readAsBytes(), both.patch.bytes);
      expect(transport.sent.map((r) => r.from), [held - 10, null]);
      // The reply that began at the wrong byte is not written, so no
      // progress counts it. Written, it would put the file ten bytes past
      // its own length before the check caught it.
      expect(seen, [both.patch.bytes.length]);
    });

    test(
      'replaces a file that has a pack\'s name and other contents',
      () async {
        final both = await pair();
        await folderDirectory.create(recursive: true);
        await packFile(both.patch.hash).writeAsBytes(both.shipped.bytes);
        final transport = MapTransport({url: TransportPage(both.patch.bytes)});

        final pack = await download(both.patch, transport);

        expect(pack.hash, both.patch.hash);
        expect(await packFile(both.patch.hash).readAsBytes(), both.patch.bytes);
        expect(transport.sent, hasLength(1));
      },
    );

    test('throws away a part that belongs to a different file', () async {
      final both = await pair();
      final older = await fixture.build(
        'older',
        {
          'textures/wall.ktx2': body('wall v0'),
          'models/tree.glb': body('tree'),
          'scenes/town.oscene': body('town'),
        },
        base: [both.shipped.pack],
      );
      await folderDirectory.create(recursive: true);
      await partFile(both.patch.hash).writeAsBytes(older.bytes.sublist(0, 60));
      final transport = MapTransport({url: TransportPage(both.patch.bytes)});

      final pack = await download(both.patch, transport);

      expect(pack.hash, both.patch.hash);
      expect(transport.sent.map((r) => r.from), [60, null]);
      expect(await packFile(both.patch.hash).readAsBytes(), both.patch.bytes);
    });

    test('refuses a pack that is not the one offered, whole', () async {
      final both = await pair();
      final transport = MapTransport({url: TransportPage(both.shipped.bytes)});

      await expectLater(
        download(both.patch, transport),
        throwsA(
          isA<FetchCorrupt>()
              .having((e) => e.wanted, 'wanted', both.patch.hash)
              .having((e) => e.got, 'got', both.shipped.hash),
        ),
      );

      expect(transport.sent, hasLength(1));
      expect(folderDirectory.listSync(), isEmpty);
    });

    test('refuses a pack with a damaged blob and keeps nothing', () async {
      final built = (await pair()).patch;
      final damaged = Uint8List.fromList(built.bytes);
      final marker = utf8.encode(body('wall v2'));
      final at = _indexOf(damaged, marker);
      damaged[at + 10] ^= 0xff;
      final transport = MapTransport({url: TransportPage(damaged)});

      await expectLater(
        download(built, transport),
        throwsA(
          isA<FormatException>().having(
            (e) => e.message,
            'message',
            contains('textures/wall.ktx2'),
          ),
        ),
      );

      expect(folderDirectory.listSync(), isEmpty);
      expect(transport.sent, hasLength(1));
    });

    test('does not retry a 404', () async {
      final built = (await pair()).patch;
      final transport = MapTransport({});

      await expectLater(
        download(built, transport),
        throwsA(isA<FetchFailed>().having((e) => e.cause, 'cause', 404)),
      );

      expect(transport.sent, hasLength(1));
    });

    test('tries a busy server again and then succeeds', () async {
      final built = (await pair()).patch;
      final transport = MapTransport({
        url: TransportPage(
          built.bytes,
          faults: [const Fault.status(503), const Fault.thrown()],
        ),
      });

      final pack = await download(built, transport);

      expect(pack.hash, built.hash);
      expect(transport.sent, hasLength(3));
    });

    test('gives up after the attempts it was given', () async {
      final built = (await pair()).patch;
      final transport = _Answers(503);

      await expectLater(
        download(built, transport, attempts: 3),
        throwsA(
          isA<FetchFailed>()
              .having((e) => e.attempts, 'attempts', 3)
              .having((e) => e.cause, 'cause', 503),
        ),
      );

      expect(transport.sent, hasLength(3));
    });

    test(
      'refuses a host the origin does not allow before any request',
      () async {
        final built = (await pair()).patch;
        final transport = MapTransport({});
        final elsewhere = Uri.parse('https://evil.example.net/one.opack');

        await expectLater(
          folder.download(
            PackOffer(elsewhere, built.hash),
            transport: transport,
            origin: origin,
          ),
          throwsA(isA<FetchRefused>()),
        );

        expect(transport.sent, isEmpty);
      },
    );

    test('refuses a pack longer than the policy allows', () async {
      final built = (await pair()).patch;
      final transport = MapTransport({url: TransportPage(built.bytes)});
      final small = AssetOrigin(
        Uri.parse('https://cdn.example.com/assets/'),
        policy: const FetchPolicy(maxBytes: 1000),
      );

      await expectLater(
        download(built, transport, from: small),
        throwsA(isA<FetchRefused>()),
      );

      expect(folderDirectory.listSync(), isEmpty);
    });

    test('stops reading a body that runs past the policy', () async {
      final built = (await pair()).patch;
      final transport = _NeverEnds();
      final small = AssetOrigin(
        Uri.parse('https://cdn.example.com/assets/'),
        policy: const FetchPolicy(maxBytes: 1000),
      );

      await expectLater(
        download(built, transport, from: small),
        throwsA(isA<FetchRefused>()),
      );

      expect(folderDirectory.listSync(), isEmpty);
      expect(transport.pulled, lessThan(10));
    });

    test('drops what a server sent past the length it declared', () async {
      final built = (await pair()).patch;
      final transport = _Lies(built.bytes);

      await expectLater(
        download(built, transport),
        throwsA(isA<FetchFailed>()),
      );

      expect(transport.sent.map((r) => r.from), [null, null, null]);
      expect(folderDirectory.listSync(), isEmpty);
    });

    test(
      'answers from the folder without a request when it holds the pack',
      () async {
        final built = (await pair()).patch;
        final transport = MapTransport({url: TransportPage(built.bytes)});
        await download(built, transport);

        final again = await download(built, transport);

        expect(again.hash, built.hash);
        expect(transport.sent, hasLength(1));
      },
    );

    test('shares one download between two callers', () async {
      final built = (await pair()).patch;
      final transport = MapTransport({url: TransportPage(built.bytes)});

      final both = await Future.wait([
        download(built, transport),
        download(built, transport),
      ]);

      expect(both.map((p) => p.hash), [built.hash, built.hash]);
      expect(transport.sent, hasLength(1));
    });

    test('reports how much has arrived of how much', () async {
      final built = (await pair()).patch;
      final transport = MapTransport({
        url: TransportPage(built.bytes, cut: 1024),
      });
      final seen = <FetchProgress>[];

      await download(built, transport, onProgress: seen.add);

      expect(seen.length, greaterThan(2));
      expect(seen.last.received, built.bytes.length);
      expect(seen.last.total, built.bytes.length);
      expect([
        for (final p in seen) p.received,
      ], orderedEquals([for (final p in seen) p.received]..sort()));
    });
  });

  group('list', () {
    test('is empty when the folder is not there', () async {
      final listing = await folder.list();

      expect(listing.packs, isEmpty);
      expect(listing.unreadable, isEmpty);
      expect(listing.unfinished, isEmpty);
    });

    test('opens packs, and reports what is not one without stopping', () async {
      final both = await pair();
      await folderDirectory.create(recursive: true);
      await packFile(both.shipped.hash).writeAsBytes(both.shipped.bytes);
      await packFile(both.patch.hash).writeAsBytes(both.patch.bytes);
      // A pack under a name that is not its hash.
      final renamed = File(
        '${folderDirectory.path}${Platform.pathSeparator}${'a' * 64}.opack',
      );
      await renamed.writeAsBytes(both.patch.bytes);
      final junk = File(
        '${folderDirectory.path}${Platform.pathSeparator}${'b' * 64}.opack',
      );
      await junk.writeAsBytes(List.filled(100, 7));
      final cut = File(
        '${folderDirectory.path}${Platform.pathSeparator}${'c' * 64}.opack',
      );
      await cut.writeAsBytes(both.patch.bytes.sublist(0, 200));
      final unfinishedHash = ContentHash.of(utf8.encode('unfinished'));
      await partFile(unfinishedHash).writeAsBytes([1, 2, 3]);
      await File(
        '${folderDirectory.path}${Platform.pathSeparator}notes.txt',
      ).writeAsString('hello');

      final listing = await folder.list();

      expect(listing.packs.map((p) => p.hash).toSet(), {
        both.shipped.hash,
        both.patch.hash,
      });
      expect(listing.unreadable.map((u) => u.path).toSet(), {
        renamed.path,
        junk.path,
        cut.path,
      });
      expect(
        listing.unreadable.firstWhere((u) => u.path == renamed.path).reason,
        contains(both.patch.hash.hex),
      );
      expect(listing.unfinished, [unfinishedHash]);
    });

    test('lists in the order of the hashes', () async {
      final both = await pair();
      final more = [
        for (final name in ['a', 'b', 'c', 'd'])
          await fixture.build(name, state(wall: 'mod $name')),
      ];
      await folderDirectory.create(recursive: true);
      for (final built in [both.patch, ...more, both.shipped]) {
        await packFile(built.hash).writeAsBytes(built.bytes);
      }

      final listing = await folder.list();

      final hashes = [for (final p in listing.packs) p.hash.hex];
      expect(hashes, hasLength(6));
      expect(hashes, [...hashes]..sort());
    });
  });

  group('start-up', () {
    test('mounts what is on disk in chain order and skips an orphan', () async {
      final shipped = await fixture.build('game', state());
      final one = await fixture.build(
        'one',
        state(wall: 'wall v2'),
        base: [shipped.pack],
      );
      final two = await fixture.build(
        'two',
        {...state(wall: 'wall v2'), 'models/tree.glb': body('tree v2')},
        base: [shipped.pack, one.pack],
      );
      final three = await fixture.build(
        'three',
        {...state(wall: 'wall v3'), 'models/tree.glb': body('tree v2')},
        base: [shipped.pack, one.pack, two.pack],
      );
      // Built on a pack that was never downloaded.
      final lost = await fixture.build(
        'lost',
        {...state(wall: 'wall v9'), 'models/tree.glb': body('tree v9')},
        base: [shipped.pack, one.pack, two.pack, three.pack],
      );
      final orphan = await fixture.build(
        'orphan',
        {...state(wall: 'wall v10'), 'models/tree.glb': body('tree v10')},
        base: [shipped.pack, one.pack, two.pack, three.pack, lost.pack],
      );
      await folderDirectory.create(recursive: true);
      for (final built in [three, orphan, one, two]) {
        await packFile(built.hash).writeAsBytes(built.bytes);
      }

      final listing = await folder.list();
      final chain = PatchChain.of(shipped.pack, listing.packs);
      final packs = MountedPacks(target: CookTargets.find('android')!)
        ..start(shipped.pack);
      chain.ordered.forEach(packs.patch);

      expect(chain.ordered.map((p) => p.hash), [
        one.hash,
        two.hash,
        three.hash,
      ]);
      expect(chain.skipped.map((s) => s.pack.hash), [orphan.hash]);
      expect(await text(packs, 'textures/wall.ktx2'), body('wall v3'));
      expect(await text(packs, 'models/tree.glb'), body('tree v2'));
    });
  });

  group('remove', () {
    test('deletes a pack and its part, and says so', () async {
      final both = await pair();
      await folderDirectory.create(recursive: true);
      await packFile(both.patch.hash).writeAsBytes(both.patch.bytes);
      await partFile(both.patch.hash).writeAsBytes([1]);

      expect(await folder.remove(both.patch.hash), isTrue);

      expect(folderDirectory.listSync(), isEmpty);
      expect(await folder.remove(both.patch.hash), isFalse);
    });
  });
}

/// A server that answers 200 with no length and a body far longer than any
/// pack, counting the chunks it is asked for. The size can only be found out
/// as the body arrives, and only a reader that stops early pulls few.
final class _NeverEnds implements AssetTransport {
  var pulled = 0;

  @override
  Future<FetchReply> send(FetchRequest request) async =>
      FetchReply(status: 200, body: _chunks());

  Stream<List<int>> _chunks() async* {
    for (var i = 0; i < 5000; i++) {
      pulled++;
      yield Uint8List(400);
    }
  }

  @override
  void close() {}
}

/// A server whose body is longer than the length it declares.
final class _Lies implements AssetTransport {
  _Lies(this._bytes);

  final Uint8List _bytes;
  final List<FetchRequest> sent = [];

  @override
  Future<FetchReply> send(FetchRequest request) async {
    sent.add(request);
    return FetchReply(
      status: 200,
      body: Stream.value(_bytes),
      length: _bytes.length - 100,
    );
  }

  @override
  void close() {}
}

int _indexOf(Uint8List haystack, List<int> needle) {
  for (var at = 0; at <= haystack.length - needle.length; at++) {
    var match = true;
    for (var i = 0; i < needle.length && match; i++) {
      match = haystack[at + i] == needle[i];
    }
    if (match) return at;
  }
  throw StateError('The marker is not in the pack.');
}

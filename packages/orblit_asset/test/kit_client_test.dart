import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:orblit_asset/src/pack/pack_format.dart' show PackHeader;
import 'package:test/test.dart';

final base = Uri.parse('https://store.example/store/');
const png = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 1, 2, 3];

late Directory scratch;
late Map<Uri, TransportPage> pages;
late KitStoreClient client;

String at(String name) => '${scratch.path}${Platform.pathSeparator}$name';

/// Packs a kit, lists it in an entry, builds a store from it and serves the
/// pack and the store's files over the fake network.
Future<void> publish() async {
  final folder = Directory(at('src'))..createSync();
  File('${folder.path}/models/crate.obj')
    ..parent.createSync(recursive: true)
    ..writeAsBytesSync(ascii.encode('crate'));
  File('${folder.path}/shots/a.png')
    ..parent.createSync(recursive: true)
    ..writeAsBytesSync(png);
  File('${folder.path}/kit.json').writeAsStringSync(
    jsonEncode({
      'slug': 'crates',
      'publisher': 'brass-lamp',
      'name': 'Crates',
      'summary': 'Some crates.',
      'version': '1.0.0',
      'licence': {'spdx': 'MIT', 'url': 'https://example.com/l'},
      'engine': {'from': '0.30.0', 'to': '0.40.0'},
      'tags': ['props'],
      'previews': ['shots/a.png'],
    }),
  );
  final summary = await packKit(
    folder: folder.path,
    outPath: at('crates.opack'),
  );
  final pack = File(at('crates.opack')).readAsBytesSync();
  final packUrl = Uri.parse('https://example.com/crates.opack');
  pages[packUrl] = TransportPage(pack);
  File(at('entries/brass-lamp/crates.json'))
    ..parent.createSync(recursive: true)
    ..writeAsStringSync(
      jsonEncode({
        'releases': [
          {
            'version': '1.0.0',
            'url': '$packUrl',
            'hash': summary.hash.hex,
            'bytes': pack.length,
            'released': '2026-10-19',
            'notes': 'First.',
          },
        ],
      }),
    );
  await buildKitStore(
    entriesPath: at('entries'),
    outPath: at('out'),
    transport: MapTransport(pages),
  );
  final out = Directory(at('out'));
  for (final item in out.listSync(recursive: true).whereType<File>()) {
    final name = item.path.substring(out.path.length + 1);
    pages[base.resolve(name)] = TransportPage(item.readAsBytesSync());
  }
}

/// A network whose replies do not say how long they are.
final class _NoLength implements AssetTransport {
  _NoLength(this._inner);

  final AssetTransport _inner;

  @override
  Future<FetchReply> send(FetchRequest request) async {
    final reply = await _inner.send(request);
    return FetchReply(status: reply.status, body: reply.body);
  }

  @override
  void close() => _inner.close();
}

/// A network that never answers.
final class _Silent implements AssetTransport {
  @override
  Future<FetchReply> send(FetchRequest request) =>
      Completer<FetchReply>().future;

  @override
  void close() {}
}

void main() {
  setUp(() async {
    scratch = Directory.systemTemp.createTempSync('orblit_kit_client_');
    pages = {};
    await publish();
    client = KitStoreClient(base, MapTransport(pages));
  });
  tearDown(() => scratch.deleteSync(recursive: true));

  test('reads the index, the releases, a preview and the file list', () async {
    final row = (await client.index()).rows.single;
    expect(row.manifest.name, 'Crates');
    final releases = await client.releases(row);
    expect(releases.previews, ['preview-0.png']);
    expect(await client.preview(row, 'preview-0.png'), png);
    expect(await client.thumbnail(row), png);
    final kit = await client.open(releases.releases.single.release);
    expect(kit.files.keys.map((id) => '$id'), [
      'models/crate.obj',
      'shots/a.png',
    ]);
  });

  test('takes a base with no trailing slash as a folder', () async {
    final other = KitStoreClient(
      Uri.parse('https://store.example/store'),
      MapTransport(pages),
    );
    expect((await other.index()).rows, hasLength(1));
  });

  test('refuses a base that is not a plain https address', () {
    for (final text in [
      'http://store.example/store/',
      'https://store.example/store/?a=1',
      'https://store.example/store/#a',
      'https://me@store.example/store/',
      '/store/',
    ]) {
      expect(
        () => KitStoreClient(Uri.parse(text), MapTransport(pages)),
        throwsArgumentError,
        reason: text,
      );
      expect(kitStoreAddressProblem(Uri.parse(text)), isNotNull, reason: text);
    }
  });

  test('an address with nothing wrong with it has no problem', () {
    expect(kitStoreAddressProblem(base), isNull);
  });

  test('refuses a status other than 200', () async {
    pages.remove(base.resolve('index.json'));
    await expectLater(client.index(), throwsA(isA<KitStoreFailed>()));
  });

  test('refuses a body over the limit, by header and by count', () async {
    final row = (await client.index()).rows.single;
    pages[base.resolve('kits/brass-lamp/crates/preview-0.png')] = TransportPage(
      Uint8List(2 * 1024 * 1024),
      cut: 4096,
    );
    final unsaid = KitStoreClient(base, _NoLength(MapTransport(pages)));
    for (final reader in [client, unsaid]) {
      await expectLater(
        reader.preview(row, 'preview-0.png'),
        throwsA(isA<KitStoreFailed>()),
      );
    }
  });

  test('refuses a preview name that is not a preview', () async {
    final row = (await client.index()).rows.single;
    for (final name in ['../index.json', 'releases.json', 'preview-9.png']) {
      await expectLater(
        client.preview(row, name),
        throwsArgumentError,
        reason: name,
      );
    }
  });

  test('a store with a newer format is refused', () async {
    final text = utf8.decode(pages[base.resolve('index.json')]!.bytes);
    pages[base.resolve('index.json')] = TransportPage(
      utf8.encode(text.replaceFirst('"formatVersion":1', '"formatVersion":9')),
    );
    await expectLater(client.index(), throwsFormatException);
  });

  test('gives up on a store that does not answer', () async {
    final silent = KitStoreClient(
      base,
      _Silent(),
      timeout: const Duration(milliseconds: 20),
    );
    await expectLater(silent.index(), throwsA(isA<KitStoreFailed>()));
  });

  test('refuses a file list that asks to read too much at once', () async {
    final row = (await client.index()).rows.single;
    final release = (await client.releases(row)).releases.single.release;
    // A header that says the directory is 5 MiB, in a file that is long
    // enough to hold it.
    final bytes = Uint8List(6 * 1024 * 1024)
      ..setRange(
        0,
        PackHeader.size,
        const PackHeader(
          directoryAt: 32,
          directoryBytes: 5 * 1024 * 1024,
        ).encode(),
      );
    final url = Uri.parse(release.url);
    pages[url] = TransportPage(bytes);
    await expectLater(
      client.open(release),
      throwsA(
        isA<FormatException>().having(
          (error) => error.message,
          'message',
          contains('the most is'),
        ),
      ),
    );
  });
}

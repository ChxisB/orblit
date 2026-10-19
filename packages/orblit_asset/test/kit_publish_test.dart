import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:test/test.dart';

const String url = 'https://example.com/kits/crate-pack-1.0.0.opack';

late Directory scratch;

String at(String name) => '${scratch.path}${Platform.pathSeparator}$name';

void write(String path, String text) {
  File(path)
    ..parent.createSync(recursive: true)
    ..writeAsStringSync(text);
}

/// A folder holding a manifest at [version] and `models/crate.obj`.
String source({String version = '1.0.0'}) {
  final folder = at('crates');
  write(
    '$folder/kit.json',
    jsonEncode({
      'slug': 'crate-pack',
      'publisher': 'brass-lamp',
      'name': 'Crate pack',
      'summary': 'Twelve crates.',
      'version': version,
      'licence': {'spdx': 'MIT', 'url': 'https://example.com/l'},
      'engine': {'from': '0.30.0', 'to': '0.40.0'},
      'tags': <String>[],
      'previews': <String>[],
    }),
  );
  write('$folder/models/crate.obj', 'v 0 0 0');
  return folder;
}

final DateTime day = DateTime.utc(2026, 10, 19, 23, 59);

Future<KitPublished> publish({
  String version = '1.0.0',
  String address = url,
  String notes = '',
  bool listed = true,
}) => publishKit(
  folder: source(version: version),
  url: address,
  outPath: at('crate-pack-$version.opack'),
  released: day,
  notes: notes,
  entriesPath: listed ? at('kits') : null,
);

String entryFile() => at('kits/brass-lamp/crate-pack.json');

void main() {
  setUp(() {
    scratch = Directory.systemTemp.createTempSync('orblit_kit_publish_');
    Directory(at('kits')).createSync();
  });
  tearDown(() => scratch.deleteSync(recursive: true));

  test('packs the folder and fills the release in from the file', () async {
    final made = await publish(notes: 'First release.', listed: false);
    final file = File(at('crate-pack-1.0.0.opack'));
    final release = made.release;
    expect(release.version, '1.0.0');
    expect(release.url, url);
    expect(release.hash, made.summary.hash);
    expect(release.bytes, file.lengthSync());
    expect(release.released, '2026-10-19');
    expect(release.notes, 'First release.');
    expect(made.entry.publisher, 'brass-lamp');
    expect(made.entry.slug, 'crate-pack');
    expect(made.entry.releases, hasLength(1));
    expect(made.entryPath, isNull);
    expect(Directory(at('kits')).listSync(), isEmpty);
  });

  test('writes an entry the index builder accepts', () async {
    final made = await publish();
    expect(made.entryPath, entryFile());
    final pages = {
      Uri.parse(url): TransportPage(
        Uint8List.fromList(
          File(at('crate-pack-1.0.0.opack')).readAsBytesSync(),
        ),
      ),
    };
    final built = await buildKitStore(
      entriesPath: at('kits'),
      outPath: at('site'),
      transport: MapTransport(pages),
    );
    expect(built, (kits: 1, releases: 1));
  });

  test('adds a release to the entry that is there', () async {
    await publish();
    await publish(
      version: '1.1.0',
      address: 'https://example.com/kits/crate-pack-1.1.0.opack',
    );
    final read = KitEntry.parse(
      File(entryFile()).readAsBytesSync(),
      publisher: 'brass-lamp',
      slug: 'crate-pack',
    );
    expect(read.releases.map((r) => r.version), ['1.1.0', '1.0.0']);
  });

  test('refuses a version that is listed and keeps the file', () async {
    await publish();
    final before = File(entryFile()).readAsStringSync();
    await expectLater(
      publish(address: 'https://example.com/other.opack'),
      throwsA(
        isA<FormatException>().having(
          (error) => error.message,
          'message',
          allOf(contains('1.0.0'), contains('kit.json')),
        ),
      ),
    );
    expect(File(entryFile()).readAsStringSync(), before);
  });

  test('a refusal leaves the kit file as it was and none beside it', () async {
    await publish();
    final file = File(at('crate-pack-1.0.0.opack'));
    final before = file.readAsBytesSync();
    write('${source()}/models/more.obj', 'v 1 1 1');
    await expectLater(publish(), throwsA(isA<FormatException>()));
    expect(file.readAsBytesSync(), before);
    expect(
      scratch.listSync().map((entity) => entity.path),
      isNot(contains(endsWith('.staged'))),
    );
  });

  test('refuses a kit file inside the folder and keeps its files', () async {
    final folder = source();
    write('$folder/README.md', 'mine');
    await expectLater(
      publishKit(
        folder: folder,
        url: 'https://example.com/README.md',
        outPath: '$folder/README.md',
        released: day,
      ),
      throwsA(
        isA<FormatException>().having(
          (error) => error.message,
          'message',
          contains('inside'),
        ),
      ),
    );
    expect(File('$folder/README.md').readAsStringSync(), 'mine');
  });

  test('refuses a url that CI would not fetch and writes no entry', () async {
    await expectLater(
      publish(address: 'http://example.com/a.opack'),
      throwsA(isA<FormatException>()),
    );
    expect(File(entryFile()).existsSync(), isFalse);
    expect(File(at('crate-pack-1.0.0.opack')).existsSync(), isFalse);
  });

  test('refuses a long note and a folder that breaks a rule', () async {
    await expectLater(
      publish(notes: 'x' * 400),
      throwsA(isA<FormatException>()),
    );
    write('${source()}/.DS_Store', 'x');
    await expectLater(publish(), throwsA(isA<FormatException>()));
    expect(File(entryFile()).existsSync(), isFalse);
  });

  test('an entries folder that is not there is an argument error', () async {
    await expectLater(
      publishKit(
        folder: source(),
        url: url,
        outPath: at('crate-pack-1.0.0.opack'),
        released: day,
        entriesPath: at('nowhere'),
      ),
      throwsArgumentError,
    );
  });

  test('an entry that is there and bad is named, and not replaced', () async {
    write(entryFile(), '{"releases": [], "extra": 1}');
    await expectLater(
      publish(),
      throwsA(
        isA<FormatException>().having(
          (error) => error.message,
          'message',
          contains('brass-lamp/crate-pack'),
        ),
      ),
    );
    expect(
      File(entryFile()).readAsStringSync(),
      '{"releases": [], "extra": 1}',
    );
  });
}

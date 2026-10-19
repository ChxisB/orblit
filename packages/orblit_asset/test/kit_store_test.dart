import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:orblit_asset/src/kit/kit_manifest.dart';
import 'package:orblit_asset/src/pack/pack_bundle_io.dart';
import 'package:test/test.dart';

typedef Served = ({String url, ContentHash hash, int bytes});

late Directory scratch;
late Map<Uri, TransportPage> pages;
var made = 0;

String at(String name) => '${scratch.path}${Platform.pathSeparator}$name';

const png = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 1, 2, 3];
const jpg = [0xff, 0xd8, 0xff, 0xe0, 1, 2, 3];
final webp = [...ascii.encode('RIFF'), 1, 0, 0, 0, ...ascii.encode('WEBP')];

Map<String, Object?> manifestOf(
  String publisher,
  String slug,
  String version,
  List<String> previews,
) => {
  'slug': slug,
  'publisher': publisher,
  'name': 'Kit $slug',
  'summary': 'Some files.',
  'version': version,
  'licence': {'spdx': 'MIT', 'url': 'https://example.com/l'},
  'engine': {'from': '0.30.0', 'to': '0.40.0'},
  'tags': ['props'],
  'previews': previews,
};

Served serve(String name, Uint8List bytes, ContentHash hash) {
  final url = 'https://example.com/$name.opack';
  pages[Uri.parse(url)] = TransportPage(bytes);
  return (url: url, hash: hash, bytes: bytes.length);
}

/// A kit packed from a folder and served over the fake network.
Future<Served> kitFile(
  String publisher,
  String slug, {
  String version = '1.0.0',
  Map<String, List<int>> files = const {
    'models/crate.obj': [118, 32, 48],
  },
  List<String> previews = const [],
}) async {
  final folder = Directory(at('src_${made++}'))..createSync();
  for (final MapEntry(:key, :value) in files.entries) {
    File('${folder.path}/$key')
      ..parent.createSync(recursive: true)
      ..writeAsBytesSync(value);
  }
  File('${folder.path}/kit.json').writeAsStringSync(
    jsonEncode(manifestOf(publisher, slug, version, previews)),
  );
  final out = at('pack_${made++}.opack');
  final summary = await packKit(folder: folder.path, outPath: out);
  return serve(
    '$publisher-$slug-$version',
    File(out).readAsBytesSync(),
    summary.hash,
  );
}

/// A pack that [packKit] would refuse to write, built by hand. [manifest]
/// replaces the manifest, which is how a kit with no licence is made.
Future<Served> brokenKit(
  String name, {
  Map<String, Object?>? manifest,
  Map<String, List<int>> files = const {
    'a.txt': [1],
  },
}) async {
  final bytes = Uint8List.fromList(
    utf8.encode(
      jsonEncode(
        manifest ??
            {
              ...manifestOf('amber', 'rocks', '1.0.0', const []),
              'kind': kitKind,
              'formatVersion': 1,
              'containsScripts': false,
            },
      ),
    ),
  );
  final contents = {
    for (final MapEntry(:key, :value) in files.entries)
      AssetId.parse(key): Uint8List.fromList(value),
  };
  final out = at('hand_${made++}.opack');
  final summary = await writePack(
    manifest: AssetManifest(
      entries: {
        kitManifestId: AssetEntry(ContentHash.of(bytes), bytes.length),
        for (final MapEntry(:key, :value) in contents.entries)
          key: AssetEntry(ContentHash.of(value), value.length),
      },
    ),
    outPath: out,
    target: kitTarget,
    read: (id) async => contents[id] ?? bytes,
  );
  return serve(name, File(out).readAsBytesSync(), summary.hash);
}

Map<String, Object?> release(
  Served served, {
  String version = '1.0.0',
  Map<String, Object?> changes = const {},
}) => {
  'version': version,
  'url': served.url,
  'hash': served.hash.hex,
  'bytes': served.bytes,
  'released': '2026-10-19',
  'notes': 'Changes $version.',
  ...changes,
};

void entry(
  String publisher,
  String slug,
  List<Map<String, Object?>> releases,
) => write('entries/$publisher/$slug.json', jsonEncode({'releases': releases}));

void write(String name, String text) => File(at(name))
  ..parent.createSync(recursive: true)
  ..writeAsStringSync(text);

Future<({int kits, int releases})> build() => buildKitStore(
  entriesPath: at('entries'),
  outPath: at('out'),
  transport: MapTransport(pages),
);

/// Builds and expects a refusal that holds [fragment] and leaves no folder.
Future<void> fails(String fragment) async {
  await expectLater(
    build(),
    throwsA(
      isA<FormatException>().having(
        (error) => error.message,
        'message',
        contains(fragment),
      ),
    ),
  );
  expect(Directory(at('out')).existsSync(), isFalse);
}

Uint8List read(String name) => File(at(name)).readAsBytesSync();

void main() {
  setUp(() {
    scratch = Directory.systemTemp.createTempSync('orblit_kit_store_');
    Directory(at('entries')).createSync();
    pages = {};
  });
  tearDown(() => scratch.deleteSync(recursive: true));

  Future<void> sample() async {
    final crates1 = await kitFile(
      'brass-lamp',
      'crates',
      files: {
        'models/crate.obj': ascii.encode('crate one'),
        'shots/a.png': png,
      },
      previews: ['shots/a.png'],
    );
    final crates2 = await kitFile(
      'brass-lamp',
      'crates',
      version: '1.1.0',
      files: {
        'models/crate.obj': ascii.encode('crate two'),
        'shots/a.png': png,
      },
      previews: ['shots/a.png'],
    );
    final lamps = await kitFile(
      'brass-lamp',
      'lamps',
      version: '0.2.0',
      files: {'scripts/flicker.js': ascii.encode('1')},
    );
    final rocks = await kitFile(
      'amber',
      'rocks',
      version: '3.0.0',
      files: {
        'a.jpg': jpg,
        'b.webp': webp,
        'rock.obj': const [1],
      },
      previews: ['a.jpg', 'b.webp'],
    );
    entry('brass-lamp', 'crates', [
      release(crates2, version: '1.1.0'),
      release(crates1),
    ]);
    entry('brass-lamp', 'lamps', [release(lamps, version: '0.2.0')]);
    entry('amber', 'rocks', [release(rocks, version: '3.0.0')]);
  }

  test('builds three kits into files a browser reads from a folder', () async {
    await sample();
    expect(await build(), (kits: 3, releases: 4));

    // Read back from a plain directory, as the editor does with no network.
    final index = KitIndex.parse(read('out/index.json'));
    expect(
      [for (final row in index.rows) row.manifest.slug],
      ['rocks', 'crates', 'lamps'],
    );
    final crates = index.rows[1];
    expect(crates.manifest.version, '1.1.0');
    expect(crates.manifest.name, 'Kit crates');
    expect(crates.updated, '2026-10-19');
    expect(crates.thumbnail, 'kits/brass-lamp/crates/preview-0.png');
    expect(index.rows[2].manifest.containsScripts, isTrue);
    expect(index.rows[2].thumbnail, isNull);

    final list = KitReleases.parse(
      read('out/kits/brass-lamp/crates/releases.json'),
    );
    expect(
      [for (final r in list.releases) r.release.version],
      ['1.1.0', '1.0.0'],
    );
    expect(list.releases.first.release.notes, 'Changes 1.1.0.');
    expect(list.previews, ['preview-0.png']);
    expect(read('out/kits/brass-lamp/crates/preview-0.png'), png);

    final rocks = KitReleases.parse(read('out/kits/amber/rocks/releases.json'));
    expect(rocks.previews, ['preview-0.jpg', 'preview-1.webp']);
    expect(read('out/kits/amber/rocks/preview-1.webp'), webp);
    expect(
      KitReleases.parse(
        read('out/kits/brass-lamp/lamps/releases.json'),
      ).releases.single.containsScripts,
      isTrue,
    );
    final files = [
      for (final file in Directory(at('out')).listSync(recursive: true))
        if (file is File) file.path.substring(at('out').length + 1),
    ]..sort();
    expect(files, hasLength(7));
  });

  test('an empty entries folder builds an empty index', () async {
    write('entries/.gitkeep', '');
    expect(await build(), (kits: 0, releases: 0));
    expect(KitIndex.parse(read('out/index.json')).rows, isEmpty);
  });

  test(
    'refuses an output folder that is there and a missing entries folder',
    () async {
      Directory(at('out')).createSync();
      await expectLater(build(), throwsArgumentError);
      expect(Directory(at('out')).existsSync(), isTrue);
      await expectLater(
        buildKitStore(
          entriesPath: at('nowhere'),
          outPath: at('out2'),
          transport: MapTransport(pages),
        ),
        throwsArgumentError,
      );
    },
  );

  final cases = <String, (Future<void> Function(), String)>{
    'a hash that is not the pack\'s': (
      () async {
        final served = await kitFile('amber', 'rocks');
        entry('amber', 'rocks', [
          release(
            served,
            changes: {
              'hash': ContentHash.of([9]).hex,
            },
          ),
        ]);
      },
      'amber/rocks 1.0.0 could not be fetched',
    ),
    'a size that is not the file\'s': (
      () async {
        final served = await kitFile('amber', 'rocks');
        entry('amber', 'rocks', [
          release(served, changes: {'bytes': served.bytes + 1}),
        ]);
      },
      'amber/rocks 1.0.0: the file is',
    ),
    'a file that is not there': (
      () async {
        final served = await kitFile('amber', 'rocks');
        pages.clear();
        entry('amber', 'rocks', [release(served)]);
      },
      'could not be fetched',
    ),
    'a blob changed after packing': (
      () async {
        final served = await kitFile(
          'amber',
          'rocks',
          files: {'rock.obj': ascii.encode('UNIQUE-ROCK-TEXT')},
        );
        final bytes = pages.values.single.bytes as Uint8List;
        final at = latin1.decode(bytes).indexOf('UNIQUE');
        bytes[at] = 0x58;
        entry('amber', 'rocks', [release(served)]);
      },
      'could not be fetched',
    ),
    'a kit with no licence': (
      () async {
        final served = await brokenKit(
          'rocks',
          manifest: {
            ...manifestOf('amber', 'rocks', '1.0.0', const []),
            'kind': kitKind,
            'formatVersion': 1,
            'containsScripts': false,
          }..remove('licence'),
        );
        entry('amber', 'rocks', [release(served)]);
      },
      'licence',
    ),
    'a slug that is not the entry\'s': (
      () async {
        final served = await kitFile('amber', 'lamps');
        entry('amber', 'rocks', [release(served)]);
      },
      'slug "lamps" and the entry says "rocks"',
    ),
    'a publisher that is not the entry\'s': (
      () async {
        final served = await kitFile('brass-lamp', 'rocks');
        entry('amber', 'rocks', [release(served)]);
      },
      'publisher "brass-lamp" and the entry says "amber"',
    ),
    'a version that is not the entry\'s': (
      () async {
        final served = await kitFile('amber', 'rocks', version: '1.1.0');
        entry('amber', 'rocks', [release(served)]);
      },
      'version "1.1.0" and the entry says "1.0.0"',
    ),
    'a kit that breaks a rule': (
      () async {
        final served = await brokenKit(
          'rocks',
          files: {
            '.hidden': const [1],
          },
        );
        entry('amber', 'rocks', [release(served)]);
      },
      'starts with a dot',
    ),
    'a preview that is too big': (
      () async {
        final served = await kitFile(
          'amber',
          'rocks',
          files: {
            'a.png': [...png, ...List.filled(1024 * 1024, 0)],
          },
          previews: ['a.png'],
        );
        entry('amber', 'rocks', [release(served)]);
      },
      'A preview is at most',
    ),
    'a preview that is not a picture': (
      () async {
        final served = await kitFile(
          'amber',
          'rocks',
          files: {'a.png': ascii.encode('not a png')},
          previews: ['a.png'],
        );
        entry('amber', 'rocks', [release(served)]);
      },
      'does not start like a png image',
    ),
    'an entry that is not JSON': (
      () async {
        write('entries/amber/rocks.json', '{');
      },
      'The entry "amber/rocks" is not JSON',
    ),
    'an entry with a typo': (
      () async {
        final served = await kitFile('amber', 'rocks');
        write(
          'entries/amber/rocks.json',
          jsonEncode({
            'release': [release(served)],
          }),
        );
      },
      'not a key of this file',
    ),
    'a file that is not an entry': (
      () async {
        write('entries/amber/notes.txt', 'hello');
      },
      '"amber/notes.txt" is not an entry',
    ),
    'a file beside the publisher folders': (
      () async {
        write('entries/README.md', 'hello');
      },
      '"README.md" is not a folder of entries',
    ),
  };
  for (final MapEntry(:key, :value) in cases.entries) {
    test('refuses $key', () async {
      await value.$1();
      await fails(value.$2);
    });
  }

  test('lists every problem, not the first', () async {
    final rocks = await kitFile('amber', 'rocks');
    final crates = await kitFile('brass-lamp', 'crates');
    entry('amber', 'rocks', [
      release(rocks, changes: {'bytes': 1}),
    ]);
    entry('brass-lamp', 'crates', [release(crates, version: '1.0.1')]);
    await fails('amber/rocks 1.0.0: the file is');
    await expectLater(
      build(),
      throwsA(
        isA<FormatException>().having(
          (error) => error.message,
          'message',
          contains('brass-lamp/crates 1.0.1'),
        ),
      ),
    );
  });

  test('refuses a link where an entry belongs', () async {
    final served = await kitFile('amber', 'rocks');
    write(
      'real.json',
      jsonEncode({
        'releases': [release(served)],
      }),
    );
    Directory(at('entries/amber')).createSync();
    Link(at('entries/amber/rocks.json')).createSync(at('real.json'));
    await fails('is not an entry');
  }, skip: Platform.isWindows);

  test('refuses a release at an address that is not https', () async {
    final served = await kitFile('amber', 'rocks');
    final elsewhere = Uri.parse('http://example.com/amber-rocks-1.0.0.opack');
    pages[elsewhere] = pages[Uri.parse(served.url)]!;
    entry('amber', 'rocks', [
      release(served, changes: {'url': elsewhere.toString()}),
    ]);
    await fails('not an https address');
  });
}

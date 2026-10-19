import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:orblit_asset/src/kit/kit_manifest.dart';
import 'package:orblit_asset/src/pack/pack_bundle_io.dart';
import 'package:test/test.dart';

const authored = {
  'slug': 'crate-pack',
  'publisher': 'brass-lamp',
  'name': 'Crate pack',
  'summary': 'Twelve crates.',
  'version': '1.0.0',
  'licence': {'spdx': 'MIT', 'url': 'https://example.com/l'},
  'engine': {'from': '0.30.0', 'to': '0.40.0'},
  'tags': <String>[],
  'previews': <String>[],
};

late Directory scratch;
var made = 0;

String at(String name, [String? file]) =>
    [scratch.path, name, ?file].join(Platform.pathSeparator);

/// A kit folder holding [files], each a path and its text.
Future<String> folder(
  Map<String, String> files, {
  Map<String, Object?> manifest = authored,
}) async {
  final name = 'kit_${made++}';
  for (final MapEntry(:key, :value) in {
    ...files,
    'kit.json': jsonEncode(manifest),
  }.entries) {
    final file = File(at(name, key));
    await file.parent.create(recursive: true);
    await file.writeAsString(value);
  }
  return at(name);
}

Future<Kit> packed(String source) async {
  final out = at('out_${made++}.opack');
  await packKit(folder: source, outPath: out);
  return Kit.open(await AssetPack.open(FilePackBytes(out)));
}

Future<Kit> crates() async => packed(
  await folder({'models/crate.obj': 'v 0 0 0', 'readme.txt': 'Twelve.'}),
);

Future<String> project(Map<String, String> files) async {
  final name = 'project_${made++}';
  await Directory(at(name)).create();
  for (final MapEntry(:key, :value) in files.entries) {
    final file = File(at(name, key));
    await file.parent.create(recursive: true);
    await file.writeAsString(value);
  }
  return at(name);
}

String textOf(String path) => File(path).readAsStringSync();

/// Hand built, because [packKit] never writes a pack that is not a kit. It
/// holds [files], each a name and its text, and does not check their names.
Future<AssetPack> handBuilt({
  String target = kitTarget,
  Map<String, Object?> manifest = const {},
  Map<String, String> files = const {},
  ContentHash? base,
  bool withManifest = true,
}) async {
  final bytes = utf8.encode(
    jsonEncode(
      manifest.isEmpty
          ? {
              ...authored,
              'kind': kitKind,
              'formatVersion': 1,
              'containsScripts': false,
            }
          : manifest,
    ),
  );
  final texts = {
    for (final MapEntry(:key, :value) in files.entries)
      AssetId.parse(key): utf8.encode(value),
  };
  final entries = {
    if (withManifest)
      kitManifestId: AssetEntry(ContentHash.of(bytes), bytes.length),
    for (final MapEntry(:key, :value) in texts.entries)
      key: AssetEntry(ContentHash.of(Uint8List.fromList(value)), value.length),
  };
  final out = at('hand_${made++}.opack');
  await writePack(
    manifest: AssetManifest(entries: entries),
    outPath: out,
    target: target,
    read: (id) async => Uint8List.fromList(texts[id] ?? bytes),
    base: base,
  );
  return AssetPack.open(FilePackBytes(out));
}

/// Changes [from] to [to] in the pack file at [path], two texts of one length.
void tamper(String path, String from, String to) {
  final bytes = File(path).readAsBytesSync();
  final text = latin1.decode(bytes);
  final place = text.indexOf(from);
  expect(place, isNonNegative, reason: from);
  bytes.setRange(place, place + to.length, latin1.encode(to));
  File(path).writeAsBytesSync(bytes);
}

final class CountingBytes implements PackBytes {
  CountingBytes(this._inner);

  final PackBytes _inner;
  int reads = 0;

  @override
  Future<int> length() => _inner.length();

  @override
  Future<Uint8List> read(int at, int bytes) {
    reads++;
    return _inner.read(at, bytes);
  }
}

void main() {
  setUp(() async {
    scratch = await Directory.systemTemp.createTemp('orblit_kit_test_');
  });
  tearDown(() => scratch.delete(recursive: true));

  group('packKit', () {
    test('packs a folder into a kit that reads back', () async {
      final kit = await crates();
      expect(kit.manifest.slug, 'crate-pack');
      expect(kit.pack.target, 'source');
      expect(kit.files.keys.map((id) => '$id'), [
        'models/crate.obj',
        'readme.txt',
      ]);
      expect(
        utf8.decode(await kit.pack.read(AssetId.parse('readme.txt'))),
        ('Twelve.'),
      );
      expect(await kit.check(), isEmpty);
    });

    test('writes the same bytes for the same folder', () async {
      final source = await folder({'a.txt': 'one', 'b/c.txt': 'two'});
      await packKit(folder: source, outPath: at('first.opack'));
      await packKit(folder: source, outPath: at('second.opack'));
      expect(
        File(at('first.opack')).readAsBytesSync(),
        File(at('second.opack')).readAsBytesSync(),
      );
    });

    test(
      'counts the scripts itself and ignores what the author typed',
      () async {
        final source = await folder(
          {'tools/spawn.js': 'run()'},
          manifest: {...authored, 'containsScripts': false},
        );
        expect((await packed(source)).manifest.containsScripts, isTrue);
        expect((await crates()).manifest.containsScripts, isFalse);
      },
    );

    test('takes a folder written with a trailing slash', () async {
      final source = await folder({'a.txt': 'one', 'b/c.txt': 'two'});
      final out = at('slash.opack');
      await packKit(folder: '$source/', outPath: out);
      final kit = await Kit.open(await AssetPack.open(FilePackBytes(out)));
      expect(kit.files.keys.map((id) => '$id'), ['a.txt', 'b/c.txt']);
    });

    test('does not pack its own output on a second run', () async {
      final source = await folder({'a.txt': 'one'});
      final out = '$source${Platform.pathSeparator}crates.opack';
      await packKit(folder: source, outPath: out);
      final first = File(out).readAsBytesSync();
      await packKit(folder: source, outPath: out);
      expect(File(out).readAsBytesSync(), first);
    });

    test('refuses a file name outside the allowed characters', () async {
      final source = await folder({'caf\u00e9.png': 'x'});
      await expectLater(
        packKit(folder: source, outPath: at('out.opack')),
        throwsA(
          isA<FormatException>().having(
            (e) => e.message,
            'message',
            contains('letters, digits'),
          ),
        ),
      );
    });

    test('refuses a hidden file and names it', () async {
      final source = await folder({'a.txt': 'one', '.DS_Store': 'x'});
      await expectLater(
        packKit(folder: source, outPath: at('out.opack')),
        throwsA(
          isA<FormatException>().having(
            (e) => e.message,
            'message',
            contains('.DS_Store'),
          ),
        ),
      );
      expect(File(at('out.opack')).existsSync(), isFalse);
    });

    test('refuses a preview the folder does not hold', () async {
      final source = await folder(
        {'a.txt': 'one'},
        manifest: {
          ...authored,
          'previews': ['shot.png'],
        },
      );
      await expectLater(
        packKit(folder: source, outPath: at('out.opack')),
        throwsFormatException,
      );
    });

    test('refuses a link, which could lead out of the folder', () async {
      final source = await folder({'a.txt': 'one'});
      Link('$source${Platform.pathSeparator}outside').createSync(scratch.path);
      await expectLater(
        packKit(folder: source, outPath: at('out.opack')),
        throwsA(
          isA<FormatException>().having(
            (e) => e.message,
            'message',
            contains('is a link'),
          ),
        ),
      );
    });

    test('refuses a folder with no kit.json', () async {
      final source = await folder({'a.txt': 'one'});
      File('$source${Platform.pathSeparator}kit.json').deleteSync();
      await expectLater(
        packKit(folder: source, outPath: at('out.opack')),
        throwsFormatException,
      );
    });

    test('refuses a folder that is not there', () async {
      await expectLater(
        packKit(folder: at('missing'), outPath: at('out.opack')),
        throwsArgumentError,
      );
    });
  });

  group('Kit.open', () {
    test('refuses a pack made for a game target', () async {
      final pack = await handBuilt(target: 'android');
      await expectLater(Kit.open(pack), throwsFormatException);
    });

    test('refuses a patch', () async {
      final pack = await handBuilt(base: ContentHash.of(Uint8List(1)));
      await expectLater(Kit.open(pack), throwsFormatException);
    });

    test('refuses a pack with no kit.json', () async {
      final pack = await handBuilt(withManifest: false);
      await expectLater(Kit.open(pack), throwsFormatException);
    });

    test('refuses a manifest that says it is something else', () async {
      final pack = await handBuilt(
        manifest: {...authored, 'kind': 'orblit.theme', 'formatVersion': 1},
      );
      await expectLater(Kit.open(pack), throwsFormatException);
    });

    test('refuses a kit.json that is not what the pack lists', () async {
      final out = at('flipped.opack');
      await packKit(folder: await folder({'a.txt': 'one'}), outPath: out);
      tamper(out, 'Twelve crates.', 'Twelve crates!');
      await expectLater(
        Kit.open(await AssetPack.open(FilePackBytes(out))),
        throwsA(
          isA<FormatException>().having(
            (e) => e.message,
            'message',
            contains('does not match its hash'),
          ),
        ),
      );
    });

    test('refuses a kit.json over the size limit', () async {
      final pack = await handBuilt(
        manifest: {
          ...authored,
          'kind': kitKind,
          'formatVersion': 1,
          'containsScripts': false,
          'summary': 'x' * (kitManifestLimit + 1),
        },
      );
      await expectLater(Kit.open(pack), throwsFormatException);
    });

    test('a game does not mount a kit', () async {
      final kit = await crates();
      final games = MountedPacks(target: CookTargets.find('macos')!);
      expect(() => games.start(kit.pack), throwsA(isA<PackMountRefused>()));
    });
  });

  group('check', () {
    test('lists a file whose bytes changed in the pack', () async {
      final source = await folder({'a.txt': 'one'});
      final out = at('out.opack');
      await packKit(folder: source, outPath: out);
      final bytes = File(out).readAsBytesSync();
      final at0 = utf8.decode(bytes, allowMalformed: true).indexOf('one');
      bytes[at0] = 0x58;
      File(out).writeAsBytesSync(bytes);
      final kit = await Kit.open(await AssetPack.open(FilePackBytes(out)));
      expect(await kit.check(), ['"a.txt" does not match its hash.']);
    });

    test('lists a name that packing would have refused', () async {
      final kit = await Kit.open(
        await handBuilt(files: {'.git/hooks/pre-commit': 'x'}),
      );
      expect(await kit.check(), [
        '".git/hooks/pre-commit" has a part, ".git", that starts with a dot, '
            'so it would write a hidden file.',
      ]);
    });
  });

  group('conflictsIn', () {
    test(
      'lists a file with other bytes and reads no blob of the kit',
      () async {
        final out = at('counted.opack');
        await packKit(
          folder: await folder({
            'models/crate.obj': 'v 0 0 0',
            'same.txt': 'ok',
          }),
          outPath: out,
        );
        final bytes = CountingBytes(FilePackBytes(out));
        final kit = await Kit.open(await AssetPack.open(bytes));
        final reads = bytes.reads;
        final mine = await project({
          'models/crate.obj': 'mine',
          'same.txt': 'ok',
        });
        final found = await kit.conflictsIn(DirectoryAssetSource(mine));
        expect(found.map((id) => '$id'), ['models/crate.obj']);
        expect(bytes.reads, reads);
      },
    );
  });

  group('extractKit', () {
    test(
      'writes the files, never the manifest, and is safe to repeat',
      () async {
        final kit = await crates();
        final mine = await project({});
        final first = await extractKit(kit, mine);
        expect(first.map((id) => '$id'), ['models/crate.obj', 'readme.txt']);
        expect(textOf('$mine/models/crate.obj'), 'v 0 0 0');
        expect(File('$mine/kit.json').existsSync(), isFalse);
        expect(
          Directory(mine).listSync(recursive: true).whereType<File>(),
          hasLength(2),
        );
        expect(await extractKit(kit, mine), isEmpty);
      },
    );

    test('stops on a conflict before it writes anything', () async {
      final kit = await crates();
      final mine = await project({'readme.txt': 'mine'});
      await expectLater(
        extractKit(kit, mine),
        throwsA(
          isA<KitConflicts>().having((e) => e.ids.map((id) => '$id'), 'ids', [
            'readme.txt',
          ]),
        ),
      );
      expect(File('$mine/models/crate.obj').existsSync(), isFalse);
      expect(textOf('$mine/readme.txt'), 'mine');
    });

    test('overwrites a conflict when it is told to', () async {
      final kit = await crates();
      final mine = await project({'readme.txt': 'mine'});
      await extractKit(kit, mine, overwrite: true);
      expect(textOf('$mine/readme.txt'), 'Twelve.');
    });

    test('does not write through a link in the project', () async {
      final kit = await crates();
      final mine = await project({});
      final outside = await project({});
      Link('$mine/models').createSync(outside);
      await expectLater(extractKit(kit, mine), throwsFormatException);
      expect(Directory(outside).listSync(), isEmpty);
    });

    test('does not write over a file that is in the way of a folder', () async {
      final kit = await crates();
      final mine = await project({'models': 'a file'});
      await expectLater(extractKit(kit, mine), throwsFormatException);
      expect(textOf('$mine/models'), 'a file');
    });

    test('writes nothing when a later file is blocked', () async {
      final kit = await packed(
        await folder({'a.txt': 'one', 'z/b.txt': 'two'}),
      );
      final mine = await project({'z': 'a file'});
      await expectLater(extractKit(kit, mine), throwsFormatException);
      expect(File('$mine/a.txt').existsSync(), isFalse);
    });

    test('writes nothing when a later blob is corrupt', () async {
      final out = at('late.opack');
      await packKit(
        folder: await folder({'a.txt': 'one', 'z.txt': 'two'}),
        outPath: out,
      );
      tamper(out, 'two', 'twX');
      final kit = await Kit.open(await AssetPack.open(FilePackBytes(out)));
      final mine = await project({});
      await expectLater(extractKit(kit, mine), throwsFormatException);
      expect(Directory(mine).listSync(), isEmpty);
    });

    test('refuses a pack with a name that packing would refuse', () async {
      final kit = await Kit.open(
        await handBuilt(files: {'.git/hooks/pre-commit': 'x'}),
      );
      final mine = await project({});
      await expectLater(extractKit(kit, mine), throwsFormatException);
      expect(Directory(mine).listSync(), isEmpty);
    });

    test('does not write through a link at the place of a file', () async {
      final kit = await crates();
      final mine = await project({});
      final outside = await project({'target.txt': 'precious'});
      Link('$mine/readme.txt').createSync('$outside/target.txt');
      await expectLater(extractKit(kit, mine), throwsFormatException);
      expect(textOf('$outside/target.txt'), 'precious');
    });

    test('does not write over a folder at the place of a file', () async {
      final kit = await crates();
      final mine = await project({'readme.txt/inner.txt': 'x'});
      await expectLater(extractKit(kit, mine), throwsFormatException);
      expect(textOf('$mine/readme.txt/inner.txt'), 'x');
    });

    test('leaves a file that has the temporary name alone', () async {
      final kit = await crates();
      final mine = await project({'readme.txt.partial': 'precious'});
      await extractKit(kit, mine);
      expect(textOf('$mine/readme.txt.partial'), 'precious');
      expect(textOf('$mine/readme.txt'), 'Twelve.');
    });

    test('a conflict message shows no raw escape character', () {
      final text = KitConflicts([AssetId.parse('a\u001b[31m.png')]).toString();
      expect(text, isNot(contains('\u001b')));
      expect(text, contains(r'a\u001b[31m.png'));
    });

    test('refuses a project folder that is not there', () async {
      await expectLater(
        extractKit(await crates(), at('missing')),
        throwsArgumentError,
      );
    });
  });
}

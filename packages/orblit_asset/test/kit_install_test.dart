import 'dart:convert';
import 'dart:io';

import 'package:orblit_asset/orblit_asset.dart';
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

Future<void> write(String root, Map<String, String> files) async {
  for (final MapEntry(:key, :value) in files.entries) {
    final file = File('$root/$key');
    await file.parent.create(recursive: true);
    await file.writeAsString(value);
  }
}

/// A kit holding [files], each a path and its text.
Future<Kit> kitOf(Map<String, String> files) async {
  final source = at('kit_${made++}');
  await write(source, {...files, 'kit.json': jsonEncode(authored)});
  final out = at('out_${made++}.opack');
  await packKit(folder: source, outPath: out);
  return Kit.open(await AssetPack.open(FilePackBytes(out)));
}

Future<String> project(Map<String, String> files) async {
  final root = at('project_${made++}');
  await Directory(root).create();
  await write(root, files);
  return root;
}

Set<AssetId> ids(List<String> names) => {
  for (final n in names) AssetId.parse(n),
};

List<String> names(Iterable<AssetId> list) => [for (final id in list) '$id'];

void main() {
  setUp(() async {
    scratch = await Directory.systemTemp.createTemp('orblit_install_test_');
  });
  tearDown(() => scratch.delete(recursive: true));

  group('into and only', () {
    test('writes the picked files under the folder', () async {
      final kit = await kitOf({
        'models/crate.obj': 'v 0 0 0',
        'readme.txt': 'Twelve.',
      });
      final mine = await project({});
      final written = await extractKit(
        kit,
        mine,
        into: 'assets/kits/crate-pack',
        only: ids(['models/crate.obj']),
      );
      expect(names(written), ['models/crate.obj']);
      expect(
        File(
          '$mine/assets/kits/crate-pack/models/crate.obj',
        ).readAsStringSync(),
        'v 0 0 0',
      );
      expect(
        File('$mine/assets/kits/crate-pack/readme.txt').existsSync(),
        isFalse,
      );
      expect(File('$mine/models/crate.obj').existsSync(), isFalse);
    });

    test('looks for conflicts at the new place and in the pick only', () async {
      final kit = await kitOf({'a.txt': 'kit a', 'b.txt': 'kit b'});
      final mine = await project({
        'kits/a.txt': 'mine',
        'kits/b.txt': 'kit b',
        'b.txt': 'elsewhere',
      });
      final source = DirectoryAssetSource(mine);
      expect(names(await kit.conflictsIn(source, into: 'kits')), ['a.txt']);
      expect(
        await kit.conflictsIn(source, into: 'kits', only: ids(['b.txt'])),
        isEmpty,
      );
      expect(names(await kit.conflictsIn(source)), ['b.txt']);
    });

    test('a conflict outside the pick does not stop the install', () async {
      final kit = await kitOf({'a.txt': 'kit a', 'b.txt': 'kit b'});
      final mine = await project({'a.txt': 'mine'});
      await extractKit(kit, mine, only: ids(['b.txt']));
      expect(File('$mine/a.txt').readAsStringSync(), 'mine');
      expect(File('$mine/b.txt').readAsStringSync(), 'kit b');
    });

    test('a conflict inside the pick stops it and writes nothing', () async {
      final kit = await kitOf({'a.txt': 'kit a', 'b.txt': 'kit b'});
      final mine = await project({'kits/a.txt': 'mine'});
      await expectLater(
        extractKit(kit, mine, into: 'kits'),
        throwsA(isA<KitConflicts>()),
      );
      expect(File('$mine/kits/b.txt').existsSync(), isFalse);
    });

    test('refuses a name the kit does not hold', () async {
      final kit = await kitOf({'a.txt': 'kit a'});
      final mine = await project({});
      expect(
        () => extractKit(kit, mine, only: ids(['kit.json'])),
        throwsArgumentError,
      );
      expect(
        () => kit.conflictsIn(DirectoryAssetSource(mine), only: ids(['x.txt'])),
        throwsArgumentError,
      );
    });

    test('refuses a folder that would write somewhere hidden', () async {
      final kit = await kitOf({'a.txt': 'kit a'});
      final mine = await project({});
      for (final into in ['.git/hooks', 'a//b', '/abs', 'dir/', 'nul']) {
        await expectLater(
          extractKit(kit, mine, into: into),
          throwsFormatException,
          reason: into,
        );
      }
      expect(Directory(mine).listSync(), isEmpty);
    });

    test('refuses a folder that is a link', () async {
      final kit = await kitOf({'a.txt': 'kit a'});
      final mine = await project({});
      final outside = await Directory(at('outside')).create();
      Link('$mine/kits').createSync(outside.path);
      await expectLater(
        extractKit(kit, mine, into: 'kits'),
        throwsFormatException,
      );
      expect(outside.listSync(), isEmpty);
    });
  });

  group('scripts', () {
    test('lists the files a person could run', () async {
      final kit = await kitOf({
        'models/crate.obj': 'v 0 0 0',
        'tool/build.sh': 'echo hi',
        'run.js': '1',
      });
      expect(names(kit.scripts), ['run.js', 'tool/build.sh']);
      expect(kit.manifest.containsScripts, isTrue);
    });

    test('is empty for a kit of art', () async {
      expect((await kitOf({'models/crate.obj': 'v 0 0 0'})).scripts, isEmpty);
    });
  });
}

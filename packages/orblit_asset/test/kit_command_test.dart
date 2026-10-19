import 'dart:convert';
import 'dart:io';

import 'package:test/test.dart';

late Directory scratch;

String at(String name) => '${scratch.path}${Platform.pathSeparator}$name';

Future<ProcessResult> kit(List<String> arguments) => Process.run(
  Platform.resolvedExecutable,
  ['run', 'bin/kit.dart', ...arguments],
  workingDirectory: Directory.current.path,
);

void write(String path, String text) {
  File(path)
    ..parent.createSync(recursive: true)
    ..writeAsStringSync(text);
}

/// A folder holding a manifest and `models/crate.obj`.
String source() {
  final folder = at('crates');
  write(
    '$folder/kit.json',
    jsonEncode({
      'slug': 'crate-pack',
      'publisher': 'brass-lamp',
      'name': 'Crate pack',
      'summary': 'Twelve crates.',
      'version': '1.0.0',
      'licence': {'spdx': 'MIT', 'url': 'https://example.com/l'},
      'engine': {'from': '0.30.0', 'to': '0.40.0'},
      'tags': <String>[],
      'previews': <String>[],
    }),
  );
  write('$folder/models/crate.obj', 'v 0 0 0');
  return folder;
}

void main() {
  setUp(() => scratch = Directory.systemTemp.createTempSync('orblit_kit_cli_'));
  tearDown(() => scratch.deleteSync(recursive: true));

  test('packs, checks and installs a kit', () async {
    final packed = await kit(['pack', source(), '-o', at('crates.opack')]);
    expect(packed.exitCode, 0, reason: '${packed.stderr}');
    final checked = await kit(['check', at('crates.opack')]);
    expect(checked.exitCode, 0, reason: '${checked.stderr}');
    expect(checked.stdout, contains('Crate pack 1.0.0, 1 files'));
    Directory(at('mine')).createSync();
    final installed = await kit(['extract', at('crates.opack'), at('mine')]);
    expect(installed.exitCode, 0, reason: '${installed.stderr}');
    expect(File(at('mine/models/crate.obj')).readAsStringSync(), 'v 0 0 0');
  });

  test(
    'an install over a changed file exits 1 until told to overwrite',
    () async {
      await kit(['pack', source(), '-o', at('crates.opack')]);
      write(at('mine/models/crate.obj'), 'mine');
      final stopped = await kit(['extract', at('crates.opack'), at('mine')]);
      expect(stopped.exitCode, 1);
      expect(stopped.stderr, contains('models/crate.obj'));
      expect(stopped.stderr, contains('--overwrite'));
      final forced = await kit([
        'extract',
        at('crates.opack'),
        at('mine'),
        '--overwrite',
      ]);
      expect(forced.exitCode, 0, reason: '${forced.stderr}');
      expect(File(at('mine/models/crate.obj')).readAsStringSync(), 'v 0 0 0');
    },
  );

  test('a folder that breaks a rule exits 1 and names the file', () async {
    write('${source()}/.DS_Store', 'x');
    final result = await kit(['pack', at('crates'), '-o', at('crates.opack')]);
    expect(result.exitCode, 1);
    expect(result.stderr, contains('.DS_Store'));
  });

  test('a file that is not a pack exits 1', () async {
    write(at('plain.opack'), 'not a pack, and long enough to read a header');
    expect((await kit(['check', at('plain.opack')])).exitCode, 1);
  });

  test('arguments that make no sense exit 2', () async {
    expect((await kit([])).exitCode, 2);
    expect((await kit(['spin'])).exitCode, 2);
    expect((await kit(['pack', at('crates')])).exitCode, 2);
    expect((await kit(['check'])).exitCode, 2);
    expect((await kit(['index', at('kits')])).exitCode, 2);
  });

  test('an empty entries folder builds an empty index', () async {
    write(at('kits/.gitkeep'), '');
    final built = await kit(['index', at('kits'), '-o', at('site')]);
    expect(built.exitCode, 0, reason: '${built.stderr}');
    expect(built.stdout, contains('0 kits, 0 releases'));
    expect(File(at('site/index.json')).existsSync(), isTrue);
  });

  test('an index goes to a new folder and names a bad entry', () async {
    Directory(at('kits')).createSync();
    Directory(at('site')).createSync();
    final there = await kit(['index', at('kits'), '-o', at('site')]);
    expect(there.exitCode, 1);
    expect(there.stderr, contains('is there already'));
    write(at('kits/amber/rocks.json'), '{');
    final bad = await kit(['index', at('kits'), '-o', at('fresh')]);
    expect(bad.exitCode, 1);
    expect(bad.stderr, contains('amber/rocks'));
    expect(Directory(at('fresh')).existsSync(), isFalse);
  });
}

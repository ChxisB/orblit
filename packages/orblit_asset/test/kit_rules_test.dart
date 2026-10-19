import 'package:orblit_asset/orblit_asset.dart';
import 'package:orblit_asset/src/kit/kit_rules.dart';
import 'package:test/test.dart';

KitManifest manifest({
  bool containsScripts = false,
  List<AssetId> previews = const [],
}) => KitManifest(
  slug: 'crate-pack',
  publisher: 'brass-lamp',
  name: 'Crate pack',
  summary: 'Twelve crates.',
  version: '1.0.0',
  licence: (spdx: 'MIT', url: 'https://example.com/l'),
  engine: (from: '0.30.0', to: '0.40.0'),
  containsScripts: containsScripts,
  previews: previews,
);

Map<AssetId, int> files(List<String> names) => {
  for (final name in names) AssetId.parse(name): 10,
};

List<String> problems(
  List<String> names, {
  KitManifest? kit,
  int limit = kitFileLimit,
}) => kitProblems(kit ?? manifest(), files(names), fileLimit: limit);

void main() {
  test('a good kit has no problems', () {
    expect(problems(['models/crate.glb', 'textures/wall.png']), isEmpty);
  });

  test('a kit with no files is a problem', () {
    expect(problems([]), ['The kit holds no files.']);
  });

  test('a hidden file or folder is a problem', () {
    for (final name in ['.env', '.git/hooks/pre-commit', 'a/.b/c.png']) {
      expect(problems([name]), hasLength(1), reason: name);
    }
  });

  test('a name Windows reads as something else is a problem', () {
    for (final name in ['con', 'models/NUL.txt', 'a/aux.png', 'lpt1.png']) {
      expect(problems([name]), hasLength(1), reason: name);
    }
    for (final name in ['a.png.', 'models /a.png', 'a.png ']) {
      expect(problems([name]), hasLength(1), reason: name);
    }
    expect(problems(['console.png', 'auxiliary/a.png']), isEmpty);
  });

  test('a control character in a name is a problem', () {
    expect(problems(['a\u0001b.png']), hasLength(1));
  });

  test('a file over the limit is a problem', () {
    expect(
      kitProblems(manifest(), {AssetId.parse('a.bin'): 101}, fileLimit: 100),
      hasLength(1),
    );
    expect(
      kitProblems(manifest(), {AssetId.parse('a.bin'): 100}, fileLimit: 100),
      isEmpty,
    );
  });

  test('two names that differ only in case are a problem', () {
    expect(problems(['a/Wall.png', 'a/wall.png']), hasLength(1));
  });

  test('a preview that is not in the kit is a problem', () {
    final kit = manifest(previews: [AssetId.parse('previews/a.png')]);
    expect(problems(['models/a.glb'], kit: kit), hasLength(1));
    expect(problems(['previews/a.png'], kit: kit), isEmpty);
  });

  test('the scripts flag must match the files', () {
    expect(problems(['scripts/run.js']), [
      'The manifest says the kit holds no scripts and it holds some.',
    ]);
    expect(problems(['a.png'], kit: manifest(containsScripts: true)), [
      'The manifest says the kit holds scripts and it holds none.',
    ]);
    expect(
      problems(['scripts/run.js'], kit: manifest(containsScripts: true)),
      isEmpty,
    );
  });

  test('carriesScripts counts what a person could run or compile', () {
    for (final name in ['a.js', 'a.DART', 'x/y.sh', 'b.ps1', 'c.py']) {
      expect(carriesScripts([AssetId.parse(name)]), isTrue, reason: name);
    }
    expect(carriesScripts(files(['a.glb', 'b.png', 'c.json']).keys), isFalse);
  });
}

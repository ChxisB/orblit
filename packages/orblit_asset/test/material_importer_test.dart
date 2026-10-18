@TestOn('vm')
library;

import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:test/test.dart';

const _source = '''
render_mode unshaded;
uniform vec4 tint : source_color = vec4(1.0);
void fragment() {
  ALBEDO = tint.rgb;
}
''';

ImportRequest _request(
  String text, {
  String path = 'materials/flat.oshader',
  CookTarget target = CookTargets.macos,
  Map<String, String> files = const {},
}) {
  final source = MemoryAssetSource({
    AssetId.parse(path): Uint8List.fromList(utf8.encode(text)),
    for (final entry in files.entries)
      AssetId.parse(entry.key): Uint8List.fromList(utf8.encode(entry.value)),
  });
  return ImportRequest(
    id: AssetId.parse(path),
    bytes: Uint8List.fromList(utf8.encode(text)),
    settings: const {},
    target: target,
    source: source,
  );
}

void main() {
  late Directory work;

  /// A matc that records its arguments and then does [body]. It is a real
  /// process run through the real [NativeTool], so the flags and the exit
  /// code are what the importer would meet.
  NativeTool fakeMatc(String body) {
    final shim = File('${work.path}/matc')
      ..writeAsStringSync('''#!/bin/sh
echo "\$@" >> "${work.path}/record"
[ "\$1" = "--version" ] && { echo 77; exit 0; }
out=""
while [ \$# -gt 0 ]; do [ "\$1" = "-o" ] && out="\$2"; shift; done
$body
''');
    Process.runSync('chmod', ['+x', shim.path]);
    return NativeTool('matc', path: shim.path);
  }

  List<String> flags() => File(
    '${work.path}/record',
  ).readAsLinesSync().where((line) => line != '--version').toList();

  setUp(() => work = Directory.systemTemp.createTempSync('orblit_matc_test'));
  tearDown(() => work.deleteSync(recursive: true));

  test('writes the package and the parameter list', () async {
    final importer = MaterialImporter(
      tool: fakeMatc('printf PACKAGE > "\$out"'),
    );

    final result = await importer.import(_request(_source));

    expect(utf8.decode(result.outputs['material.filamat']!), 'PACKAGE');
    final params = jsonDecode(utf8.decode(result.outputs['params.json']!));
    expect(params, [
      {
        'name': 'tint',
        'type': 'vec4',
        'instance': false,
        'hints': {'source_color': <String>[]},
        'default': [1, 1, 1, 1],
      },
    ]);
  });

  test('asks matc for the backends of the target', () async {
    final importer = MaterialImporter(tool: fakeMatc('echo x > "\$out"'));

    await importer.import(_request(_source));
    await importer.import(_request(_source, target: CookTargets.android));

    final runs = flags();
    expect(runs[0], startsWith('-a metal -p all -o '));
    expect(runs[1], startsWith('-a opengl -a vulkan -p mobile -o '));
  });

  test('puts the matc version in the settings', () {
    final importer = MaterialImporter(tool: fakeMatc('true'));
    expect(importer.resolveSettings(const ImportSettings()), {'matc': '77'});
  });

  test('reports a matc error at the line the author wrote', () async {
    final importer = MaterialImporter(
      tool: fakeMatc('''
echo "ERROR: 0:14: 'tinnt' : undeclared identifier" >&2
echo "ERROR: 0:14: '' : compilation terminated" >&2
exit 1'''),
    );

    // The material has an 10 line header, so its line 14 is the author's 4.
    final text = _source.replaceFirst('tint.rgb', 'tinnt.rgb');
    final failure = await importer
        .import(_request(text))
        .then<ImportFailure?>(
          (_) => null,
          onError: (Object e) => e as ImportFailure,
        );

    expect(failure, isNotNull);
    expect(failure!.reason, startsWith('materials/flat.oshader:4: '));
    expect(failure.reason, contains("'tinnt' : undeclared identifier"));
    expect(failure.reason, isNot(contains('compilation terminated')));
  });

  test('reports what the translator refuses without starting matc', () async {
    final importer = MaterialImporter(tool: fakeMatc('exit 1'));

    expect(
      () => importer.import(_request('void vertex() {}\nvoid fragment() {}')),
      throwsA(
        isA<ImportFailure>().having(
          (f) => f.reason,
          'reason',
          'materials/flat.oshader:1: `vertex()` is not supported yet',
        ),
      ),
    );
    expect(File('${work.path}/record').existsSync(), isFalse);
  });

  test('reads included files and lists them as dependencies', () async {
    final importer = MaterialImporter(tool: fakeMatc('echo x > "\$out"'));
    final text =
        '#include "util.oshaderinc"\nvoid fragment() { ROUGHNESS = half(1.0); }';
    final request = _request(
      text,
      files: {
        'materials/util.oshaderinc': 'float half(float x) { return x * 0.5; }',
      },
    );

    await importer.import(request);
    final dependencies = await importer.dependenciesIn(
      request.id,
      request.bytes,
      const {},
      request.source,
    );

    expect(dependencies, {AssetId.parse('materials/util.oshaderinc')});
  });

  test('an include inside an include is a dependency too', () async {
    final importer = MaterialImporter(tool: fakeMatc('echo x > "\$out"'));
    final request = _request(
      '#include "a.oshaderinc"\nvoid fragment() { ROUGHNESS = half(1.0); }',
      files: {
        'materials/a.oshaderinc': '#include "deep/b.oshaderinc"',
        'materials/deep/b.oshaderinc': 'float half(float x) { return x; }',
      },
    );

    final dependencies = await importer.dependenciesIn(
      request.id,
      request.bytes,
      const {},
      request.source,
    );

    expect(dependencies, {
      AssetId.parse('materials/a.oshaderinc'),
      AssetId.parse('materials/deep/b.oshaderinc'),
    });
    await importer.import(request);
  });

  test('an include nobody wrote is told at its line, not as a missing asset', () {
    final importer = MaterialImporter(tool: fakeMatc('exit 1'));
    final request = _request('#include "gone.oshaderinc"\nvoid fragment() {}');

    expect(
      importer.dependenciesIn(
        request.id,
        request.bytes,
        const {},
        request.source,
      ),
      completion(isEmpty),
    );
    expect(
      () => importer.import(request),
      throwsA(
        isA<ImportFailure>().having(
          (f) => f.reason,
          'reason',
          'materials/flat.oshader:1: no file `materials/gone.oshaderinc` to include',
        ),
      ),
    );
  });

  final real = NativeTool('matc', environmentVariable: 'ORBLIT_MATC').locate();
  test('cooks with the real matc', () async {
    final importer = MaterialImporter();

    final good = await importer.import(_request(_source));
    expect(good.outputs['material.filamat']!.length, greaterThan(1000));

    final text = _source.replaceFirst('tint.rgb', 'tinnt.rgb');
    expect(
      () => importer.import(_request(text)),
      throwsA(
        isA<ImportFailure>().having(
          (f) => f.reason,
          'reason',
          startsWith('materials/flat.oshader:4: '),
        ),
      ),
    );
  }, skip: real == null ? 'no matc: set ORBLIT_MATC to run this' : false);
}

import 'dart:convert';
import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:test/test.dart';

AssetId id(String text) => AssetId.parse(text);

/// Counts its runs, so a test can tell a real import from a cache hit.
class CountingImporter extends Importer {
  CountingImporter();

  int runs = 0;

  @override
  String get name => 'counting';

  @override
  int get version => 1;

  @override
  Set<String> get extensions => const {'glb', 'png'};

  @override
  Map<String, Object?> resolveSettings(ImportSettings settings) => {
    'tint': settings.values['tint'],
  };

  @override
  Future<ImportResult> import(ImportRequest request) async {
    runs++;
    final tint = request.settings['tint'];
    return ImportResult(
      outputs: {
        'out': [...request.bytes, ...utf8.encode(' tint=$tint')],
      },
    );
  }
}

void main() {
  late MemoryCookCache cache;
  late CountingImporter importer;
  late RuntimeImport runtime;

  setUp(() {
    cache = MemoryCookCache();
    importer = CountingImporter();
    runtime = RuntimeImport(
      cache: cache,
      target: CookTargets.macos,
      importers: ImporterRegistry([importer]),
    );
  });

  group('naming a file a user chose', () {
    test('keeps the part the user would recognise', () {
      expect(
        RuntimeImport.nameFor('/var/mobile/Containers/My Model.glb'),
        id('My Model.glb'),
      );
      expect(
        RuntimeImport.nameFor(r'C:\Users\chris\Downloads\robot.glb'),
        id('robot.glb'),
      );
      expect(RuntimeImport.nameFor('robot.glb'), id('robot.glb'));
    });

    test('drops what a URL carries after the name', () {
      expect(
        RuntimeImport.nameFor('https://example.com/a/robot.glb?v=2#frag'),
        id('robot.glb'),
      );
    });

    test('a colon cannot survive, because an id is not a URL', () {
      expect(RuntimeImport.nameFor('file:robot.glb'), id('file_robot.glb'));
    });

    test('walks back past a trailing slash rather than giving up', () {
      expect(RuntimeImport.nameFor('models/robot.glb/'), id('robot.glb'));
    });

    test('gives nothing when there is no name left to take', () {
      expect(RuntimeImport.nameFor('/'), isNull);
      expect(RuntimeImport.nameFor('   '), isNull);
      expect(RuntimeImport.nameFor('../..'), isNull);
    });
  });

  group('importing a file', () {
    test('produces what the importer made', () async {
      final result = await runtime.import(id('robot.glb'), utf8.encode('body'));
      expect(result.status, CookStatus.cooked);
      expect(
        utf8.decode(await runtime.readOnly(result.asset!) as Uint8List),
        'body tint=null',
      );
    });

    test('the same file twice costs one import', () async {
      await runtime.import(id('robot.glb'), utf8.encode('body'));
      final again = await runtime.import(id('robot.glb'), utf8.encode('body'));
      expect(importer.runs, 1);
      expect(again.status, CookStatus.cached);
    });

    test(
      'a different file is a different import, under the same name',
      () async {
        await runtime.import(id('robot.glb'), utf8.encode('body'));
        final other = await runtime.import(
          id('robot.glb'),
          utf8.encode('other'),
        );
        expect(importer.runs, 2);
        expect(other.status, CookStatus.cooked);
      },
    );

    test(
      'settings given at the call reach the importer, and change the key',
      () async {
        final plain = await runtime.import(
          id('robot.glb'),
          utf8.encode('body'),
        );
        final tinted = await runtime.import(
          id('robot.glb'),
          utf8.encode('body'),
          settings: const ImportSettings(values: {'tint': 'red'}),
        );
        expect(importer.runs, 2);
        expect(tinted.key, isNot(plain.key));
        expect(
          utf8.decode(await runtime.readOnly(tinted.asset!) as Uint8List),
          'body tint=red',
        );
      },
    );

    test('a file nothing claims is a result, not an exception', () async {
      final result = await runtime.import(
        id('notes.txt'),
        utf8.encode('hello'),
      );
      expect(result.status, CookStatus.skipped);
      expect(result.asset, isNull);
    });

    test('an importer that fails is reported rather than thrown', () async {
      final failing = RuntimeImport(
        cache: cache,
        target: CookTargets.macos,
        importers: ImporterRegistry([const _AlwaysFails()]),
      );
      final result = await failing.import(id('robot.glb'), utf8.encode('x'));
      expect(result.status, CookStatus.failed);
      expect('${result.error}', contains('never works'));
    });

    test('says up front whether it can open a file', () {
      expect(runtime.handles(id('robot.glb')), isTrue);
      expect(runtime.handles(id('notes.txt')), isFalse);
    });
  });

  group('a file that is not self-contained', () {
    late RuntimeImport gltf;

    setUp(() {
      gltf = RuntimeImport(
        cache: cache,
        target: CookTargets.macos,
        importers: ImporterRegistry([const GltfImporter()]),
      );
    });

    String document() => jsonEncode({
      'buffers': [
        {'uri': 'robot.bin'},
      ],
    });

    test('fails when the files it names were not handed over too', () async {
      final result = await gltf.import(
        id('robot.gltf'),
        utf8.encode(document()),
      );
      expect(result.status, CookStatus.failed);
      expect('${result.error}', contains('robot.bin'));
    });

    test('imports when they were', () async {
      final result = await gltf.import(
        id('robot.gltf'),
        utf8.encode(document()),
        alongside: MemoryAssetSource({
          id('robot.bin'): utf8.encode('vertices'),
        }),
      );
      expect(result.status, CookStatus.cooked);
      expect(result.dependencies, contains(id('robot.bin')));
    });

    test('changing only a file it points at re-imports it', () async {
      final alongside = {id('robot.bin'): utf8.encode('vertices')};
      await gltf.import(
        id('robot.gltf'),
        utf8.encode(document()),
        alongside: MemoryAssetSource(alongside),
      );
      final again = await gltf.import(
        id('robot.gltf'),
        utf8.encode(document()),
        alongside: MemoryAssetSource({
          id('robot.bin'): utf8.encode('different vertices'),
        }),
      );
      expect(
        again.status,
        CookStatus.cooked,
        reason: 'the document did not change, but what it points at did',
      );
    });
  });

  group('reading an import back', () {
    test('by the name the importer gave it', () async {
      final result = await runtime.import(id('robot.glb'), utf8.encode('body'));
      expect(await runtime.read(result.asset!, 'out'), isNotNull);
      expect(await runtime.read(result.asset!, 'nope'), isNull);
    });

    test('readOnly refuses to guess when there is more than one', () async {
      final many = RuntimeImport(
        cache: cache,
        target: CookTargets.macos,
        importers: ImporterRegistry([const _TwoOutputs()]),
      );
      final result = await many.import(id('robot.glb'), utf8.encode('x'));
      expect(await many.readOnly(result.asset!), isNull);
      expect(await many.read(result.asset!, 'a'), isNotNull);
    });
  });

  test('brings the same importers a build machine has', () {
    final registry = RuntimeImport.defaultImporters();
    expect(registry.byName('gltf'), isNotNull);
    expect(registry.byName('scene'), isNotNull);
  });
}

class _AlwaysFails extends Importer {
  const _AlwaysFails();

  @override
  String get name => 'fails';

  @override
  int get version => 1;

  @override
  Set<String> get extensions => const {'glb'};

  @override
  Map<String, Object?> resolveSettings(ImportSettings settings) => const {};

  @override
  Future<ImportResult> import(ImportRequest request) =>
      throw ImportFailure(request.id, 'this importer never works');
}

class _TwoOutputs extends Importer {
  const _TwoOutputs();

  @override
  String get name => 'two';

  @override
  int get version => 1;

  @override
  Set<String> get extensions => const {'glb'};

  @override
  Map<String, Object?> resolveSettings(ImportSettings settings) => const {};

  @override
  Future<ImportResult> import(ImportRequest request) async =>
      ImportResult(outputs: {'a': utf8.encode('a'), 'b': utf8.encode('b')});
}

import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:test/test.dart';

AssetId id(String text) => AssetId.parse(text);

/// A cooked output's actual bytes. [CookOutput.bytes] is how many there are,
/// not what they say, so reading one goes back through the cache.
Future<String> textOf(
  CookCache cache,
  CookResult result,
  String output,
) async => utf8.decode((await cache.read(result.asset![output]!.hash))!);

/// An importer that records what it was asked, so a test can assert that the
/// cook did *not* ask — which is the whole claim the cache makes.
class RecordingImporter extends Importer {
  RecordingImporter({
    this.name = 'recording',
    this.version = 1,
    this.extensions = const {'txt'},
    this.defaults = const {'mode': 'normal'},
    this.discovers = const {},
    this.transform,
  });

  @override
  final String name;
  @override
  final int version;
  @override
  final Set<String> extensions;

  final Map<String, Object?> defaults;

  /// Dependencies to report for an asset, by the asset's id.
  final Map<AssetId, Set<AssetId>> discovers;

  /// What to write, given the request. The default echoes the source.
  final List<int> Function(ImportRequest request)? transform;

  final List<AssetId> imported = [];
  final List<AssetId> asked = [];

  @override
  Map<String, Object?> resolveSettings(ImportSettings settings) {
    final resolved = {...defaults};
    for (final entry in settings.values.entries) {
      if (!defaults.containsKey(entry.key)) continue;
      resolved[entry.key] = entry.value;
    }
    return resolved;
  }

  @override
  Future<Set<AssetId>> dependenciesOf(
    AssetId id,
    Uint8List bytes,
    Map<String, Object?> settings,
  ) async {
    asked.add(id);
    return discovers[id] ?? const {};
  }

  @override
  Future<ImportResult> import(ImportRequest request) async {
    imported.add(request.id);
    final bytes = transform?.call(request) ?? request.bytes;
    return ImportResult(outputs: {'out': bytes});
  }
}

void main() {
  group('CookTarget', () {
    test('puts its name in the recipe, so two targets never share a key', () {
      expect(const CookTarget(name: 'macos').recipe, {'name': 'macos'});
    });

    test('carries its properties alongside', () {
      const target = CookTarget(
        name: 'android',
        properties: {
          'textureFamilies': ['astc'],
          'maxTextureSize': 2048,
        },
      );
      expect(target.recipe, {
        'name': 'android',
        'textureFamilies': ['astc'],
        'maxTextureSize': 2048,
      });
    });

    test('reads the properties the built-in importers agree on', () {
      const target = CookTarget(
        name: 'ios',
        properties: {
          'textureFamilies': ['astc', 'basis'],
          'maxTextureSize': 4096,
          'halfFloatTextures': true,
        },
      );
      expect(target.textureFamilies, ['astc', 'basis']);
      expect(target.maxTextureSize, 4096);
      expect(target.halfFloatTextures, isTrue);
    });

    test('an absent property is absent, not a wrong default', () {
      expect(CookTarget.any.textureFamilies, isEmpty);
      expect(CookTarget.any.maxTextureSize, isNull);
      expect(CookTarget.any.halfFloatTextures, isFalse);
    });

    test('refuses a property of the wrong shape, naming it', () {
      const target = CookTarget(
        name: 'bad',
        properties: {'maxTextureSize': -1, 'textureFamilies': 'astc'},
      );
      expect(
        () => target.maxTextureSize,
        throwsA(
          isA<ArgumentError>().having((e) => e.name, 'name', 'maxTextureSize'),
        ),
      );
      expect(
        () => target.textureFamilies,
        throwsA(
          isA<ArgumentError>().having((e) => e.name, 'name', 'textureFamilies'),
        ),
      );
    });
  });

  group('ImporterRegistry', () {
    test('picks by extension, in registration order', () {
      final first = RecordingImporter(name: 'first', extensions: {'png'});
      final second = RecordingImporter(name: 'second', extensions: {'png'});
      final registry = ImporterRegistry([first, second]);
      expect(registry.forAsset(id('a.png')), same(first));
    });

    test('a named importer beats the extension', () {
      final byExtension = RecordingImporter(
        name: 'by-extension',
        extensions: {'png'},
      );
      final named = RecordingImporter(name: 'named', extensions: {'exr'});
      final registry = ImporterRegistry([byExtension, named]);
      expect(
        registry.forAsset(id('a.png'), const ImportSettings(importer: 'named')),
        same(named),
      );
    });

    test('naming an importer that is not registered is an error that lists '
        'the ones that are', () {
      final registry = ImporterRegistry([RecordingImporter(name: 'texture')]);
      expect(
        () => registry.forAsset(
          id('a.txt'),
          const ImportSettings(importer: 'textrue'),
        ),
        throwsA(
          isA<ArgumentError>().having(
            (e) => e.message.toString(),
            'message',
            contains('texture'),
          ),
        ),
      );
    });

    test('two importers may not share a name, because they would share '
        'cache entries', () {
      final registry = ImporterRegistry([RecordingImporter()]);
      expect(() => registry.add(RecordingImporter()), throwsArgumentError);
    });

    test('an unclaimed asset is nobody, not an error', () {
      final registry = ImporterRegistry([RecordingImporter()]);
      expect(registry.forAsset(id('README.md')), isNull);
    });
  });

  group('Cook', () {
    late MemoryAssetSource source;
    late MemoryCookCache cache;
    late RecordingImporter importer;

    Cook cookFor({
      CookTarget target = CookTarget.any,
      RecordingImporter? with_,
    }) {
      importer = with_ ?? importer;
      return Cook(
        source: source,
        cache: cache,
        importers: ImporterRegistry([importer]),
        target: target,
      );
    }

    setUp(() {
      source = MemoryAssetSource({
        id('a.txt'): utf8.encode('alpha'),
        id('b.txt'): utf8.encode('beta'),
        id('README.md'): utf8.encode('read me'),
      });
      cache = MemoryCookCache();
      importer = RecordingImporter();
    });

    test('cooks an asset and files the result under its key', () async {
      final result = await cookFor().cookOne(id('a.txt'));
      expect(result.status, CookStatus.cooked);
      expect(result.importer, same(importer));
      expect(await textOf(cache, result, 'out'), 'alpha');
      expect(await cache.lookUp(result.key!), isNotNull);
    });

    test('a second cook of an unchanged asset runs no importer', () async {
      final cook = cookFor();
      await cook.cookOne(id('a.txt'));
      expect(importer.imported, [id('a.txt')]);

      final again = await cookFor().cookOne(id('a.txt'));
      expect(again.status, CookStatus.cached);
      expect(await textOf(cache, again, 'out'), 'alpha');
      expect(importer.imported, [
        id('a.txt'),
      ], reason: 'the importer ran once, for the first cook');
    });

    test('changing the source recooks it', () async {
      await cookFor().cookOne(id('a.txt'));
      source = MemoryAssetSource({id('a.txt'): utf8.encode('changed')});
      final result = await cookFor().cookOne(id('a.txt'));
      expect(result.status, CookStatus.cooked);
      expect(await textOf(cache, result, 'out'), 'changed');
    });

    test('changing the target recooks it', () async {
      await cookFor(
        target: const CookTarget(name: 'macos'),
      ).cookOne(id('a.txt'));
      final result = await cookFor(
        target: const CookTarget(name: 'android'),
      ).cookOne(id('a.txt'));
      expect(result.status, CookStatus.cooked);
    });

    test('bumping the importer version recooks it', () async {
      await cookFor(with_: RecordingImporter()).cookOne(id('a.txt'));
      final result = await cookFor(
        with_: RecordingImporter(version: 2),
      ).cookOne(id('a.txt'));
      expect(result.status, CookStatus.cooked);
    });

    test('an unclaimed asset is skipped, with no key and no outputs', () async {
      final result = await cookFor().cookOne(id('README.md'));
      expect(result.status, CookStatus.skipped);
      expect(result.key, isNull);
      expect(result.asset, isNull);
      expect(importer.imported, isEmpty);
    });

    group('settings', () {
      test('are resolved before the key, so a default counts', () async {
        final result = await cookFor().cookOne(id('a.txt'));
        expect(result.key!.settings, {'mode': 'normal'});
      });

      test('come from the asset\'s own .import.json', () async {
        source = MemoryAssetSource({
          id('a.txt'): utf8.encode('alpha'),
          id('a.txt.import.json'): utf8.encode(
            '{"settings": {"mode": "special"}}',
          ),
        });
        final result = await cookFor().cookOne(id('a.txt'));
        expect(result.key!.settings, {'mode': 'special'});
      });

      test(
        'come from a folder too, and the file wins over the folder',
        () async {
          source = MemoryAssetSource({
            id('art/a.txt'): utf8.encode('alpha'),
            id('art/b.txt'): utf8.encode('beta'),
            id('art/.import.json'): utf8.encode(
              '{"settings": {"mode": "folder"}}',
            ),
            id('art/b.txt.import.json'): utf8.encode(
              '{"settings": {"mode": "file"}}',
            ),
          });
          final cook = cookFor();
          expect((await cook.cookOne(id('art/a.txt'))).key!.settings, {
            'mode': 'folder',
          });
          expect((await cook.cookOne(id('art/b.txt'))).key!.settings, {
            'mode': 'file',
          });
        },
      );

      test('changing one recooks the asset it applies to', () async {
        source = MemoryAssetSource({id('a.txt'): utf8.encode('alpha')});
        await cookFor().cookOne(id('a.txt'));
        source = MemoryAssetSource({
          id('a.txt'): utf8.encode('alpha'),
          id('a.txt.import.json'): utf8.encode(
            '{"settings": {"mode": "special"}}',
          ),
        });
        expect(
          (await cookFor().cookOne(id('a.txt'))).status,
          CookStatus.cooked,
        );
      });

      test(
        'a setting the importer does not understand changes nothing',
        () async {
          source = MemoryAssetSource({id('a.txt'): utf8.encode('alpha')});
          await cookFor().cookOne(id('a.txt'));
          source = MemoryAssetSource({
            id('a.txt'): utf8.encode('alpha'),
            id('a.txt.import.json'): utf8.encode(
              '{"settings": {"nonsense": 7}}',
            ),
          });
          expect(
            (await cookFor().cookOne(id('a.txt'))).status,
            CookStatus.cached,
          );
        },
      );

      test(
        'a malformed .import.json fails that asset, naming the file',
        () async {
          source = MemoryAssetSource({
            id('a.txt'): utf8.encode('alpha'),
            id('a.txt.import.json'): utf8.encode('{not json'),
          });
          final result = await cookFor().cookOne(id('a.txt'));
          expect(result.status, CookStatus.failed);
          expect(result.error.toString(), contains('a.txt.import.json'));
        },
      );
    });

    group('dependencies', () {
      setUp(() {
        source = MemoryAssetSource({
          id('scene.txt'): utf8.encode('scene'),
          id('a.txt'): utf8.encode('alpha'),
          id('b.txt'): utf8.encode('beta'),
        });
        importer = RecordingImporter(
          discovers: {
            id('scene.txt'): {id('a.txt'), id('b.txt')},
          },
        );
      });

      test('go into the key and come back in the result', () async {
        final result = await cookFor().cookOne(id('scene.txt'));
        expect(result.dependencies, {id('a.txt'), id('b.txt')});
        expect(result.key!.dependencies.keys, {id('a.txt'), id('b.txt')});
      });

      test('changing one recooks what depends on it', () async {
        await cookFor().cookOne(id('scene.txt'));
        source = MemoryAssetSource({
          id('scene.txt'): utf8.encode('scene'),
          id('a.txt'): utf8.encode('ALPHA'),
          id('b.txt'): utf8.encode('beta'),
        });
        expect(
          (await cookFor().cookOne(id('scene.txt'))).status,
          CookStatus.cooked,
        );
      });

      test('changing one recooks only what depends on it', () async {
        final cook = cookFor();
        await cook.cookAll([id('scene.txt'), id('a.txt'), id('b.txt')]);
        importer.imported.clear();

        source = MemoryAssetSource({
          id('scene.txt'): utf8.encode('scene'),
          id('a.txt'): utf8.encode('ALPHA'),
          id('b.txt'): utf8.encode('beta'),
        });
        final report = await cookFor().cookAll([
          id('scene.txt'),
          id('a.txt'),
          id('b.txt'),
        ]);

        expect(importer.imported.toSet(), {
          id('scene.txt'),
          id('a.txt'),
        }, reason: 'b.txt did not change and nothing it feeds changed');
        expect(report.count(CookStatus.cached), 1);
      });

      test('are asked for before the cache is consulted, and only once '
          'per source file', () async {
        final cook = cookFor();
        await cook.cookAll([id('scene.txt'), id('a.txt')]);
        expect(importer.asked.length, 2);
      });

      test('a missing one fails the asset that named it', () async {
        importer = RecordingImporter(
          discovers: {
            id('scene.txt'): {id('gone.txt')},
          },
        );
        final result = await cookFor().cookOne(id('scene.txt'));
        expect(result.status, CookStatus.failed);
        expect(result.error, isA<AssetNotFound>());
      });

      test(
        'the report collects every dependency, for a build to watch',
        () async {
          final report = await cookFor().cookAll([
            id('scene.txt'),
            id('a.txt'),
          ]);
          expect(report.dependencies, {id('a.txt'), id('b.txt')});
        },
      );
    });

    group('stateOf', () {
      test('an asset nothing claims is ignored', () async {
        expect(await cookFor().stateOf(id('README.md')), CookState.ignored);
      });

      test('an asset that has never been cooked is stale', () async {
        expect(await cookFor().stateOf(id('a.txt')), CookState.stale);
      });

      test('asking does not cook it', () async {
        await cookFor().stateOf(id('a.txt'));
        expect(
          importer.imported,
          isEmpty,
          reason: 'the whole point is that an editor can ask a thousand times',
        );
        expect(
          await cache.lookUp(
            CookKey(
              importer: importer.name,
              importerVersion: importer.version,
              source: ContentHash.of(utf8.encode('alpha')),
              dependencies: const {},
              settings: const {'mode': 'normal'},
              target: CookTarget.any.recipe,
            ),
          ),
          isNull,
        );
      });

      test('one that has just been cooked is cooked', () async {
        await cookFor().cookOne(id('a.txt'));
        expect(await cookFor().stateOf(id('a.txt')), CookState.cooked);
      });

      test('changing the source makes it stale again', () async {
        await cookFor().cookOne(id('a.txt'));
        source = MemoryAssetSource({id('a.txt'): utf8.encode('changed')});
        expect(await cookFor().stateOf(id('a.txt')), CookState.stale);
      });

      test('changing something it depends on makes it stale', () async {
        final discovers = {
          id('a.txt'): {id('b.txt')},
        };
        source = MemoryAssetSource({
          id('a.txt'): utf8.encode('alpha'),
          id('b.txt'): utf8.encode('beta'),
        });
        await cookFor(
          with_: RecordingImporter(discovers: discovers),
        ).cookOne(id('a.txt'));

        source = MemoryAssetSource({
          id('a.txt'): utf8.encode('alpha'),
          id('b.txt'): utf8.encode('changed'),
        });
        expect(
          await cookFor(
            with_: RecordingImporter(discovers: discovers),
          ).stateOf(id('a.txt')),
          CookState.stale,
        );
      });

      test('changing its settings makes it stale', () async {
        await cookFor().cookOne(id('a.txt'));
        source = MemoryAssetSource({
          id('a.txt'): utf8.encode('alpha'),
          id('a.txt.import.json'): utf8.encode(
            '{"settings": {"mode": "special"}}',
          ),
        });
        expect(await cookFor().stateOf(id('a.txt')), CookState.stale);
      });

      test('cooked for one target is still stale for another', () async {
        await cookFor(
          target: const CookTarget(name: 'macos'),
        ).cookOne(id('a.txt'));
        expect(
          await cookFor(
            target: const CookTarget(name: 'web'),
          ).stateOf(id('a.txt')),
          CookState.stale,
        );
      });

      test('an asset that is not there is failed', () async {
        expect(await cookFor().stateOf(id('gone.txt')), CookState.failed);
      });

      test('a dependency that is not there is failed', () async {
        expect(
          await cookFor(
            with_: RecordingImporter(
              discovers: {
                id('a.txt'): {id('missing.txt')},
              },
            ),
          ).stateOf(id('a.txt')),
          CookState.failed,
        );
      });

      test('an importer that cannot read the asset is failed', () async {
        final cook = Cook(
          source: source,
          cache: cache,
          importers: ImporterRegistry([_UnreadableImporter()]),
          target: CookTarget.any,
        );
        expect(await cook.stateOf(id('a.txt')), CookState.failed);
      });

      test('an asset a cook could not cook is failed afterwards', () async {
        final broken = RecordingImporter(
          transform: (request) => throw ImportFailure(request.id, 'no good'),
        );
        expect(
          await cookFor(with_: broken).stateOf(id('a.txt')),
          CookState.stale,
          reason: 'nothing has tried yet, and nobody can know without trying',
        );

        await cookFor(with_: broken).cookOne(id('a.txt'));
        expect(
          await cookFor(with_: broken).stateOf(id('a.txt')),
          CookState.failed,
        );
      });

      test('a remembered failure does not stop a cook trying again', () async {
        var attempts = 0;
        final flaky = RecordingImporter(
          transform: (request) {
            attempts++;
            if (attempts == 1) throw ImportFailure(request.id, 'not installed');
            return request.bytes;
          },
        );
        expect(
          (await cookFor(with_: flaky).cookOne(id('a.txt'))).status,
          CookStatus.failed,
        );
        final again = await cookFor(with_: flaky).cookOne(id('a.txt'));
        expect(
          again.status,
          CookStatus.cooked,
          reason: 'importers fail for reasons that are not in the key',
        );
        expect(
          await cookFor(with_: flaky).stateOf(id('a.txt')),
          CookState.cooked,
        );
      });

      test('changing the asset clears the failure', () async {
        final broken = RecordingImporter(
          transform: (request) => throw ImportFailure(request.id, 'no good'),
        );
        await cookFor(with_: broken).cookOne(id('a.txt'));
        source = MemoryAssetSource({id('a.txt'): utf8.encode('fixed')});
        expect(
          await cookFor(with_: broken).stateOf(id('a.txt')),
          CookState.stale,
          reason: 'a different asset is different work, not the failed work',
        );
      });

      test('says the same thing the cook goes on to do', () async {
        final cook = cookFor();
        expect(await cook.stateOf(id('a.txt')), CookState.stale);
        expect(
          (await cookFor().cookOne(id('a.txt'))).status,
          CookStatus.cooked,
        );
        expect(await cookFor().stateOf(id('a.txt')), CookState.cooked);
        expect(
          (await cookFor().cookOne(id('a.txt'))).status,
          CookStatus.cached,
        );
      });
    });

    group('stateOfAll', () {
      test('answers for every one it is given', () async {
        await cookFor().cookOne(id('a.txt'));
        expect(
          await cookFor().stateOfAll([
            id('a.txt'),
            id('b.txt'),
            id('README.md'),
            id('gone.txt'),
          ]),
          {
            id('a.txt'): CookState.cooked,
            id('b.txt'): CookState.stale,
            id('README.md'): CookState.ignored,
            id('gone.txt'): CookState.failed,
          },
        );
      });

      test('reads a shared dependency once for the whole answer', () async {
        final reads = <AssetId, int>{};
        final bytes = {
          id('a.txt'): utf8.encode('alpha'),
          id('b.txt'): utf8.encode('beta'),
          id('shared.txt'): utf8.encode('shared'),
        };
        final counting = CallbackAssetSource((id) async {
          reads[id] = (reads[id] ?? 0) + 1;
          final found = bytes[id];
          return found == null ? null : Uint8List.fromList(found);
        });

        await Cook(
          source: counting,
          cache: cache,
          importers: ImporterRegistry([
            RecordingImporter(
              discovers: {
                id('a.txt'): {id('shared.txt')},
                id('b.txt'): {id('shared.txt')},
              },
            ),
          ]),
          target: CookTarget.any,
        ).stateOfAll([id('a.txt'), id('b.txt')]);

        expect(reads[id('shared.txt')], 1);
      });

      test('an empty list is an empty answer', () async {
        expect(await cookFor().stateOfAll([]), isEmpty);
      });
    });

    group('cookAll', () {
      test(
        'reports in the order it was given, whatever order they finish in',
        () async {
          final ids = [id('a.txt'), id('README.md'), id('b.txt')];
          final report = await cookFor().cookAll(ids);
          expect([for (final result in report.results) result.id], ids);
        },
      );

      test('calls back as each lands', () async {
        final seen = <AssetId>[];
        await cookFor().cookAll([
          id('a.txt'),
          id('b.txt'),
        ], onResult: (r) => seen.add(r.id));
        expect(seen, unorderedEquals([id('a.txt'), id('b.txt')]));
      });

      test('one broken asset does not stop the rest', () async {
        importer = RecordingImporter(
          transform: (request) {
            if (request.id == id('b.txt')) {
              throw ImportFailure(request.id, 'no good');
            }
            return request.bytes;
          },
        );
        final report = await cookFor().cookAll([id('a.txt'), id('b.txt')]);
        expect(report.ok, isFalse);
        expect(report.count(CookStatus.cooked), 1);
        expect(report.count(CookStatus.failed), 1);
        expect(
          report.withStatus(CookStatus.failed).single.error,
          isA<ImportFailure>(),
        );
      });

      test('summarises what it did', () async {
        final report = await cookFor().cookAll([
          id('a.txt'),
          id('b.txt'),
          id('README.md'),
        ]);
        expect(report.summary, '2 cooked, 1 skipped');
        expect(CookReport(const []).summary, 'nothing to cook');
      });

      test('nothing to do is a clean report, not an empty failure', () async {
        final report = await cookFor().cookAll(const []);
        expect(report.ok, isTrue);
        expect(report.results, isEmpty);
      });

      test('runs at most `concurrency` importers at once', () async {
        var running = 0;
        var peak = 0;
        final gate = <void Function()>[];
        final slow = _GatedImporter(
          onStart: () {
            running++;
            peak = peak > running ? peak : running;
          },
          onFinish: () => running--,
          wait: (release) => gate.add(release),
        );
        final cook = Cook(
          source: MemoryAssetSource({
            for (var i = 0; i < 8; i++) id('$i.txt'): utf8.encode('$i'),
          }),
          cache: MemoryCookCache(),
          importers: ImporterRegistry([slow]),
          target: CookTarget.any,
          concurrency: 3,
        );
        final done = cook.cookAll([for (var i = 0; i < 8; i++) id('$i.txt')]);
        // Let the first batch reach the gate, then release everything.
        await Future<void>.delayed(Duration.zero);
        while (gate.isNotEmpty) {
          gate.removeAt(0)();
          await Future<void>.delayed(Duration.zero);
        }
        await done;
        expect(peak, 3);
      });

      test('a concurrency below one is refused', () {
        expect(
          () => Cook(
            source: source,
            cache: cache,
            importers: ImporterRegistry([importer]),
            target: CookTarget.any,
            concurrency: 0,
          ),
          throwsArgumentError,
        );
      });
    });

    test('an importer sees the resolved settings and the target', () async {
      Map<String, Object?>? seenSettings;
      CookTarget? seenTarget;
      importer = RecordingImporter(
        transform: (request) {
          seenSettings = request.settings;
          seenTarget = request.target;
          return request.bytes;
        },
      );
      const target = CookTarget(
        name: 'web',
        properties: {'maxTextureSize': 2048},
      );
      await cookFor(target: target).cookOne(id('a.txt'));
      expect(seenSettings, {'mode': 'normal'});
      expect(seenTarget!.name, 'web');
      expect(seenTarget!.maxTextureSize, 2048);
    });
  });
}

/// An importer that parks in [import] until it is released, so a test can see
/// how many are running at once.
class _GatedImporter extends Importer {
  _GatedImporter({
    required this.onStart,
    required this.onFinish,
    required this.wait,
  });

  final void Function() onStart;
  final void Function() onFinish;
  final void Function(void Function() release) wait;

  @override
  String get name => 'gated';
  @override
  int get version => 1;
  @override
  Set<String> get extensions => const {'txt'};
  @override
  Map<String, Object?> resolveSettings(ImportSettings settings) => const {};

  @override
  Future<ImportResult> import(ImportRequest request) async {
    onStart();
    final released = Completer<void>();
    wait(released.complete);
    await released.future;
    onFinish();
    return ImportResult(outputs: {'out': request.bytes});
  }
}

/// An importer that claims an asset and then cannot make sense of it, the way
/// a real one meets a `.png` that is not a PNG.
class _UnreadableImporter extends Importer {
  @override
  String get name => 'unreadable';
  @override
  int get version => 1;
  @override
  Set<String> get extensions => const {'txt'};
  @override
  Map<String, Object?> resolveSettings(ImportSettings settings) => const {};

  @override
  Future<Set<AssetId>> dependenciesOf(
    AssetId id,
    Uint8List bytes,
    Map<String, Object?> settings,
  ) async => throw ImportFailure(id, 'not a txt at all');

  @override
  Future<ImportResult> import(ImportRequest request) async =>
      throw ImportFailure(request.id, 'not a txt at all');
}

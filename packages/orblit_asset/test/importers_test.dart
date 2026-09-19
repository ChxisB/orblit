@TestOn('vm')
library;

import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:test/test.dart';

AssetId id(String text) => AssetId.parse(text);

void main() {
  late Directory work;
  late File record;

  /// A [NativeTool] that runs `test/src/fake_tool.dart` with [spec], so a
  /// test can see the flags an importer chose and hand back the files the
  /// real program would have written.
  ///
  /// It runs as a real process, through the real [NativeTool], because the
  /// things worth testing here — a bad exit becoming a useful error, the
  /// output directory being read back — are things an in-process fake would
  /// quietly get right and the real one would not.
  NativeTool fake(String spec) {
    final script = File('test/src/fake_tool.dart').absolute.path;
    final shim = File(inside(work, 'tool.sh'))
      ..writeAsStringSync(
        '#!/bin/sh\nexec "${Platform.resolvedExecutable}" "$script" '
        '"${record.path}" "$spec" "\$@"\n',
      );
    Process.runSync('chmod', ['+x', shim.path]);
    return NativeTool('fake', path: shim.path);
  }

  List<List<String>> runs() {
    if (!record.existsSync()) return const [];
    return [
      for (final run in record.readAsStringSync().trim().split('\n\n'))
        run.split('\n'),
    ];
  }

  List<String> onlyRun() => runs().single;

  ImportRequest requestFor(
    AssetId asset, {
    Map<String, Object?> settings = const {},
    CookTarget target = CookTarget.any,
    List<int>? bytes,
  }) => ImportRequest(
    id: asset,
    bytes: Uint8List.fromList(bytes ?? utf8.encode('source bytes')),
    settings: settings,
    target: target,
    source: MemoryAssetSource(),
  );

  setUp(() {
    work = Directory.systemTemp.createTempSync('orblit_importers_test');
    record = File(inside(work, 'record.txt'));
  });

  tearDown(() => work.deleteSync(recursive: true));

  group('NativeTool', () {
    test('an absent program is an error naming where it looked', () async {
      final missing = NativeTool(
        'not_a_program_anyone_has',
        environmentVariable: 'ORBLIT_NOT_A_PROGRAM',
      );
      expect(
        () => missing.run(const [], on: id('a.png')),
        throwsA(
          isA<ImportFailure>().having(
            (e) => e.reason,
            'reason',
            allOf(contains('not built'), contains('ORBLIT_NOT_A_PROGRAM')),
          ),
        ),
      );
    });

    test('a bad exit carries what the program said, which is the part worth '
        'reading', () async {
      final tool = fake('say=unsupported PNG bit depth;exit=2');
      expect(
        () => tool.run(const ['x'], on: id('a.png')),
        throwsA(
          isA<ImportFailure>()
              .having((e) => e.reason, 'reason', contains('exited 2'))
              .having(
                (e) => e.cause.toString(),
                'cause',
                contains('unsupported PNG bit depth'),
              ),
        ),
      );
    });

    test('a good exit says nothing', () async {
      await fake('').run(const ['x'], on: id('a.png'));
      expect(onlyRun(), ['x']);
    });
  });

  group('TextureImporter', () {
    TextureImporter importerWriting(String files) =>
        TextureImporter(tool: fake(files));

    test('writes only the families the target asks for', () async {
      final importer = importerWriting('writeInto=1:wall.astc.ktx2');
      final result = await importer.import(
        requestFor(
          id('textures/wall.png'),
          settings: importer.resolveSettings(const ImportSettings()),
          target: const CookTarget(
            name: 'ios',
            properties: {
              'textureFamilies': ['astc'],
            },
          ),
        ),
      );
      expect(onlyRun(), containsAllInOrder(['--targets', 'astc']));
      expect(result.outputs.keys, ['wall.astc.ktx2']);
    });

    test('a target that names no families gets all of them, which is what a '
        'project-wide cook wants', () async {
      final importer = importerWriting('writeInto=1:wall.ktx2');
      await importer.import(
        requestFor(
          id('wall.png'),
          settings: importer.resolveSettings(const ImportSettings()),
        ),
      );
      final targets = onlyRun()[onlyRun().indexOf('--targets') + 1];
      expect(targets.split(','), containsAll(['astc', 'bc', 'etc2', 'basis']));
    });

    test('passes the settings through as flags', () async {
      final importer = importerWriting('writeInto=1:n.ktx2');
      await importer.import(
        requestFor(
          id('n.png'),
          settings: importer.resolveSettings(
            const ImportSettings(
              values: {
                'normal': true,
                'twoChannelNormals': true,
                'wrap': true,
                'mips': false,
                'colourSpace': 'linear',
                'cutout': 0.5,
                'maxSize': 1024,
                'uastc': 4,
                'zstd': 22,
              },
            ),
          ),
        ),
      );
      expect(
        onlyRun(),
        containsAll([
          '--normal',
          '--two-channel-normals',
          '--wrap',
          '--no-mips',
          '--linear',
        ]),
      );
      expect(onlyRun(), containsAllInOrder(['--cutout', '0.5']));
      expect(onlyRun(), containsAllInOrder(['--max-size', '1024']));
      expect(onlyRun(), containsAllInOrder(['--uastc', '4']));
      expect(onlyRun(), containsAllInOrder(['--zstd', '22']));
    });

    test('mips are on unless turned off, because a texture without them '
        'shimmers', () async {
      final importer = importerWriting('writeInto=1:a.ktx2');
      await importer.import(
        requestFor(
          id('a.png'),
          settings: importer.resolveSettings(const ImportSettings()),
        ),
      );
      expect(onlyRun(), isNot(contains('--no-mips')));
    });

    test(
      'the target\'s size limit applies when the settings do not set one',
      () async {
        final importer = importerWriting('writeInto=1:a.ktx2');
        final result = await importer.import(
          requestFor(
            id('a.png'),
            settings: importer.resolveSettings(const ImportSettings()),
            target: const CookTarget(
              name: 'web',
              properties: {'maxTextureSize': 2048},
            ),
          ),
        );
        expect(onlyRun(), containsAllInOrder(['--max-size', '2048']));
        expect(result.notes.single, contains('2048'));
      },
    );

    test(
      'a setting beats the target\'s limit, and says nothing about it',
      () async {
        final importer = importerWriting('writeInto=1:a.ktx2');
        final result = await importer.import(
          requestFor(
            id('a.png'),
            settings: importer.resolveSettings(
              const ImportSettings(values: {'maxSize': 512}),
            ),
            target: const CookTarget(
              name: 'web',
              properties: {'maxTextureSize': 2048},
            ),
          ),
        );
        expect(onlyRun(), containsAllInOrder(['--max-size', '512']));
        expect(result.notes, isEmpty);
      },
    );

    test('lossless asks for no family at all', () async {
      final importer = importerWriting('writeInto=1:a.ktx2');
      await importer.import(
        requestFor(
          id('a.png'),
          settings: importer.resolveSettings(
            const ImportSettings(values: {'lossless': true}),
          ),
        ),
      );
      expect(onlyRun(), contains('--lossless'));
      expect(onlyRun(), isNot(contains('--targets')));
    });

    test(
      'a family nobody has heard of is refused before anything runs',
      () async {
        final importer = importerWriting('');
        expect(
          () => importer.import(
            requestFor(
              id('a.png'),
              settings: importer.resolveSettings(const ImportSettings()),
              target: const CookTarget(
                name: 'odd',
                properties: {
                  'textureFamilies': ['astc', 'dxt1'],
                },
              ),
            ),
          ),
          throwsA(
            isA<ImportFailure>().having(
              (e) => e.reason,
              'reason',
              contains('dxt1'),
            ),
          ),
        );
        expect(runs(), isEmpty);
      },
    );

    test(
      'a cook that writes nothing is a failure, not an empty asset',
      () async {
        final importer = importerWriting('');
        expect(
          () => importer.import(
            requestFor(
              id('a.png'),
              settings: importer.resolveSettings(const ImportSettings()),
            ),
          ),
          throwsA(
            isA<ImportFailure>().having(
              (e) => e.reason,
              'reason',
              contains('no files'),
            ),
          ),
        );
      },
    );

    group('settings', () {
      final importer = TextureImporter();

      test('fill in their defaults, so a key covers them', () {
        expect(
          importer.resolveSettings(const ImportSettings()),
          containsPair('mips', true),
        );
        expect(
          importer.resolveSettings(const ImportSettings()),
          containsPair('uastc', 2),
        );
      });

      test('read colorSpace as well as colourSpace, because both spellings '
          'get typed', () {
        expect(
          importer.resolveSettings(
            const ImportSettings(values: {'colorSpace': 'srgb'}),
          ),
          containsPair('colourSpace', 'srgb'),
        );
      });

      test('refuse a maxSize that is not a power of two', () {
        expect(
          () => importer.resolveSettings(
            const ImportSettings(values: {'maxSize': 1000}),
          ),
          throwsFormatException,
        );
      });

      test('refuse a cutout outside zero to one', () {
        expect(
          () => importer.resolveSettings(
            const ImportSettings(values: {'cutout': 2}),
          ),
          throwsFormatException,
        );
      });

      test('refuse a flag that is not true or false', () {
        expect(
          () => importer.resolveSettings(
            const ImportSettings(values: {'normal': 'yes'}),
          ),
          throwsFormatException,
        );
      });

      test('read a whole-number cutout as the fraction it means', () {
        expect(
          importer.resolveSettings(const ImportSettings(values: {'cutout': 1})),
          containsPair('cutout', 1.0),
        );
      });
    });
  });

  group('ModelImporter', () {
    test('converts to a GLB', () async {
      final importer = ModelImporter(tool: fake('writeArg=1'));
      final result = await importer.import(requestFor(id('chair.fbx')));
      expect(result.outputs.keys, ['glb']);
      expect(onlyRun().first, endsWith('in.fbx'));
      expect(onlyRun().last, endsWith('out.glb'));
    });

    test('claims the formats artists actually hand over', () {
      final importer = ModelImporter();
      expect(importer.handles(id('a.fbx')), isTrue);
      expect(importer.handles(id('a.obj')), isTrue);
      expect(importer.handles(id('a.glb')), isFalse);
    });

    test('a conversion that writes nothing is a failure that says why that '
        'happens', () async {
      final importer = ModelImporter(tool: fake(''));
      expect(
        () => importer.import(requestFor(id('empty.fbx'))),
        throwsA(
          isA<ImportFailure>().having(
            (e) => e.reason,
            'reason',
            contains('no GLB'),
          ),
        ),
      );
    });
  });

  group('SplatImporter', () {
    test(
      'cooks to .osplat, keeping every band unless told otherwise',
      () async {
        final importer = SplatImporter(tool: fake('writeArg=1'));
        final result = await importer.import(
          requestFor(
            id('capture.ply'),
            settings: importer.resolveSettings(const ImportSettings()),
          ),
        );
        expect(result.outputs.keys, ['osplat']);
        expect(onlyRun(), containsAllInOrder(['--harmonics', '3']));
        expect(onlyRun(), isNot(contains('--limit')));
      },
    );

    test(
      'a limit is passed on, and is how a phone gets the small file',
      () async {
        final importer = SplatImporter(tool: fake('writeArg=1'));
        await importer.import(
          requestFor(
            id('capture.ply'),
            settings: importer.resolveSettings(
              const ImportSettings(values: {'harmonics': 1, 'limit': 200000}),
            ),
          ),
        );
        expect(onlyRun(), containsAllInOrder(['--harmonics', '1']));
        expect(onlyRun(), containsAllInOrder(['--limit', '200000']));
      },
    );

    test('refuses a harmonics degree the format does not have', () {
      expect(
        () => SplatImporter().resolveSettings(
          const ImportSettings(values: {'harmonics': 4}),
        ),
        throwsFormatException,
      );
    });

    test('refuses a limit of nothing, which would cook an empty cloud', () {
      expect(
        () => SplatImporter().resolveSettings(
          const ImportSettings(values: {'limit': 0}),
        ),
        throwsFormatException,
      );
    });
  });

  group('EnvironmentImporter', () {
    test(
      'bakes the reflections and the backdrop, and keeps the harmonics',
      () async {
        final importer = EnvironmentImporter(
          tool: fake(
            'writeAfter=--deploy:environment_ibl.ktx;'
            'writeAfter=--deploy:sh.txt;'
            'writeAfter=--extract:environment_skybox.ktx',
          ),
        );
        final result = await importer.import(
          requestFor(
            id('sky/dusk.hdr'),
            settings: importer.resolveSettings(const ImportSettings()),
          ),
        );
        expect(
          result.outputs.keys,
          unorderedEquals(['ibl.ktx', 'skybox.ktx', 'sh.txt']),
        );
      },
    );

    test('the backdrop is four times the reflections, held to 1024', () {
      final importer = EnvironmentImporter();
      expect(importer.resolveSettings(const ImportSettings()), {
        'size': 256,
        'skyboxSize': 1024,
      });
      expect(
        importer.resolveSettings(const ImportSettings(values: {'size': 64})),
        {'size': 64, 'skyboxSize': 256},
      );
    });

    test('refuses a face size that is not a power of two', () {
      expect(
        () => EnvironmentImporter().resolveSettings(
          const ImportSettings(values: {'size': 300}),
        ),
        throwsFormatException,
      );
    });

    test('a cmgen that names its outputs differently is a failure that says '
        'so', () async {
      final importer = EnvironmentImporter(tool: fake(''));
      expect(
        () => importer.import(
          requestFor(
            id('sky.hdr'),
            settings: importer.resolveSettings(const ImportSettings()),
          ),
        ),
        throwsA(
          isA<ImportFailure>().having(
            (e) => e.reason,
            'reason',
            contains('different Filament version'),
          ),
        ),
      );
    });
  });

  test('the native importers are registered under distinct names', () {
    final registry = ImporterRegistry(nativeImporters);
    expect(registry.importers.length, nativeImporters.length);
    expect(registry.forAsset(id('a.png')), isA<TextureImporter>());
    expect(registry.forAsset(id('a.fbx')), isA<ModelImporter>());
    expect(registry.forAsset(id('a.ply')), isA<SplatImporter>());
    expect(registry.forAsset(id('a.hdr')), isA<EnvironmentImporter>());
  });
}

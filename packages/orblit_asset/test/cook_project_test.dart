@TestOn('vm')
library;

import 'dart:convert';
import 'dart:io';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:test/test.dart';

AssetId id(String text) => AssetId.parse(text);

/// An importer that writes one file per texture family the target asked for,
/// so a test can see which families a bundle ends up carrying without
/// building the real encoder.
class FamilyImporter extends Importer {
  const FamilyImporter();

  @override
  String get name => 'family';

  @override
  int get version => 1;

  @override
  Set<String> get extensions => const {'png'};

  @override
  Map<String, Object?> resolveSettings(ImportSettings settings) => const {};

  @override
  Future<ImportResult> import(ImportRequest request) async {
    final families = request.target.textureFamilies;
    return ImportResult(
      outputs: {
        for (final family in families)
          '$family.ktx2': [
            // Derived from the source, the way a real encoder's output is, so
            // that a changed asset cooks to changed bytes.
            ...utf8.encode('$family of ${request.id}\n'),
            ...request.bytes,
          ],
      },
    );
  }
}

void main() {
  late Directory work;

  String at(String name) => beside(work.path, name);

  void writeAsset(String name, String contents) {
    final file = File(
      at(
        'assets${Platform.pathSeparator}'
        '${name.replaceAll('/', Platform.pathSeparator)}',
      ),
    );
    file.parent.createSync(recursive: true);
    file.writeAsStringSync(contents);
  }

  CookProject projectFor(CookTarget target, {ImporterRegistry? importers}) =>
      CookProject(
        assetsPath: at('assets'),
        outPath: at('out'),
        target: target,
        cachePath: at('cache'),
        importers: importers ?? ImporterRegistry([const FamilyImporter()]),
      );

  setUp(() {
    work = Directory.systemTemp.createTempSync('orblit_cook_project_test');
  });

  tearDown(() => work.deleteSync(recursive: true));

  group('assets()', () {
    test('finds everything under the folder, in a fixed order', () async {
      writeAsset('b.png', 'b');
      writeAsset('a.png', 'a');
      writeAsset('nested/deep/c.png', 'c');
      expect(await projectFor(CookTargets.macos).assets(), [
        id('a.png'),
        id('b.png'),
        id('nested/deep/c.png'),
      ]);
    });

    test(
      'leaves out settings files, which are inputs and not assets',
      () async {
        writeAsset('a.png', 'a');
        writeAsset('a.png.import.json', '{}');
        writeAsset('.import.json', '{}');
        expect(await projectFor(CookTargets.macos).assets(), [id('a.png')]);
      },
    );

    test('leaves out hidden files and folders', () async {
      writeAsset('a.png', 'a');
      writeAsset('.DS_Store', 'junk');
      writeAsset('.git/objects/thing', 'junk');
      expect(await projectFor(CookTargets.macos).assets(), [id('a.png')]);
    });

    test('a missing assets folder is an error that names it', () {
      expect(
        () => projectFor(CookTargets.macos).assets(),
        throwsA(
          isA<ArgumentError>().having((e) => e.name, 'name', 'assetsPath'),
        ),
      );
    });
  });

  group('a cooked bundle', () {
    setUp(() {
      writeAsset('textures/wall.png', 'wall');
      writeAsset('README.md', 'not an asset');
    });

    test('carries only its own target\'s formats', () async {
      await projectFor(CookTargets.ios).run();
      await projectFor(CookTargets.macos).run();

      final ios = Directory(at('out${Platform.pathSeparator}ios'))
          .listSync(recursive: true)
          .whereType<File>()
          .map((file) => file.path.split(Platform.pathSeparator).last)
          .toList();
      final macos = Directory(at('out${Platform.pathSeparator}macos'))
          .listSync(recursive: true)
          .whereType<File>()
          .map((file) => file.path.split(Platform.pathSeparator).last)
          .toList();

      expect(ios, contains('wall.png.astc.ktx2'));
      expect(ios, isNot(contains('wall.png.bc.ktx2')));
      expect(macos, contains('wall.png.bc.ktx2'));
      expect(macos, isNot(contains('wall.png.astc.ktx2')));
    });

    test('keeps the source name in the middle, so a file says where it came '
        'from', () async {
      await projectFor(CookTargets.macos).run();
      expect(
        File(
          at(
            'out/macos/textures/wall.png.bc.ktx2'.replaceAll(
              '/',
              Platform.pathSeparator,
            ),
          ),
        ).existsSync(),
        isTrue,
      );
    });

    test('has a manifest naming every file it holds', () async {
      await projectFor(CookTargets.macos).run();
      final manifest = AssetManifest.read(
        File(
          at(
            'out${Platform.pathSeparator}macos'
            '${Platform.pathSeparator}manifest.json',
          ),
        ).readAsStringSync(),
      ).manifest;
      expect(manifest.entries.keys, [id('textures/wall.png.bc.ktx2')]);
      expect(manifest.entries.values.single.bytes, greaterThan(0));
    });

    test('does not carry an asset nobody claimed', () async {
      await projectFor(CookTargets.macos).run();
      expect(File(at('out/macos/README.md')).existsSync(), isFalse);
    });

    test('loses yesterday\'s files rather than shipping them', () async {
      await projectFor(CookTargets.macos).run();
      final stale = File(
        at(
          'out${Platform.pathSeparator}macos'
          '${Platform.pathSeparator}gone.ktx2',
        ),
      )..writeAsStringSync('from a cook that no longer applies');
      expect(stale.existsSync(), isTrue);

      await projectFor(CookTargets.macos).run();
      expect(stale.existsSync(), isFalse);
    });
  });

  group('the claims a build depends on', () {
    setUp(() {
      writeAsset('a.png', 'a');
      writeAsset('b.png', 'b');
      writeAsset('c.png', 'c');
    });

    test('a rebuild with nothing changed cooks nothing', () async {
      final first = await projectFor(CookTargets.macos).run();
      expect(first.count(CookStatus.cooked), 3);

      final second = await projectFor(CookTargets.macos).run();
      expect(second.count(CookStatus.cooked), 0);
      expect(second.count(CookStatus.cached), 3);
    });

    test('changing one asset recooks only that one', () async {
      await projectFor(CookTargets.macos).run();
      writeAsset('b.png', 'b changed');

      final report = await projectFor(CookTargets.macos).run();
      expect(
        [for (final result in report.withStatus(CookStatus.cooked)) result.id],
        [id('b.png')],
      );
      expect(report.count(CookStatus.cached), 2);
    });

    test(
      'a clean cook produces the same hashes as the one before it',
      () async {
        final first = await projectFor(CookTargets.macos).run();
        final firstManifest = _manifestOf(at('out'), 'macos');

        // A clean cook: no cache, no bundle, nothing carried over.
        Directory(at('cache')).deleteSync(recursive: true);
        Directory(at('out')).deleteSync(recursive: true);

        final second = await projectFor(CookTargets.macos).run();
        final secondManifest = _manifestOf(at('out'), 'macos');

        expect(second.count(CookStatus.cooked), first.count(CookStatus.cooked));
        expect(bundleHash(secondManifest), bundleHash(firstManifest));

        // And the other half of the claim, without which the first half would
        // hold for a hash that never changes at all.
        writeAsset('b.png', 'b changed');
        await projectFor(CookTargets.macos).run();
        expect(
          bundleHash(_manifestOf(at('out'), 'macos')),
          isNot(bundleHash(firstManifest)),
        );
      },
    );

    test(
      'two targets cooked in the same run do not disturb each other',
      () async {
        await projectFor(CookTargets.ios).run();
        await projectFor(CookTargets.macos).run();
        await projectFor(CookTargets.ios).run();

        final ios = _manifestOf(at('out'), 'ios').entries.keys;
        final macos = _manifestOf(at('out'), 'macos').entries.keys;
        expect(ios, hasLength(3));
        expect(macos, hasLength(3));
        expect(ios.map((id) => '$id'), everyElement(contains('astc')));
        expect(macos.map((id) => '$id'), everyElement(contains('bc')));
      },
    );

    test('a failing asset does not stop the bundle, and is reported', () async {
      final report = await projectFor(
        CookTargets.macos,
        importers: ImporterRegistry([const _BrokenImporter()]),
      ).run();
      expect(report.ok, isFalse);
      expect(report.count(CookStatus.failed), 3);
      expect(
        File(
          at(
            'out${Platform.pathSeparator}macos'
            '${Platform.pathSeparator}manifest.json',
          ),
        ).existsSync(),
        isTrue,
        reason: 'the bundle is still written, without what failed',
      );
    });
  });

  group('cooking during a build', () {
    setUp(() {
      writeAsset('a.png', 'a');
      writeAsset('a.png.import.json', '{}');
    });

    Future<CookReport> cookIn(
      String? targetOs, {
      void Function(Iterable<Uri>)? dependencies,
      ImporterRegistry? importers,
    }) => cookDuringBuild(
      packageRoot: work.path,
      targetOs: targetOs,
      cachePath: at('cache'),
      out: 'out/cooked',
      importers: importers ?? ImporterRegistry([const FamilyImporter()]),
      dependencies: dependencies,
    );

    test(
      'cooks for the platform the build names, as the hook spells it',
      () async {
        await cookIn('ios');
        expect(
          File(
            at(
              'out/cooked/ios/a.png.astc.ktx2'.replaceAll(
                '/',
                Platform.pathSeparator,
              ),
            ),
          ).existsSync(),
          isTrue,
        );
      },
    );

    test('a build with no platform is a web build, which is the one that has '
        'none', () async {
      await cookIn(null);
      expect(
        Directory(
          at('out/cooked/web'.replaceAll('/', Platform.pathSeparator)),
        ).existsSync(),
        isTrue,
      );
    });

    test('refuses a platform Orblit does not cook for', () {
      expect(
        () => cookIn('fuchsia'),
        throwsA(
          isA<ArgumentError>()
              .having((e) => e.name, 'name', 'targetOs')
              .having((e) => '${e.message}', 'message', contains('android')),
        ),
      );
    });

    test('tells the build every file it read, so an edit re-runs it', () async {
      Iterable<Uri>? watched;
      await cookIn('macos', dependencies: (files) => watched = files);
      final paths = watched!.map((uri) => uri.toFilePath()).toSet();
      expect(paths, contains(at('assets${Platform.pathSeparator}a.png')));
      expect(
        paths,
        contains(at('assets${Platform.pathSeparator}a.png.import.json')),
        reason: 'changing how an asset is cooked has to recook it too',
      );
    });

    test('watches a settings file that does not exist yet, which is how one '
        'being added is noticed', () async {
      writeAsset('b.png', 'b');
      Iterable<Uri>? watched;
      await cookIn('macos', dependencies: (files) => watched = files);
      expect(
        watched!.map((uri) => uri.toFilePath()),
        contains(at('assets${Platform.pathSeparator}b.png.import.json')),
      );
      expect(
        File(
          at('assets${Platform.pathSeparator}b.png.import.json'),
        ).existsSync(),
        isFalse,
      );
    });

    test(
      'fails the build rather than shipping an app missing an asset',
      () async {
        await expectLater(
          cookIn(
            'macos',
            importers: ImporterRegistry([const _BrokenImporter()]),
          ),
          throwsA(
            isA<CookFailed>().having(
              (e) => e.failures.map((f) => '${f.id}'),
              'failures',
              contains('a.png'),
            ),
          ),
        );
      },
    );
  });

  group('the command', () {
    Future<ProcessResult> cook(List<String> arguments) => Process.run(
      Platform.resolvedExecutable,
      ['run', 'bin/cook.dart', ...arguments],
      workingDirectory: Directory.current.path,
    );

    test(
      'refuses to run without a target, and says what the targets are',
      () async {
        final result = await cook(['--assets', at('assets')]);
        expect(result.exitCode, 2);
        expect(result.stderr, contains('macos'));
      },
    );

    test('refuses a target nobody has', () async {
      final result = await cook(['--target', 'playstation']);
      expect(result.exitCode, 2);
      expect(result.stderr, contains('playstation'));
    });

    test('refuses two options that ask for opposite things', () async {
      final result = await cook(['--target', 'macos', '--quiet', '--verbose']);
      expect(result.exitCode, 2);
    });

    test('refuses an option it does not have', () async {
      final result = await cook(['--target', 'macos', '--fast']);
      expect(result.exitCode, 2);
      expect(result.stderr, contains('--fast'));
    });

    test('explains itself', () async {
      final result = await cook(['--help']);
      expect(result.exitCode, 0);
      expect(result.stdout, contains('--target'));
    });

    test('cooks a project and leaves 0 behind it', () async {
      writeAsset('a.gltf', jsonEncode({'images': <Object>[]}));
      final result = await cook([
        '--target',
        'macos',
        '--assets',
        at('assets'),
        '--out',
        at('out'),
        '--cache',
        at('cache'),
      ]);
      expect(result.exitCode, 0, reason: '${result.stderr}');
      expect(result.stdout, contains('macos:'));
      expect(
        File(
          at(
            'out${Platform.pathSeparator}macos'
            '${Platform.pathSeparator}a.gltf.gltf',
          ),
        ).existsSync(),
        isTrue,
      );
    });

    test('leaves 1 behind it when an asset failed', () async {
      writeAsset('broken.gltf', 'not json at all');
      final result = await cook([
        '--target',
        'macos',
        '--assets',
        at('assets'),
        '--out',
        at('out'),
        '--cache',
        at('cache'),
      ]);
      expect(result.exitCode, 1);
      expect(result.stderr, contains('broken.gltf'));
      expect(result.stderr, contains('1 asset failed'));
    });
  });
}

AssetManifest _manifestOf(String outPath, String target) => AssetManifest.read(
  File(beside(beside(outPath, target), 'manifest.json')).readAsStringSync(),
).manifest;

class _BrokenImporter extends Importer {
  const _BrokenImporter();

  @override
  String get name => 'broken';

  @override
  int get version => 1;

  @override
  Set<String> get extensions => const {'png'};

  @override
  Map<String, Object?> resolveSettings(ImportSettings settings) => const {};

  @override
  Future<ImportResult> import(ImportRequest request) =>
      throw ImportFailure(request.id, 'this importer never works');
}

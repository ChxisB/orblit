@TestOn('vm')
library;

import 'dart:io';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:test/test.dart';

// Where a built Flutter app keeps its assets, worked out from the path of the
// program. The layouts were read from apps built on each platform. These
// tests check the arithmetic over them, and a build of an app on the platform
// is what checks the layouts themselves.

String? directoryFor(String platform, String executable) =>
    flutterAssetsDirectory(executable: executable, platform: platform);

void main() {
  group('flutterAssetsDirectory', () {
    test('macOS keeps assets in the App framework inside the bundle', () {
      expect(
        directoryFor('macos', '/Applications/Game.app/Contents/MacOS/Game'),
        '/Applications/Game.app/Contents/Frameworks/App.framework/'
        'Resources/flutter_assets',
      );
    });

    test('iOS keeps them in the App framework beside the executable', () {
      expect(
        directoryFor(
          'ios',
          '/var/containers/Bundle/Application/ABC/Runner.app/Runner',
        ),
        '/var/containers/Bundle/Application/ABC/Runner.app/Frameworks/'
        'App.framework/flutter_assets',
      );
    });

    test('Linux keeps them in data beside the executable', () {
      expect(
        directoryFor('linux', '/opt/game/bundle/game'),
        '/opt/game/bundle/data/flutter_assets',
      );
    });

    test('Windows is the same with its own separator', () {
      expect(
        directoryFor('windows', r'C:\Games\Game\game.exe'),
        r'C:\Games\Game\data\flutter_assets',
      );
    });

    test('Android keeps them in the APK, which has no path', () {
      expect(directoryFor('android', '/system/bin/app_process64'), isNull);
    });

    test('a platform it does not know has no answer', () {
      expect(directoryFor('fuchsia', '/pkg/bin/app'), isNull);
    });

    test('a macOS program that is not inside an app bundle has no '
        'answer', () {
      expect(directoryFor('macos', '/usr/local/bin/dart'), isNull);
      expect(directoryFor('macos', 'dart'), isNull);
    });
  });

  group('bundledPackBytes', () {
    test('finds nothing when the program is not a built Flutter app', () {
      // The test runner is the Dart VM, so whatever machine this runs on, the
      // layout it is in is not an app's.
      expect(bundledPackBytes('assets/cooked/game.macos.opack'), isNull);
    });
  });

  group('a pack opened from the layout it was shipped in', () {
    late Directory work;

    setUp(() {
      work = Directory.systemTemp.createTempSync('orblit_bundled_pack');
    });

    tearDown(() => work.deleteSync(recursive: true));

    test('reads through FilePackBytes at the path the layout gives', () async {
      final bundle = Directory('${work.path}/bundle/macos')
        ..createSync(recursive: true);
      File('${bundle.path}/hello.txt').writeAsStringSync('hello');
      final manifest = AssetManifest(
        entries: {
          AssetId.parse('hello.txt'): AssetEntry(
            ContentHash.of('hello'.codeUnits),
            5,
          ),
        },
      );
      File('${bundle.path}/manifest.json').writeAsStringSync(manifest.encode());

      final app = '${work.path}/Game.app';
      final assets = flutterAssetsDirectory(
        executable: '$app/Contents/MacOS/Game',
        platform: 'macos',
      )!;
      final packFile = File('$assets/assets/cooked/game.macos.opack')
        ..parent.createSync(recursive: true);
      await packBundle(
        bundlePath: bundle.path,
        outPath: packFile.path,
        target: CookTargets.macos,
      );

      final pack = await AssetPack.open(FilePackBytes(packFile.path));
      expect(
        String.fromCharCodes(await pack.read(AssetId.parse('hello.txt'))),
        'hello',
      );
    });
  });
}

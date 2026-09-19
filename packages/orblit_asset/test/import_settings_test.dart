import 'dart:convert';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:test/test.dart';

AssetId _id(String text) => AssetId.parse(text);

MemoryAssetSource _project(Map<String, String> files) => MemoryAssetSource({
  for (final file in files.entries) _id(file.key): utf8.encode(file.value),
});

String _settings({String? importer, Map<String, Object?> values = const {}}) =>
    jsonEncode({
      'formatVersion': 1,
      if (importer != null) 'importer': importer,
      'settings': values,
    });

void main() {
  group('which files can have a say', () {
    test('every folder above the asset, then the asset itself', () {
      expect(
        ImportSettingsReader.filesFor(
          _id('textures/wood/wall.png'),
        ).map((id) => id.toString()),
        [
          '.import.json',
          'textures/.import.json',
          'textures/wood/.import.json',
          'textures/wood/wall.png.import.json',
        ],
      );
    });

    test('an asset at the root has the project file and its own', () {
      expect(
        ImportSettingsReader.filesFor(
          _id('wall.png'),
        ).map((id) => id.toString()),
        ['.import.json', 'wall.png.import.json'],
      );
    });

    test('the suffix goes on the whole name, so two kinds do not collide', () {
      // wall.png and wall.jpg in one folder want settings of their own.
      expect(
        ImportSettings.fileFor(_id('t/wall.png')).toString(),
        't/wall.png.import.json',
      );
      expect(
        ImportSettings.fileFor(_id('t/wall.jpg')).toString(),
        't/wall.jpg.import.json',
      );
    });

    test('a folder file is named for its folder', () {
      expect(ImportSettings.folderFileFor('').toString(), '.import.json');
      expect(
        ImportSettings.folderFileFor('textures/wood').toString(),
        'textures/wood/.import.json',
      );
    });
  });

  group('telling settings files from assets', () {
    test('a settings file is not something to cook', () {
      expect(ImportSettings.isSettingsFile(_id('.import.json')), isTrue);
      expect(ImportSettings.isSettingsFile(_id('t/.import.json')), isTrue);
      expect(
        ImportSettings.isSettingsFile(_id('t/wall.png.import.json')),
        isTrue,
      );
      expect(ImportSettings.isSettingsFile(_id('t/wall.png')), isFalse);
      expect(ImportSettings.isSettingsFile(_id('t/import.json')), isFalse);
    });

    test('a per-asset file says which asset it belongs to', () {
      expect(
        ImportSettings.assetFor(_id('t/wall.png.import.json')).toString(),
        't/wall.png',
      );
    });

    test('a folder file belongs to no one asset', () {
      expect(ImportSettings.assetFor(_id('.import.json')), isNull);
      expect(ImportSettings.assetFor(_id('t/.import.json')), isNull);
    });

    test('something that is not a settings file belongs to nothing', () {
      expect(ImportSettings.assetFor(_id('t/wall.png')), isNull);
    });
  });

  group('what applies to an asset', () {
    test('nothing at all when the project says nothing', () async {
      final read = ImportSettingsReader(_project({}));
      final settings = await read.forAsset(_id('t/wall.png'));
      expect(settings.importer, isNull);
      expect(settings.values, isEmpty);
    });

    test('a folder speaks for everything under it', () async {
      // The case the Phase 4 script could only guess at: a folder of sprites
      // is all pixel art, and saying so once is the difference between
      // settings that stay true and two hundred files nobody maintains.
      final read = ImportSettingsReader(
        _project({
          'sprites/.import.json': _settings(values: {'lossless': true}),
        }),
      );
      expect((await read.forAsset(_id('sprites/hero/idle.png'))).values, {
        'lossless': true,
      });
    });

    test('the nearer file wins, key by key', () async {
      final read = ImportSettingsReader(
        _project({
          '.import.json': _settings(values: {'maxSize': 2048, 'srgb': true}),
          'textures/.import.json': _settings(values: {'maxSize': 1024}),
        }),
      );
      expect((await read.forAsset(_id('textures/wall.png'))).values, {
        'maxSize': 1024,
        'srgb': true,
      });
    });

    test('the asset has the last word', () async {
      final read = ImportSettingsReader(
        _project({
          'textures/.import.json': _settings(values: {'role': 'colour'}),
          'textures/wall.png.import.json': _settings(
            values: {'role': 'normal'},
          ),
        }),
      );
      expect(
        (await read.forAsset(_id('textures/wall.png'))).values['role'],
        'normal',
      );
    });

    test('an importer named higher up still applies', () async {
      final read = ImportSettingsReader(
        _project({'t/.import.json': _settings(importer: 'raw')}),
      );
      expect((await read.forAsset(_id('t/table.png'))).importer, 'raw');
    });

    test('a nearer file can change the importer', () async {
      final read = ImportSettingsReader(
        _project({
          't/.import.json': _settings(importer: 'raw'),
          't/table.png.import.json': _settings(importer: 'texture'),
        }),
      );
      expect((await read.forAsset(_id('t/table.png'))).importer, 'texture');
    });

    test('one asset does not pick up another asset\'s settings', () async {
      final read = ImportSettingsReader(
        _project({
          't/wall.png.import.json': _settings(values: {'role': 'normal'}),
        }),
      );
      expect((await read.forAsset(_id('t/floor.png'))).values, isEmpty);
    });

    test('a settings file is read once however many assets ask', () async {
      var reads = 0;
      final source = CallbackAssetSource((id) async {
        if (id.toString() != 't/.import.json') return null;
        reads++;
        return utf8.encode(_settings(values: {'maxSize': 512}));
      });
      final read = ImportSettingsReader(source);
      for (final name in ['a', 'b', 'c']) {
        expect(
          (await read.forAsset(_id('t/$name.png'))).values['maxSize'],
          512,
        );
      }
      expect(reads, 1);
    });
  });

  group('merging', () {
    test('a key in the later settings replaces the earlier one outright', () {
      const outer = ImportSettings(
        values: {
          'mips': {'filter': 'box', 'renormalise': true},
        },
      );
      const inner = ImportSettings(
        values: {
          'mips': {'filter': 'kaiser'},
        },
      );
      // Shallow on purpose: renormalise is gone, not merged back in.
      expect(outer.mergedWith(inner).values, {
        'mips': {'filter': 'kaiser'},
      });
    });

    test('an importer the later settings do not name is kept', () {
      const outer = ImportSettings(importer: 'raw');
      const inner = ImportSettings(values: {'maxSize': 8});
      expect(outer.mergedWith(inner).importer, 'raw');
    });
  });

  group('reading a file', () {
    test('what encode writes, parse reads', () {
      const settings = ImportSettings(
        importer: 'texture',
        values: {'role': 'normal', 'maxSize': 1024},
      );
      final read = ImportSettings.parse(
        settings.encode(),
        from: _id('.import.json'),
      );
      expect(read.importer, 'texture');
      expect(read.values, settings.values);
    });

    test('settings are written in key order, so a diff is reviewable', () {
      const settings = ImportSettings(values: {'b': 1, 'a': 2});
      expect(
        settings.encode().indexOf('"a"'),
        lessThan(settings.encode().indexOf('"b"')),
      );
    });

    test('a file with no format version is read as the current one', () {
      final read = ImportSettings.parse(
        '{"settings": {"maxSize": 4}}',
        from: _id('.import.json'),
      );
      expect(read.values, {'maxSize': 4});
    });

    test('a newer format version is refused rather than half-read', () {
      expect(
        () => ImportSettings.parse(
          '{"formatVersion": 99, "settings": {}}',
          from: _id('.import.json'),
        ),
        throwsA(
          isA<FormatException>().having(
            (error) => error.message,
            'message',
            allOf(contains('.import.json'), contains('99')),
          ),
        ),
      );
    });

    for (final (what, text) in [
      ('not JSON at all', '{oh dear'),
      ('not an object', '[1, 2, 3]'),
      ('an importer that is not a name', '{"importer": 7}'),
      ('an importer named as nothing', '{"importer": ""}'),
      ('settings that are not an object', '{"settings": [1]}'),
      ('a format version that is not a number', '{"formatVersion": "1"}'),
    ]) {
      test('$what is refused, naming the file', () {
        expect(
          () => ImportSettings.parse(text, from: _id('t/wall.png.import.json')),
          throwsA(
            isA<FormatException>().having(
              (error) => error.message,
              'message',
              contains('t/wall.png.import.json'),
            ),
          ),
        );
      });
    }
  });
}

import 'dart:convert';
import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:orblit_sprite/orblit_sprite.dart';
import 'package:test/test.dart';

AssetId id(String text) => AssetId.parse(text);

/// A solid rectangle, as PNG bytes — enough for the packer to place and for a
/// test to tell two sprites apart by colour.
Uint8List block(int width, int height, int red) {
  final pixels = Uint8List(width * height * 4);
  for (var i = 0; i < width * height; i++) {
    pixels[i * 4] = red;
    pixels[i * 4 + 3] = 255;
  }
  return encodePng(width, height, pixels);
}

Uint8List document(Object? value) => Uint8List.fromList(
  utf8.encode(value is String ? value : jsonEncode(value)),
);

void main() {
  const importer = AtlasImporter();

  late MemoryAssetSource source;
  late MemoryCookCache cache;
  late Cook cook;

  Map<AssetId, List<int>> assets(Object document, {int count = 3}) => {
    id('ui.atlas.json'): utf8.encode(jsonEncode(document)),
    for (var i = 0; i < count; i++) id('ui/s$i.png'): block(16, 16, i * 40),
  };

  void give(Map<AssetId, List<int>> files) {
    source = MemoryAssetSource(files);
    cache = MemoryCookCache();
    cook = Cook(
      source: source,
      cache: cache,
      importers: ImporterRegistry([importer]),
      target: CookTargets.macos,
    );
  }

  group('what it claims', () {
    test('a document that says it is an atlas', () {
      expect(importer.handles(id('ui.atlas.json')), isTrue);
      expect(importer.handles(id('menus/ui.ATLAS.JSON')), isTrue);
    });

    test('and no other json, because json is everybody\'s extension', () {
      expect(importer.handles(id('level.json')), isFalse);
      expect(importer.handles(id('wall.png.import.json')), isFalse);
      expect(importer.handles(id('atlas.json')), isFalse);
    });
  });

  group('the sprites it depends on', () {
    test(
      'are the ones the document names, before anything is packed',
      () async {
        final found = await importer.dependenciesOf(
          id('ui.atlas.json'),
          document({
            'sprites': ['ui/a.png', 'ui/b.png'],
          }),
          const {},
        );
        expect(found, {id('ui/a.png'), id('ui/b.png')});
      },
    );

    test('are project-relative, with ./ meaning beside me', () async {
      final found = await importer.dependenciesOf(
        id('menus/ui.atlas.json'),
        document({
          'sprites': ['./play.png', 'shared/stop.png'],
        }),
        const {},
      );
      expect(found, {id('menus/play.png'), id('shared/stop.png')});
    });

    test('a document that is not JSON says so rather than throwing a parser '
        'at somebody', () {
      expect(
        () => importer.dependenciesOf(
          id('ui.atlas.json'),
          document('{ not json'),
          const {},
        ),
        throwsA(
          isA<ImportFailure>().having(
            (e) => e.reason,
            'reason',
            contains('not JSON'),
          ),
        ),
      );
    });

    test('a document with no sprites list says which key is missing', () {
      expect(
        () => importer.dependenciesOf(
          id('ui.atlas.json'),
          document({'padding': 2}),
          const {},
        ),
        throwsA(
          isA<ImportFailure>().having(
            (e) => e.reason,
            'reason',
            contains('"sprites"'),
          ),
        ),
      );
    });

    test('a sprite that is not an asset name is named in the failure', () {
      expect(
        () => importer.dependenciesOf(
          id('ui.atlas.json'),
          document({
            'sprites': ['https://example.com/a.png'],
          }),
          const {},
        ),
        throwsA(
          isA<ImportFailure>().having(
            (e) => e.reason,
            'reason',
            contains('example.com'),
          ),
        ),
      );
    });
  });

  group('packing', () {
    test('produces a page and a descriptor for it', () async {
      give(
        assets({
          'sprites': ['ui/s0.png', 'ui/s1.png', 'ui/s2.png'],
        }),
      );
      final result = await cook.cookOne(id('ui.atlas.json'));
      expect(result.status, CookStatus.cooked, reason: '${result.error}');
      expect(
        result.asset!.outputs.map((o) => o.name),
        containsAll(['page0.png', 'page0.json']),
      );
    });

    test('the descriptor names every sprite by its asset id', () async {
      give(
        assets({
          'sprites': ['ui/s0.png', 'ui/s1.png', 'ui/s2.png'],
        }),
      );
      final result = await cook.cookOne(id('ui.atlas.json'));
      final atlas = Atlas.read(
        utf8.decode(
          await cache.read(result.asset!['page0.json']!.hash) as Uint8List,
        ),
      );
      expect(atlas!.regions.keys, containsAll(['ui/s0.png', 'ui/s2.png']));
    });

    test('the page is a PNG that reads back at the size it says', () async {
      give(
        assets({
          'sprites': ['ui/s0.png'],
          'powerOfTwo': false,
          'padding': 0,
        }),
      );
      final result = await cook.cookOne(id('ui.atlas.json'));
      final page = decodePng(
        await cache.read(result.asset!['page0.png']!.hash) as Uint8List,
      );
      expect(page.width, greaterThanOrEqualTo(16));
      expect(page.pixels.length, page.width * page.height * 4);
    });

    test(
      'says what it did, because a pack is a result worth reading',
      () async {
        give(
          assets({
            'sprites': ['ui/s0.png', 'ui/s1.png'],
          }),
        );
        final result = await cook.cookOne(id('ui.atlas.json'));
        expect(result.notes.first, contains('2 sprites'));
        expect(result.notes.join(' '), contains('% full'));
      },
    );

    test('an empty atlas is a mistake, not an empty page', () async {
      give({
        id('ui.atlas.json'): utf8.encode(jsonEncode({'sprites': <String>[]})),
      });
      final result = await cook.cookOne(id('ui.atlas.json'));
      expect(result.status, CookStatus.failed);
      expect('${result.error}', contains('at least one sprite'));
    });

    test(
      'a sprite too large to fit fails the atlas rather than going missing',
      () async {
        give({
          id('ui.atlas.json'): utf8.encode(
            jsonEncode({
              'sprites': ['ui/huge.png'],
              'maxPageSize': 32,
            }),
          ),
          id('ui/huge.png'): block(64, 64, 255),
        });
        final result = await cook.cookOne(id('ui.atlas.json'));
        expect(result.status, CookStatus.failed);
        expect('${result.error}', contains('do not fit'));
        expect('${result.error}', contains('ui/huge.png'));
      },
    );

    test('an unknown heuristic says which ones there are', () async {
      give(
        assets({
          'sprites': ['ui/s0.png'],
          'heuristic': 'sideways',
        }),
      );
      final result = await cook.cookOne(id('ui.atlas.json'));
      expect(result.status, CookStatus.failed);
      expect('${result.error}', contains('contact'));
    });

    test(
      'a sprite that is not a PNG names the sprite, not the atlas',
      () async {
        give({
          id('ui.atlas.json'): utf8.encode(
            jsonEncode({
              'sprites': ['ui/broken.png'],
            }),
          ),
          id('ui/broken.png'): utf8.encode('this is not a png'),
        });
        final result = await cook.cookOne(id('ui.atlas.json'));
        expect(result.status, CookStatus.failed);
        expect('${result.error}', contains('ui/broken.png'));
      },
    );
  });

  group('what a repack costs', () {
    test('nothing, when neither the document nor a sprite changed', () async {
      final files = assets({
        'sprites': ['ui/s0.png', 'ui/s1.png'],
      });
      give(files);
      await cook.cookOne(id('ui.atlas.json'));

      final again = Cook(
        source: MemoryAssetSource(files),
        cache: cache,
        importers: ImporterRegistry([importer]),
        target: CookTargets.macos,
      );
      expect(
        (await again.cookOne(id('ui.atlas.json'))).status,
        CookStatus.cached,
      );
    });

    test('a repack, when one sprite in it changed', () async {
      final files = assets({
        'sprites': ['ui/s0.png', 'ui/s1.png'],
      });
      give(files);
      await cook.cookOne(id('ui.atlas.json'));

      final again = Cook(
        source: MemoryAssetSource({
          ...files,
          id('ui/s1.png'): block(16, 16, 200),
        }),
        cache: cache,
        importers: ImporterRegistry([importer]),
        target: CookTargets.macos,
      );
      expect(
        (await again.cookOne(id('ui.atlas.json'))).status,
        CookStatus.cooked,
      );
    });
  });

  test('a project cooks atlases without being told to', () {
    expect(CookProject.defaultImporters().byName('atlas'), isNotNull);
    expect(RuntimeImport.defaultImporters().byName('atlas'), isNotNull);
  });
}

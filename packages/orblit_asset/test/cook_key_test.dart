import 'dart:convert';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:test/test.dart';

ContentHash _hash(String text) => ContentHash.of(utf8.encode(text));

CookKey _key({
  String importer = 'texture',
  int version = 1,
  String source = 'source',
  Map<AssetId, ContentHash> dependencies = const {},
  Map<String, Object?> settings = const {},
  Map<String, Object?> target = const {},
}) => CookKey(
  importer: importer,
  importerVersion: version,
  source: _hash(source),
  dependencies: dependencies,
  settings: settings,
  target: target,
);

void main() {
  group('what changes a key', () {
    test('the same recipe is the same key', () {
      expect(_key(), _key());
      expect(_key().hash, _key().hash);
    });

    test('different source bytes are a different key', () {
      expect(_key(source: 'a'), isNot(_key(source: 'b')));
    });

    test('a different importer is a different key', () {
      expect(_key(importer: 'texture'), isNot(_key(importer: 'splat')));
    });

    test('a new version of one importer is a different key', () {
      // The case a cache gets wrong when nobody thinks about it: every byte
      // on disk is the same and the code that reads them is not.
      expect(_key(version: 1), isNot(_key(version: 2)));
    });

    test('different settings are a different key', () {
      expect(
        _key(settings: {'role': 'colour'}),
        isNot(_key(settings: {'role': 'normal'})),
      );
    });

    test('a different target is a different key', () {
      expect(
        _key(target: {'formats': 'astc'}),
        isNot(_key(target: {'formats': 'bc'})),
      );
    });

    test('a dependency changing is a different key', () {
      final id = AssetId.parse('textures/wall.png');
      expect(
        _key(dependencies: {id: _hash('one')}),
        isNot(_key(dependencies: {id: _hash('two')})),
      );
    });

    test('a dependency moving to another id is a different key', () {
      // Every byte in the project is the same; what the result points at is
      // not.
      expect(
        _key(dependencies: {AssetId.parse('a/wall.png'): _hash('x')}),
        isNot(_key(dependencies: {AssetId.parse('b/wall.png'): _hash('x')})),
      );
    });

    test('losing a dependency is a different key', () {
      expect(
        _key(dependencies: {AssetId.parse('a.bin'): _hash('x')}),
        isNot(_key()),
      );
    });
  });

  group('what does not change a key', () {
    test('the order settings were built in', () {
      expect(
        _key(settings: {'a': 1, 'b': 2}),
        _key(settings: {'b': 2, 'a': 1}),
      );
    });

    test('the order nested settings were built in', () {
      expect(
        _key(
          settings: {
            'mips': {'filter': 'box', 'renormalise': true},
          },
        ),
        _key(
          settings: {
            'mips': {'renormalise': true, 'filter': 'box'},
          },
        ),
      );
    });

    test('the order dependencies were built in', () {
      final a = AssetId.parse('a.bin');
      final b = AssetId.parse('b.bin');
      expect(
        _key(dependencies: {a: _hash('1'), b: _hash('2')}),
        _key(dependencies: {b: _hash('2'), a: _hash('1')}),
      );
    });
  });

  group('the recipe', () {
    test('is canonical JSON with every object in key order', () {
      final recipe = jsonDecode(_key(settings: {'b': 1, 'a': 2}).recipe);
      expect(recipe, isA<Map<String, Object?>>());
      expect((recipe as Map<String, Object?>)['settings'], {'a': 2, 'b': 1});
      expect(_key(settings: {'b': 1, 'a': 2}).recipe, contains('"a":2,"b":1'));
    });

    test('says which of two keys differs, which is why it is kept', () {
      final colour = _key(settings: {'role': 'colour'}).recipe;
      final normal = _key(settings: {'role': 'normal'}).recipe;
      expect(colour, isNot(normal));
      expect(colour.replaceAll('colour', 'normal'), normal);
    });

    test('is the text the key hashes', () {
      final key = _key();
      expect(key.hash, ContentHash.of(utf8.encode(key.recipe)));
    });
  });

  group('what is refused', () {
    test('an importer with no name', () {
      expect(() => _key(importer: ''), throwsArgumentError);
    });

    test('an importer version below one', () {
      expect(() => _key(version: 0), throwsArgumentError);
    });

    test('a setting that is not JSON', () {
      expect(
        () => _key(settings: {'when': Duration.zero}).recipe,
        throwsArgumentError,
      );
    });

    test('a number JSON cannot write', () {
      expect(
        () => _key(settings: {'scale': double.nan}).recipe,
        throwsArgumentError,
      );
      expect(
        () => _key(settings: {'scale': double.infinity}).recipe,
        throwsArgumentError,
      );
    });

    test('a map whose keys are not strings', () {
      expect(
        () => _key(
          settings: {
            'sizes': <Object?, Object?>{1: 'one'},
          },
        ).recipe,
        throwsArgumentError,
      );
    });
  });
}

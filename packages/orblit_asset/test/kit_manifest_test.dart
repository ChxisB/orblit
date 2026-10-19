import 'dart:convert';
import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:test/test.dart';

KitManifest manifest({
  String slug = 'crate-pack',
  String publisher = 'brass-lamp',
  String name = 'Crate pack',
  String summary = 'Twelve crates.',
  String version = '1.2.0',
  ({String spdx, String url}) licence = (
    spdx: 'CC-BY-4.0',
    url: 'https://example.com/licence',
  ),
  ({String from, String to}) engine = (from: '0.30.0', to: '0.40.0'),
  bool containsScripts = false,
  List<String> tags = const ['props'],
  List<AssetId> previews = const [],
}) => KitManifest(
  slug: slug,
  publisher: publisher,
  name: name,
  summary: summary,
  version: version,
  licence: licence,
  engine: engine,
  containsScripts: containsScripts,
  tags: tags,
  previews: previews,
);

Uint8List text(Object json) =>
    Uint8List.fromList(utf8.encode(jsonEncode(json)));

void main() {
  group('the constructor', () {
    test('keeps what it is given', () {
      final kit = manifest(previews: [AssetId.parse('previews/a.png')]);
      expect(kit.slug, 'crate-pack');
      expect(kit.engine, (from: '0.30.0', to: '0.40.0'));
      expect(kit.previews.single.toString(), 'previews/a.png');
    });

    test('refuses a slug or publisher that is not lower case words', () {
      for (final bad in ['', 'Crate', 'a_b', '-a', 'a-', 'a--b', 'a b']) {
        expect(() => manifest(slug: bad), throwsFormatException, reason: bad);
        expect(
          () => manifest(publisher: bad),
          throwsFormatException,
          reason: bad,
        );
      }
      expect(() => manifest(slug: 'a' * 41), throwsFormatException);
    });

    test('refuses a name or summary that is blank, long or not one line', () {
      expect(() => manifest(name: '  '), throwsFormatException);
      expect(() => manifest(name: 'a' * 61), throwsFormatException);
      expect(() => manifest(summary: 'one\ntwo'), throwsFormatException);
    });

    test('refuses a version that is not three whole numbers', () {
      for (final bad in ['1', '1.2', '1.2.3.4', '01.2.3', 'v1.2.3', '1.2.x']) {
        expect(() => manifest(version: bad), throwsFormatException);
      }
    });

    test('refuses a licence that says there is none', () {
      for (final bad in ['', 'none', 'NOASSERTION', 'MIT;rm', 'a' * 81]) {
        expect(
          () => manifest(licence: (spdx: bad, url: 'https://x.org/l')),
          throwsFormatException,
          reason: bad,
        );
      }
      expect(
        () => manifest(
          licence: (spdx: 'MIT OR Apache-2.0', url: 'https://x.org/l'),
        ),
        returnsNormally,
      );
    });

    test('refuses a licence address that is not http or https', () {
      for (final bad in ['', 'file:///etc/passwd', 'example.com', 'https://']) {
        expect(
          () => manifest(licence: (spdx: 'MIT', url: bad)),
          throwsFormatException,
          reason: bad,
        );
      }
    });

    test('refuses an engine range that runs backwards', () {
      expect(
        () => manifest(engine: (from: '0.40.0', to: '0.9.0')),
        throwsFormatException,
      );
      expect(
        () => manifest(engine: (from: '0.9.0', to: '0.40.0')),
        returnsNormally,
      );
    });

    test('refuses too many tags, a repeat and a tag that is not a word', () {
      expect(
        () => manifest(tags: [for (var i = 0; i < 9; i++) 't$i']),
        throwsFormatException,
      );
      expect(() => manifest(tags: ['a', 'a']), throwsFormatException);
      expect(() => manifest(tags: ['A b']), throwsFormatException);
    });

    test('refuses a preview that is not an image or is listed twice', () {
      expect(
        () => manifest(previews: [AssetId.parse('previews/a.gif')]),
        throwsFormatException,
      );
      final one = AssetId.parse('previews/a.png');
      expect(() => manifest(previews: [one, one]), throwsFormatException);
    });
  });

  group('the bytes', () {
    test('read gives back what encode wrote', () {
      final kit = manifest(
        containsScripts: true,
        previews: [AssetId.parse('previews/a.png')],
      );
      final again = KitManifest.read(kit.encode());
      expect(again.encode(), kit.encode());
      expect(again.containsScripts, isTrue);
    });

    test('the same content is the same bytes', () {
      expect(manifest().encode(), manifest().encode());
    });

    test('read refuses another kind, a newer format and a missing flag', () {
      final good = jsonDecode(utf8.decode(manifest().encode())) as Map;
      expect(
        () => KitManifest.read(text({...good, 'kind': 'orblit.pack'})),
        throwsFormatException,
      );
      expect(
        () => KitManifest.read(text({...good, 'formatVersion': 2})),
        throwsA(
          isA<FormatException>().having(
            (e) => e.message,
            'message',
            contains('newer Orblit'),
          ),
        ),
      );
      expect(
        () => KitManifest.read(text({...good}..remove('containsScripts'))),
        throwsFormatException,
      );
    });

    test('readAuthored takes the flag from the tool, not the file', () {
      final authored = text({
        'slug': 'crate-pack',
        'publisher': 'brass-lamp',
        'name': 'Crate pack',
        'summary': 'Twelve crates.',
        'version': '1.2.0',
        'licence': {'spdx': 'MIT', 'url': 'https://x.org/l'},
        'engine': {'from': '0.30.0', 'to': '0.40.0'},
        'containsScripts': false,
      });
      expect(
        KitManifest.readAuthored(
          authored,
          containsScripts: true,
        ).containsScripts,
        isTrue,
      );
    });

    test('read refuses text that is not a manifest', () {
      expect(
        () => KitManifest.read(Uint8List.fromList(utf8.encode('{'))),
        throwsFormatException,
      );
      expect(() => KitManifest.read(text([1])), throwsFormatException);
      expect(
        () => KitManifest.read(Uint8List.fromList([0xff, 0xfe])),
        throwsFormatException,
      );
    });
  });
}

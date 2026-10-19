import 'dart:convert';
import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:test/test.dart';

KitManifest manifest(String slug, {String publisher = 'brass-lamp'}) =>
    KitManifest(
      slug: slug,
      publisher: publisher,
      name: 'Kit $slug',
      summary: 'Some crates.',
      version: '1.0.0',
      licence: (spdx: 'MIT', url: 'https://example.com/l'),
      engine: (from: '0.30.0', to: '0.40.0'),
      containsScripts: false,
      tags: ['props'],
    );

KitRow row(String slug, {String publisher = 'brass-lamp', String? thumbnail}) =>
    KitRow(
      manifest: manifest(slug, publisher: publisher),
      updated: '2026-10-19',
      thumbnail: thumbnail,
    );

PublishedRelease published(String version) => PublishedRelease(
  release: KitRelease(
    version: version,
    url: 'https://example.com/$version.opack',
    hash: ContentHash.of(utf8.encode(version)),
    bytes: 99,
    released: '2026-10-19',
    notes: 'Changes.',
  ),
  engine: (from: '0.30.0', to: '0.40.0'),
  containsScripts: true,
);

Uint8List bytes(Object? json) =>
    Uint8List.fromList(utf8.encode(jsonEncode(json)));

Map<String, Object?> json(Uint8List encoded) =>
    jsonDecode(utf8.decode(encoded)) as Map<String, Object?>;

void main() {
  group('index', () {
    test('sorts by publisher and slug and reads back the same bytes', () {
      final index = KitIndex([
        row('zebra'),
        row('crates', thumbnail: 'kits/brass-lamp/crates/preview-0.png'),
        row('apple', publisher: 'amber'),
      ]);
      expect(
        index.rows.map((r) => '${r.manifest.publisher}/${r.manifest.slug}'),
        ['amber/apple', 'brass-lamp/crates', 'brass-lamp/zebra'],
      );
      final read = KitIndex.parse(index.encode());
      expect(read.encode(), index.encode());
      final crates = read.rows[1];
      expect(crates.thumbnail, 'kits/brass-lamp/crates/preview-0.png');
      expect(crates.manifest.tags, ['props']);
      expect(crates.manifest.engine.to, '0.40.0');
      expect(crates.updated, '2026-10-19');
      expect(read.rows[0].thumbnail, isNull);
    });

    test('an empty index reads back', () {
      expect(KitIndex.parse(KitIndex(const []).encode()).rows, isEmpty);
    });

    test('ignores a key it does not know and refuses a newer format', () {
      final file = json(KitIndex([row('crates')]).encode());
      final first =
          (file['kits']! as List<Object?>).first! as Map<String, Object?>;
      first['stars'] = 5;
      expect(KitIndex.parse(bytes({...file, 'extra': 1})).rows, hasLength(1));
      expect(
        () => KitIndex.parse(bytes({...file, 'formatVersion': 2})),
        throwsA(
          isA<FormatException>().having(
            (e) => e.message,
            'message',
            contains('newer Orblit'),
          ),
        ),
      );
      expect(
        () => KitIndex.parse(bytes({...file, 'formatVersion': 0})),
        throwsFormatException,
      );
    });

    test('refuses a kit twice and a thumbnail of another kit', () {
      expect(
        () => KitIndex([row('crates'), row('crates')]),
        throwsFormatException,
      );
      for (final thumbnail in [
        'kits/brass-lamp/other/preview-0.png',
        'kits/brass-lamp/crates/preview-6.png',
        'kits/brass-lamp/crates/preview-0.gif',
        '../x/preview-0.png',
      ]) {
        expect(
          () => row('crates', thumbnail: thumbnail),
          throwsFormatException,
          reason: thumbnail,
        );
      }
    });

    test('refuses a row that a manifest would refuse, and says which', () {
      final file = json(KitIndex([row('crates')]).encode());
      final first =
          (file['kits']! as List<Object?>).first! as Map<String, Object?>;
      first['name'] = 'Crates\u202e';
      expect(
        () => KitIndex.parse(bytes(file)),
        throwsA(
          isA<FormatException>().having(
            (e) => e.message,
            'message',
            allOf(contains('index.json, kits 1'), contains('"name"')),
          ),
        ),
      );
      first['name'] = 'Crates';
      first.remove('licence');
      expect(() => KitIndex.parse(bytes(file)), throwsFormatException);
    });
  });

  group('releases', () {
    test('lists newest first and reads back the same bytes', () {
      final list = KitReleases(
        previews: ['preview-0.png', 'preview-1.webp'],
        releases: [published('1.2.0'), published('1.10.0')],
      );
      expect(list.releases.map((r) => r.release.version), ['1.10.0', '1.2.0']);
      final read = KitReleases.parse(list.encode());
      expect(read.encode(), list.encode());
      expect(read.previews, ['preview-0.png', 'preview-1.webp']);
      expect(read.releases.first.containsScripts, isTrue);
      expect(read.releases.first.engine.from, '0.30.0');
      expect(
        read.releases.first.release.offer.hash,
        ContentHash.of(utf8.encode('1.10.0')),
      );
    });

    test(
      'refuses a version twice, a bad preview name and a backward range',
      () {
        expect(
          () => KitReleases(previews: const [], releases: const []),
          throwsFormatException,
        );
        expect(
          () => KitReleases(
            previews: const [],
            releases: [published('1.0.0'), published('1.0.0')],
          ),
          throwsFormatException,
        );
        for (final name in [
          '../a.png',
          'preview-0.gif',
          'preview-9.png',
          'a/preview-0.png',
        ]) {
          expect(
            () => KitReleases(previews: [name], releases: [published('1.0.0')]),
            throwsFormatException,
            reason: name,
          );
        }
        expect(
          () => PublishedRelease(
            release: published('1.0.0').release,
            engine: (from: '0.40.0', to: '0.30.0'),
            containsScripts: false,
          ),
          throwsFormatException,
        );
      },
    );

    test('ignores a key it does not know and refuses a newer format', () {
      final file = json(
        KitReleases(
          previews: const [],
          releases: [published('1.0.0')],
        ).encode(),
      );
      expect(
        KitReleases.parse(bytes({...file, 'extra': 1})).releases,
        hasLength(1),
      );
      expect(
        () => KitReleases.parse(bytes({...file, 'formatVersion': 2})),
        throwsFormatException,
      );
    });

    test('names the release that is wrong', () {
      final file = json(
        KitReleases(
          previews: const [],
          releases: [published('1.0.0')],
        ).encode(),
      );
      ((file['releases']! as List<Object?>).first!
              as Map<String, Object?>)['url'] =
          'http://example.com/a';
      expect(
        () => KitReleases.parse(bytes(file)),
        throwsA(
          isA<FormatException>().having(
            (e) => e.message,
            'message',
            contains('releases.json, releases 1'),
          ),
        ),
      );
    });
  });
}

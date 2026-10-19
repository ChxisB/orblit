import 'dart:convert';
import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:test/test.dart';

final hash = ContentHash.of([1, 2, 3]).hex;

Map<String, Object?> release(
  String version, [
  Map<String, Object?> changes = const {},
]) => {
  'version': version,
  'url': 'https://example.com/crates-$version.opack',
  'hash': hash,
  'bytes': 1234,
  'released': '2026-10-19',
  'notes': 'Changes.',
  ...changes,
};

Uint8List entryBytes(Object? json) =>
    Uint8List.fromList(utf8.encode(jsonEncode(json)));

KitEntry entry(Object? json) =>
    KitEntry.parse(entryBytes(json), publisher: 'brass-lamp', slug: 'crates');

Matcher refuses(String what) => throwsA(
  isA<FormatException>().having(
    (error) => error.message,
    'message',
    allOf(contains('brass-lamp/crates'), contains(what)),
  ),
);

void main() {
  test('reads releases newest first and offers the pack to download', () {
    final read = entry({
      'releases': [release('1.2.0'), release('1.10.0'), release('1.9.0')],
    });
    expect(read.releases.map((r) => r.version), ['1.10.0', '1.9.0', '1.2.0']);
    expect(read.latest.version, '1.10.0');
    expect(
      read.latest.offer.url,
      Uri.parse('https://example.com/crates-1.10.0.opack'),
    );
    expect(read.latest.offer.hash.hex, hash);
    expect(read.latest.toJson()['hash'], hash);
  });

  test('notes may be left out', () {
    final bare = release('1.0.0')..remove('notes');
    expect(
      entry({
        'releases': [bare],
      }).latest.notes,
      '',
    );
  });

  final bad = <String, (Object?, String)>{
    'a key it does not know': (
      {
        'releases': [release('1.0.0')],
        'name': 'x',
      },
      'not a key of this file',
    ),
    'a release key it does not know': (
      {
        'releases': [
          release('1.0.0', {'sha': 'x'}),
        ],
      },
      'not a key of this file',
    ),
    'no releases': ({'releases': <Object?>[]}, '1 to 200 releases'),
    'releases that are not a list': ({'releases': 'x'}, 'no "releases" list'),
    'a version twice': (
      {
        'releases': [release('1.0.0'), release('1.0.0')],
      },
      'a version twice',
    ),
    'a version with a tag': (
      {
        'releases': [release('1.0.0-beta')],
      },
      'three whole numbers',
    ),
    'an http url': (
      {
        'releases': [
          release('1.0.0', {'url': 'http://example.com/a.opack'}),
        ],
      },
      'not an https address',
    ),
    'a url with a user name': (
      {
        'releases': [
          release('1.0.0', {'url': 'https://me@example.com/a'}),
        ],
      },
      'not an https address',
    ),
    'a url with a space': (
      {
        'releases': [
          release('1.0.0', {'url': 'https://example.com/a b'}),
        ],
      },
      'not an https address',
    ),
    'a url with a control character': (
      {
        'releases': [
          release('1.0.0', {'url': 'https://example.com/a\u202e'}),
        ],
      },
      'not an https address',
    ),
    'a url that is too long': (
      {
        'releases': [
          release('1.0.0', {'url': 'https://example.com/${'a' * 300}'}),
        ],
      },
      'not an https address',
    ),
    'a short hash': (
      {
        'releases': [
          release('1.0.0', {'hash': 'abc'}),
        ],
      },
      '64 hexadecimal digits',
    ),
    'a hash that is not hex': (
      {
        'releases': [
          release('1.0.0', {'hash': 'z' * 64}),
        ],
      },
      '64 hexadecimal digits',
    ),
    'no bytes': (
      {
        'releases': [
          release('1.0.0', {'bytes': 0}),
        ],
      },
      'A kit file is',
    ),
    'bytes as text': (
      {
        'releases': [
          release('1.0.0', {'bytes': '12'}),
        ],
      },
      'no "bytes" whole number',
    ),
    'too many bytes': (
      {
        'releases': [
          release('1.0.0', {'bytes': 1 << 40}),
        ],
      },
      'A kit file is',
    ),
    'a day that is not there': (
      {
        'releases': [
          release('1.0.0', {'released': '2026-02-30'}),
        ],
      },
      'a day that exists',
    ),
    'a day with one digit': (
      {
        'releases': [
          release('1.0.0', {'released': '2026-2-3'}),
        ],
      },
      'a day that exists',
    ),
    'notes on two lines': (
      {
        'releases': [
          release('1.0.0', {'notes': 'a\nb'}),
        ],
      },
      'one line',
    ),
    'notes that are too long': (
      {
        'releases': [
          release('1.0.0', {'notes': 'a' * 301}),
        ],
      },
      'one line',
    ),
  };
  for (final MapEntry(:key, :value) in bad.entries) {
    test('refuses $key', () {
      expect(() => entry(value.$1), refuses(value.$2));
    });
  }

  test('refuses a file that is not JSON, not an object or too big', () {
    expect(() => entry('{'), throwsFormatException);
    expect(() => entry(<Object?>[]), refuses('not a JSON object'));
    expect(
      () => KitEntry.parse(
        Uint8List(300 * 1024),
        publisher: 'brass-lamp',
        slug: 'crates',
      ),
      throwsFormatException,
    );
  });

  test('refuses a publisher or slug that is not a word', () {
    for (final name in ['Brass', 'a b', '../x', '']) {
      expect(
        () => KitEntry.parse(
          entryBytes({
            'releases': [release('1.0.0')],
          }),
          publisher: name,
          slug: 'crates',
        ),
        throwsFormatException,
        reason: name,
      );
    }
  });

  test('a message does not carry a control character raw', () {
    expect(
      () => entry({
        'releases': [
          release('1.0.0', {'url': 'http://a\u202e'}),
        ],
      }),
      throwsA(
        isA<FormatException>().having(
          (error) => error.message,
          'message',
          allOf(contains(r'\u202e'), isNot(contains('\u202e'))),
        ),
      ),
    );
  });
}

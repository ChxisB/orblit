import 'dart:convert';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:test/test.dart';

final ContentHash hashA = ContentHash.of(utf8.encode('a'));
final ContentHash hashB = ContentHash.of(utf8.encode('b'));

LockedKit locked({
  String publisher = 'brass-lamp',
  String slug = 'crate-pack',
  String version = '1.0.0',
  String into = 'assets/kits/crate-pack',
  Map<String, ContentHash>? files,
}) => LockedKit(
  publisher: publisher,
  slug: slug,
  version: version,
  hash: hashA,
  into: into,
  files: {
    for (final MapEntry(:key, :value) in (files ?? {'a.png': hashB}).entries)
      AssetId.parse('$into/$key'.replaceFirst(RegExp('^/'), '')): value,
  },
);

Map<String, Object?> json(KitLock lock) =>
    jsonDecode(utf8.decode(lock.encode())) as Map<String, Object?>;

void main() {
  test('writes and reads back the same lock', () {
    final lock = KitLock([locked(), locked(slug: 'barrels')]);
    final again = KitLock.parse(lock.encode());
    expect(again.kits.map((k) => k.slug), ['barrels', 'crate-pack']);
    expect(again.encode(), lock.encode());
    final kit = again.find('brass-lamp', 'crate-pack')!;
    expect(kit.hash, hashA);
    expect(kit.into, 'assets/kits/crate-pack');
    expect(kit.files.values.single, hashB);
  });

  test('is the same bytes whatever order the files were given', () {
    final one = locked(files: {'a.png': hashA, 'b.png': hashB});
    final two = locked(files: {'b.png': hashB, 'a.png': hashA});
    expect(KitLock([one]).encode(), KitLock([two]).encode());
  });

  test('adding replaces an earlier install of the same kit', () {
    final lock = KitLock([locked(), locked(slug: 'barrels')]);
    final next = lock.adding(locked(version: '1.1.0'));
    expect(next.kits, hasLength(2));
    expect(next.find('brass-lamp', 'crate-pack')!.version, '1.1.0');
    expect(lock.find('brass-lamp', 'crate-pack')!.version, '1.0.0');
  });

  test('an empty lock has no kits and adds one', () {
    expect(KitLock.empty.kits, isEmpty);
    expect(KitLock.empty.find('a', 'b'), isNull);
    expect(KitLock.empty.adding(locked()).kits, hasLength(1));
  });

  test('refuses a kit listed twice', () {
    expect(() => KitLock([locked(), locked()]), throwsFormatException);
  });

  test('refuses a bad name, version or folder', () {
    expect(() => locked(slug: 'Crate Pack'), throwsFormatException);
    expect(() => locked(version: '1.0'), throwsFormatException);
    expect(() => locked(into: '.hidden'), throwsFormatException);
  });

  test('ignores a key it does not know and refuses a newer format', () {
    final plain = json(KitLock([locked()]));
    final extra = {...plain, 'note': 'hi'};
    expect(KitLock.parse(utf8.encode(jsonEncode(extra))).kits, hasLength(1));
    final newer = {...plain, 'formatVersion': kitLockFormat + 1};
    expect(
      () => KitLock.parse(utf8.encode(jsonEncode(newer))),
      throwsA(
        isA<FormatException>().having(
          (e) => e.message,
          'message',
          contains('newer Orblit'),
        ),
      ),
    );
  });

  test('refuses text that is not a lock', () {
    for (final text in ['[]', 'nope', '{"formatVersion":1}']) {
      expect(() => KitLock.parse(utf8.encode(text)), throwsFormatException);
    }
    final bad = json(KitLock([locked()]));
    ((bad['kits'] as List).first as Map)['hash'] = 'xyz';
    expect(
      () => KitLock.parse(utf8.encode(jsonEncode(bad))),
      throwsFormatException,
    );
  });
}

@TestOn('vm')
library;

import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:orblit_asset/src/pack/pack_format.dart';
import 'package:test/test.dart';

void main() {
  late Directory temp;

  String at(String base, String relative) =>
      [base, ...relative.split('/')].join(Platform.pathSeparator);

  /// Bytes for content number [n]. Number 0 is empty and every other number
  /// is a different length or starts with a different pair of bytes.
  Uint8List content(int n) => n == 0
      ? Uint8List(0)
      : Uint8List.fromList([
          n & 0xff,
          n >> 8,
          ...List.generate((n * 13) % 200, (i) => (i * 7 + n) & 0xff),
        ]);

  /// A bundle of [count] assets where the last [duplicates] have the same
  /// bytes as the first [duplicates].
  Future<Map<AssetId, Uint8List>> bundle(
    String folder,
    int count, {
    int duplicates = 0,
  }) async {
    final assets = <AssetId, Uint8List>{};
    final entries = <AssetId, AssetEntry>{};
    for (var i = 0; i < count; i++) {
      final id = AssetId.parse('models/m$i.glb');
      final bytes = content(
        i >= count - duplicates ? i - (count - duplicates) : i,
      );
      assets[id] = bytes;
      entries[id] = AssetEntry(ContentHash.of(bytes), bytes.length);
      final file = File(at(folder, '$id'));
      await file.parent.create(recursive: true);
      await file.writeAsBytes(bytes);
    }
    await File(
      at(folder, 'manifest.json'),
    ).writeAsString(AssetManifest(entries: entries).encode());
    return assets;
  }

  Future<PackSummary> pack(
    String folder,
    String out, {
    String target = 'macos',
  }) => packBundle(
    bundlePath: folder,
    outPath: out,
    target: CookTargets.find(target)!,
  );

  /// [pack] with its directory changed by [edit], and the header following.
  Uint8List withDirectory(
    Uint8List pack,
    void Function(Map<String, Object?> json) edit,
  ) {
    final header = PackHeader.read(pack);
    final directory =
        jsonDecode(
              utf8.decode(
                pack.sublist(
                  header.directoryAt,
                  header.directoryAt + header.directoryBytes,
                ),
              ),
            )
            as Map<String, Object?>;
    edit(directory);
    final text = utf8.encode(jsonEncode(directory));
    final body = pack.sublist(0, header.directoryAt)
      ..setRange(
        0,
        PackHeader.size,
        PackHeader(
          directoryAt: header.directoryAt,
          directoryBytes: text.length,
        ).encode(),
      );
    return Uint8List.fromList([...body, ...text]);
  }

  Matcher refusedWith(String text) => throwsA(
    isA<FormatException>().having(
      (error) => error.message,
      'message',
      contains(text),
    ),
  );

  setUp(() async {
    temp = await Directory.systemTemp.createTemp('orblit_pack_');
  });

  tearDown(() => temp.delete(recursive: true));

  group('packing a bundle', () {
    test('stores duplicates once and reads every asset back', () async {
      final folder = at(temp.path, 'bundle');
      final assets = await bundle(folder, 1000, duplicates: 200);
      final out = at(temp.path, 'game.opack');

      final summary = await pack(folder, out);

      expect(summary.assets, 1000);
      expect(summary.blobs, 800);
      expect(summary.bytes, File(out).lengthSync());
      var shared = 0;
      for (var i = 800; i < 1000; i++) {
        shared += content(i - 800).length;
      }
      expect(summary.sharedBytes, shared);

      final fromMemory = await AssetPack.open(
        MemoryPackBytes(File(out).readAsBytesSync()),
      );
      final fromFile = await AssetPack.open(FilePackBytes(out));
      expect(fromMemory.hash, summary.hash);
      expect(fromFile.hash, summary.hash);
      expect(fromFile.target, 'macos');
      expect(fromFile.base, isNull);
      expect(fromFile.removed, isEmpty);
      for (final MapEntry(:key, :value) in assets.entries) {
        expect(await fromMemory.read(key), value, reason: '$key from memory');
        expect(await fromFile.read(key), value, reason: '$key from file');
      }
    });

    test('says what is in it as a manifest', () async {
      final folder = at(temp.path, 'bundle');
      final assets = await bundle(folder, 20);
      final out = at(temp.path, 'game.opack');
      await pack(folder, out);

      final opened = await AssetPack.open(FilePackBytes(out));

      expect(opened.manifest.entries.length, 20);
      for (final MapEntry(:key, :value) in assets.entries) {
        expect(
          opened.manifest.entries[key],
          AssetEntry(ContentHash.of(value), value.length),
        );
      }
    });

    test('two packs of one bundle are the same bytes', () async {
      final folder = at(temp.path, 'bundle');
      await bundle(folder, 300, duplicates: 50);
      final first = at(temp.path, 'a.opack');
      final second = at(temp.path, 'b.opack');

      await pack(folder, first);
      await pack(folder, second);

      expect(File(first).readAsBytesSync(), File(second).readAsBytesSync());
    });

    test('puts every blob on a 16-byte boundary', () async {
      final folder = at(temp.path, 'bundle');
      await bundle(folder, 100);
      final out = at(temp.path, 'game.opack');
      await pack(folder, out);
      final bytes = File(out).readAsBytesSync();
      final header = PackHeader.read(bytes);

      final directory = PackDirectory.decode(
        bytes.sublist(
          header.directoryAt,
          header.directoryAt + header.directoryBytes,
        ),
        directoryAt: header.directoryAt,
      );

      expect(directory.blobs, isNotEmpty);
      for (final blob in directory.blobs.values) {
        expect(blob.at % 16, 0);
      }
    });

    test('holds an empty asset', () async {
      final folder = at(temp.path, 'bundle');
      await bundle(folder, 1);
      final out = at(temp.path, 'game.opack');
      await pack(folder, out);

      final opened = await AssetPack.open(FilePackBytes(out));

      expect(await opened.read(AssetId.parse('models/m0.glb')), isEmpty);
    });

    test('refuses a file that is not what the manifest says', () async {
      final folder = at(temp.path, 'bundle');
      await bundle(folder, 10);
      File(at(folder, 'models/m5.glb')).writeAsBytesSync([9, 9, 9]);
      final out = at(temp.path, 'game.opack');

      await expectLater(pack(folder, out), throwsFormatException);

      expect(File(out).existsSync(), isFalse);
      expect(File('$out.partial').existsSync(), isFalse);
    });

    test('refuses a file the manifest lists and the bundle lacks', () async {
      final folder = at(temp.path, 'bundle');
      await bundle(folder, 10);
      File(at(folder, 'models/m5.glb')).deleteSync();

      await expectLater(
        pack(folder, at(temp.path, 'game.opack')),
        refusedWith('models/m5.glb'),
      );
    });

    test('refuses a folder with no manifest', () async {
      final folder = at(temp.path, 'bundle');
      Directory(folder).createSync();

      await expectLater(
        pack(folder, at(temp.path, 'game.opack')),
        refusedWith('no manifest.json'),
      );
    });
  });

  group('opening a pack', () {
    late Uint8List good;

    setUp(() async {
      final folder = at(temp.path, 'bundle');
      await bundle(folder, 3);
      final out = at(temp.path, 'game.opack');
      await pack(folder, out);
      good = File(out).readAsBytesSync();
    });

    Future<AssetPack> open(Uint8List bytes) =>
        AssetPack.open(MemoryPackBytes(bytes));

    test('refuses a truncated pack and says by how much', () async {
      final cut = good.sublist(0, good.length - 10);

      await expectLater(open(cut), refusedWith('short by 10 bytes'));
    });

    test('refuses a file too small to have a header', () async {
      await expectLater(open(Uint8List(5)), refusedWith('not an Orblit pack'));
    });

    test('refuses a file that does not start with OPAK', () async {
      final wrong = Uint8List.fromList(good)..[0] = 0x50;

      await expectLater(open(wrong), refusedWith('OPAK'));
    });

    test('refuses a newer format and names both numbers', () async {
      final newer = Uint8List.fromList(good);
      ByteData.sublistView(newer).setUint32(4, 2, Endian.little);

      await expectLater(
        open(newer),
        refusedWith('format 2; this one reads up to 1'),
      );
    });

    test('refuses a directory that is not JSON', () async {
      final header = PackHeader.read(good);
      final broken = Uint8List.fromList(good)..[header.directoryAt] = 0x78;

      await expectLater(open(broken), refusedWith('not JSON'));
    });

    test('refuses a directory of the wrong kind', () async {
      final wrong = withDirectory(
        good,
        (json) => json['kind'] = 'orblit.scene',
      );

      await expectLater(open(wrong), refusedWith('orblit.scene'));
    });

    test('refuses an asset whose blob is not there', () async {
      final wrong = withDirectory(good, (json) {
        final assets = json['assets']! as Map<String, Object?>;
        assets['models/m0.glb'] = 'a' * 64;
      });

      await expectLater(open(wrong), refusedWith('does not have'));
    });

    test('refuses blobs that overlap', () async {
      final wrong = withDirectory(good, (json) {
        final blobs = json['blobs']! as Map<String, Object?>;
        final places = blobs.values.cast<Map<String, Object?>>().toList()
          ..sort((a, b) => (a['at']! as int).compareTo(b['at']! as int));
        places.last['at'] = places[places.length - 2]['at'];
      });

      await expectLater(open(wrong), refusedWith('overlap'));
    });

    test('refuses a blob inside the header', () async {
      final wrong = withDirectory(good, (json) {
        final blobs = json['blobs']! as Map<String, Object?>;
        (blobs.values.first! as Map<String, Object?>)['at'] = 16;
      });

      await expectLater(open(wrong), refusedWith('inside the header'));
    });

    test('refuses a blob that is not on a boundary', () async {
      final wrong = withDirectory(good, (json) {
        final blobs = json['blobs']! as Map<String, Object?>;
        (blobs.values.first! as Map<String, Object?>)['at'] = 33;
      });

      await expectLater(open(wrong), refusedWith('multiple of 16'));
    });

    test('refuses a blob that runs into the directory', () async {
      final wrong = withDirectory(good, (json) {
        final blobs = json['blobs']! as Map<String, Object?>;
        final places = blobs.values.cast<Map<String, Object?>>().toList()
          ..sort((a, b) => (a['at']! as int).compareTo(b['at']! as int));
        places.last['bytes'] = 1 << 20;
      });

      await expectLater(open(wrong), refusedWith('inside the directory'));
    });
  });

  group('a pack that was changed', () {
    test('check names the assets whose bytes changed', () async {
      final folder = at(temp.path, 'bundle');
      await bundle(folder, 60, duplicates: 10);
      final out = at(temp.path, 'game.opack');
      await pack(folder, out);
      final bytes = File(out).readAsBytesSync();
      final intact = await AssetPack.open(MemoryPackBytes(bytes));
      expect(await intact.findCorrupt(), isEmpty);

      // m3 is a blob that m53 shares, so both are reported.
      final header = PackHeader.read(bytes);
      final directory = PackDirectory.decode(
        bytes.sublist(
          header.directoryAt,
          header.directoryAt + header.directoryBytes,
        ),
        directoryAt: header.directoryAt,
      );
      final hash = directory.assets[AssetId.parse('models/m3.glb')]!;
      final flipped = Uint8List.fromList(bytes);
      flipped[directory.blobs[hash]!.at + 1] ^= 0xff;

      final damaged = await AssetPack.open(MemoryPackBytes(flipped));

      expect((await damaged.findCorrupt()).map((id) => id.toString()), [
        'models/m3.glb',
        'models/m53.glb',
      ]);
    });
  });

  group('reading by id', () {
    test('an asset that is not there is not found', () async {
      final folder = at(temp.path, 'bundle');
      await bundle(folder, 2);
      final out = at(temp.path, 'game.opack');
      await pack(folder, out);
      final opened = await AssetPack.open(FilePackBytes(out));
      final missing = AssetId.parse('models/missing.glb');

      expect(opened.read(missing), throwsA(isA<AssetNotFound>()));
      expect(await opened.exists(missing), isFalse);
      expect(await opened.exists(AssetId.parse('models/m1.glb')), isTrue);
    });
  });

  group('FilePackBytes', () {
    test('answers twenty reads started together', () async {
      final path = at(temp.path, 'data.bin');
      final data = Uint8List.fromList(List.generate(4096, (i) => i & 0xff));
      File(path).writeAsBytesSync(data);
      final source = FilePackBytes(path);

      final reads = [
        for (var i = 0; i < 20; i++) source.read(i * 100, 50),
        source.length(),
      ];
      final results = await Future.wait<Object?>(reads);

      for (var i = 0; i < 20; i++) {
        expect(results[i], data.sublist(i * 100, i * 100 + 50));
      }
      expect(results.last, 4096);
      await source.close();
    });

    test('a failed read does not stop the ones behind it', () async {
      final path = at(temp.path, 'data.bin');
      File(path).writeAsBytesSync(Uint8List(100));
      final source = FilePackBytes(path);

      final tooFar = source.read(90, 50);
      final fine = source.read(0, 10);

      await expectLater(tooFar, throwsRangeError);
      expect(await fine, hasLength(10));
      await source.close();
    });

    test('opens a pack that sits inside another file', () async {
      final folder = at(temp.path, 'bundle');
      final assets = await bundle(folder, 12);
      final out = at(temp.path, 'game.opack');
      await pack(folder, out);
      final inner = File(out).readAsBytesSync();
      final wrapped = at(temp.path, 'app.apk');
      File(wrapped).writeAsBytesSync([
        ...List.filled(1000, 0xaa),
        ...inner,
        ...List.filled(77, 0xbb),
      ]);

      final opened = await AssetPack.open(
        FilePackBytes(wrapped, start: 1000, bytes: inner.length),
      );

      for (final MapEntry(:key, :value) in assets.entries) {
        expect(await opened.read(key), value);
      }
    });
  });
}

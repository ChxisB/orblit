import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';

/// A pack built for a test, as the file it is and as an opened pack.
final class BuiltPack {
  const BuiltPack(this.bytes, this.pack);

  final Uint8List bytes;
  final AssetPack pack;

  ContentHash get hash => pack.hash;
}

/// Real packs, made by `packBundle` in a temporary folder.
final class PackFixture {
  PackFixture._(this.directory);

  static Future<PackFixture> create() async => PackFixture._(
    await Directory.systemTemp.createTemp('orblit_pack_fixture_'),
  );

  final Directory directory;

  Future<void> dispose() => directory.delete(recursive: true);

  String at(String relative) =>
      [directory.path, ...relative.split('/')].join(Platform.pathSeparator);

  /// Packs [files], each a path and its text, as `<name>.opack`.
  ///
  /// With [base], the pack is a patch on those packs and holds only what
  /// differs from them.
  Future<BuiltPack> build(
    String name,
    Map<String, String> files, {
    List<AssetPack> base = const [],
    String target = 'android',
  }) async {
    final entries = <AssetId, AssetEntry>{};
    for (final MapEntry(:key, :value) in files.entries) {
      final bytes = utf8.encode(value);
      final file = File(at('bundle_$name/$key'));
      await file.parent.create(recursive: true);
      await file.writeAsBytes(bytes);
      entries[AssetId.parse(key)] = AssetEntry(
        ContentHash.of(bytes),
        bytes.length,
      );
    }
    await File(
      at('bundle_$name/manifest.json'),
    ).writeAsString(AssetManifest(entries: entries).encode());
    await packBundle(
      bundlePath: at('bundle_$name'),
      outPath: at('$name.opack'),
      target: CookTargets.find(target)!,
      base: base,
    );
    final bytes = await File(at('$name.opack')).readAsBytes();
    return BuiltPack(bytes, await AssetPack.open(MemoryPackBytes(bytes)));
  }
}

import 'dart:typed_data';

import '../net/range_pack_bytes.dart';
import '../net/transport.dart';
import '../pack/asset_pack.dart';
import 'kit.dart';
import 'kit_entry.dart';
import 'kit_index.dart';

/// A file of a store could not be read.
final class KitStoreFailed implements Exception {
  const KitStoreFailed(this.url, this.reason);

  final Uri url;
  final String reason;

  @override
  String toString() => 'Could not read $url: $reason.';
}

/// Reads a store of kits over an [AssetTransport]: the index, the releases
/// of one kit, its previews and the file list of a release.
///
/// Every file is read to the limit the format gives it and no further, so a
/// store cannot make a reader hold more than that. A reply that is not a 200
/// is refused. Nothing here writes, and no file of a kit is fetched except
/// the manifest.
final class KitStoreClient {
  /// Throws an [ArgumentError] when [kitStoreAddressProblem] has something to
  /// say about [base]. A path with no trailing slash names a folder.
  KitStoreClient(Uri base, this._transport) : base = _folder(base);

  final AssetTransport _transport;

  /// The folder of the store, which holds `index.json`. Ends in a slash.
  final Uri base;

  Future<KitIndex> index() async =>
      KitIndex.parse(await _get('index.json', kitIndexLimit));

  Future<KitReleases> releases(KitRow row) async => KitReleases.parse(
    await _get('${_folderOf(row)}releases.json', kitReleasesLimit),
  );

  /// The bytes of the preview image [name], one of [KitReleases.previews].
  Future<Uint8List> preview(KitRow row, String name) async {
    if (!isKitPreviewName(name)) {
      throw ArgumentError.value(name, 'name', 'is not a preview file');
    }
    return _get('${_folderOf(row)}$name', kitPreviewBytes);
  }

  /// The image a card shows, or null for a kit that lists none.
  Future<Uint8List?> thumbnail(KitRow row) async {
    final path = row.thumbnail;
    return path == null ? null : preview(row, path.split('/').last);
  }

  /// [release] opened without its blobs: the file list, the sizes and the
  /// manifest, by ranged requests.
  ///
  /// Throws a [PackRangeFailed] when the server does not answer a range, and
  /// a [FormatException] when the file is not a kit.
  Future<Kit> open(KitRelease release) async => Kit.open(
    await AssetPack.open(RangePackBytes(_transport, Uri.parse(release.url))),
  );

  String _folderOf(KitRow row) =>
      'kits/${row.manifest.publisher}/${row.manifest.slug}/';

  Future<Uint8List> _get(String path, int limit) async {
    final url = base.resolve(path);
    final reply = await _transport.send(FetchRequest(url));
    if (reply.status != 200) {
      await reply.body.listen(null).cancel();
      throw KitStoreFailed(url, 'the server answered ${reply.status}');
    }
    final length = reply.length;
    if (length != null && length > limit) {
      await reply.body.listen(null).cancel();
      throw KitStoreFailed(url, 'it is $length bytes and the most is $limit');
    }
    final body = BytesBuilder(copy: false);
    await for (final chunk in reply.body) {
      body.add(chunk);
      if (body.length > limit) {
        throw KitStoreFailed(url, 'it passed the $limit bytes allowed');
      }
    }
    return body.takeBytes();
  }
}

/// Why [base] cannot be the address of a store, or null.
///
/// A store is read over https, from a host, with no user, query or fragment.
String? kitStoreAddressProblem(Uri base) =>
    base.scheme == 'https' &&
        base.host.isNotEmpty &&
        base.userInfo.isEmpty &&
        !base.hasQuery &&
        !base.hasFragment
    ? null
    : 'An address of a store is https, with a host and no user, query or '
          'fragment.';

Uri _folder(Uri base) {
  final problem = kitStoreAddressProblem(base);
  if (problem != null) throw ArgumentError.value('$base', 'base', problem);
  return base.path.endsWith('/') ? base : base.replace(path: '${base.path}/');
}

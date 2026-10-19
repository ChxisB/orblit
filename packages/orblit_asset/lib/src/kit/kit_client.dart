import 'dart:async';
import 'dart:typed_data';

import '../net/range_pack_bytes.dart';
import '../net/transport.dart';
import '../pack/asset_pack.dart';
import '../pack/pack_bytes.dart';
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
  KitStoreClient(
    Uri base,
    this._transport, {
    this.timeout = const Duration(seconds: 30),
  }) : base = _folder(base);

  final AssetTransport _transport;

  /// How long one file, or the file list of one release, may take.
  ///
  /// shortcut: this stops the wait and not the request. The caller closes the
  /// transport to stop the request. Pass a token through `AssetTransport`
  /// when a request must be cancelled alone.
  final Duration timeout;

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
  /// a [FormatException] when the file is not a kit or asks to read more than
  /// a kit's directory needs.
  Future<Kit> open(KitRelease release) {
    final url = Uri.parse(release.url);
    return _within(url, () async {
      final bytes = _Bounded(RangePackBytes(_transport, url));
      return Kit.open(await AssetPack.open(bytes));
    });
  }

  String _folderOf(KitRow row) =>
      'kits/${row.manifest.publisher}/${row.manifest.slug}/';

  Future<T> _within<T>(Uri url, Future<T> Function() read) => read().timeout(
    timeout,
    onTimeout: () => throw KitStoreFailed(url, 'it was not answered in time'),
  );

  Future<Uint8List> _get(String path, int limit) {
    final url = base.resolve(path);
    return _within(url, () => _read(url, limit));
  }

  Future<Uint8List> _read(Uri url, int limit) async {
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

/// The most `open` reads in one piece. A header says how long its directory
/// is, and a store must not pick how much a reader holds to list the files.
/// A kit of 5000 files has a directory of about a megabyte.
const int _openLimit = 4 * 1024 * 1024;

final class _Bounded implements PackBytes {
  _Bounded(this._inner);

  final PackBytes _inner;

  @override
  Future<int> length() => _inner.length();

  @override
  Future<Uint8List> read(int at, int bytes) async {
    if (bytes > _openLimit) {
      throw FormatException(
        'The file asks for $bytes bytes to list its files, and the most is '
        '$_openLimit.',
      );
    }
    return _inner.read(at, bytes);
  }
}

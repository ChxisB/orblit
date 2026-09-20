// The web half of the conditional export in cache_storage.dart.
import 'dart:js_interop';
import 'dart:js_interop_unsafe';
import 'dart:typed_data';

import 'package:web/web.dart' as web;

import '../content_hash.dart';
import '../content_store.dart';
import 'records.dart';

/// A content store in the browser's Cache Storage.
///
/// The web's answer to a directory of files. Cache Storage is the only
/// browser store built for whole responses rather than values — entries are
/// kept as bytes, not serialised into a database row — and it is the one the
/// browser already evicts sensibly under pressure, which for a cache of
/// assets that can always be downloaded again is the behaviour wanted.
///
/// Entries are filed under a synthetic same-origin URL, `/_orblit/content/`
/// and the hash. Nothing is ever served from those paths; they are names in a
/// key-value store that happens to spell its keys as URLs. Same-origin
/// because the API will not store a request for another one, and a path no
/// server would answer so that a stray fetch of it fails rather than quietly
/// returning a page.
///
/// Cache Storage exists only in a secure context, which means HTTPS or
/// localhost. [open] says so plainly rather than handing back a store that
/// fails on first use.
class CacheStorageContentStore implements ContentStore {
  CacheStorageContentStore._(this._cache, this.name);

  final web.Cache _cache;

  /// The name of the cache this store lives in.
  final String name;

  /// Opens the cache, creating it if it is not there.
  static Future<CacheStorageContentStore> open({
    String name = 'orblit-assets',
  }) async {
    final cache = await _openCache(name);
    return CacheStorageContentStore._(cache, name);
  }

  static String _keyFor(ContentHash hash) => '/_orblit/content/${hash.hex}';

  /// The bytes under [hash], checked against it on the way out.
  ///
  /// Checked for the same reason the directory store checks: everything above
  /// trusts the hash, and a cache entry can be changed by things that are not
  /// this store. An entry that fails is deleted and reported as absent, which
  /// is what lets the next [put] of the right bytes write it back.
  @override
  Future<Uint8List?> get(ContentHash hash) async {
    final found = await _cache.match(_keyFor(hash).toJS).toDart;
    if (found == null) return null;

    final buffer = await found.arrayBuffer().toDart;
    final bytes = buffer.toDart.asUint8List();
    if (ContentHash.of(bytes) != hash) {
      await _cache.delete(_keyFor(hash).toJS).toDart;
      return null;
    }
    return bytes;
  }

  /// Files [bytes] under their hash.
  ///
  /// Bytes already stored are not written again: the name of an entry is its
  /// contents, so an entry under that name already holds them, and if it has
  /// been damaged since, [get] is where that is caught.
  @override
  Future<ContentHash> put(List<int> bytes) async {
    final hash = ContentHash.of(bytes);
    final key = _keyFor(hash);
    if (await _cache.match(key.toJS).toDart != null) return hash;

    // A copy, because the response holds on to the buffer and the caller is
    // free to reuse a builder's list after this returns.
    final copy = Uint8List.fromList(bytes);
    await _cache.put(key.toJS, web.Response(copy.toJS, _binary)).toDart;
    return hash;
  }

  /// Whether there is an entry under [hash].
  ///
  /// Not checked against the hash the way [get] is, since that means reading
  /// all of it. An entry this says is there can still turn out damaged.
  @override
  Future<bool> contains(ContentHash hash) async =>
      await _cache.match(_keyFor(hash).toJS).toDart != null;

  @override
  Future<void> remove(ContentHash hash) async {
    await _cache.delete(_keyFor(hash).toJS).toDart;
  }

  static web.ResponseInit get _binary => web.ResponseInit(
    status: 200,
    headers: {'content-type': 'application/octet-stream'}.jsify()! as JSObject,
  );
}

/// Fetch records in the browser's Cache Storage.
///
/// The whole set as one entry beside the assets it describes, so that clearing
/// the cache clears the notes about it too and the two cannot disagree.
/// Writes are coalesced by [BufferedFetchRecords], which matters more here
/// than on disk: every write crosses into the browser's storage layer.
class CacheStorageFetchRecords extends BufferedFetchRecords {
  CacheStorageFetchRecords._(this._cache, this.name, {super.settle});

  final web.Cache _cache;

  /// The name of the cache these records live in.
  final String name;

  static const String _key = '/_orblit/records.json';

  /// Opens the cache, creating it if it is not there.
  static Future<CacheStorageFetchRecords> open({
    String name = 'orblit-assets',
    Duration settle = const Duration(milliseconds: 250),
  }) async {
    final cache = await _openCache(name);
    return CacheStorageFetchRecords._(cache, name, settle: settle);
  }

  @override
  Future<String?> readText() async {
    final found = await _cache.match(_key.toJS).toDart;
    if (found == null) return null;
    return (await found.text().toDart).toDart;
  }

  @override
  Future<void> writeText(String text) async {
    // One put, which replaces the entry: Cache Storage has no half-written
    // entry to leave behind, so no rename dance is needed the way it is on a
    // file system.
    await _cache
        .put(
          _key.toJS,
          web.Response(
            text.toJS,
            web.ResponseInit(
              status: 200,
              headers:
                  {'content-type': 'application/json'}.jsify()! as JSObject,
            ),
          ),
        )
        .toDart;
  }
}

Future<web.Cache> _openCache(String name) async {
  // `caches` is undefined outside a secure context, and reading it then is a
  // ReferenceError rather than a null — which is why this is a check and not
  // a null test.
  if (!web.window.has('caches')) {
    throw UnsupportedError(
      'Cache Storage is only available in a secure context. Serve this over '
      'HTTPS or from localhost.',
    );
  }
  return await web.window.caches.open(name).toDart;
}

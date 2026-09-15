import 'dart:typed_data';

import 'content_hash.dart';

/// Bytes filed by their hash.
///
/// Content-addressed: nothing is stored under a name somebody chose, only
/// under what the bytes are. Putting the same bytes twice therefore stores
/// them once, two assets that happen to be identical share an entry, and an
/// entry never needs updating — different bytes are a different entry. That
/// is what lets a cache built on this skip work it has already done without
/// ever asking whether what it has is stale.
abstract interface class ContentStore {
  /// The bytes stored under [hash], or null when there are none.
  ///
  /// Null rather than an exception, unlike an asset source: a store is asked
  /// speculatively — have I already got this? — and a miss is an ordinary
  /// answer rather than a mistake.
  Future<Uint8List?> get(ContentHash hash);

  /// Stores [bytes] and gives the hash they are now filed under.
  Future<ContentHash> put(List<int> bytes);

  /// Whether anything is stored under [hash].
  Future<bool> contains(ContentHash hash);

  /// Forgets whatever is stored under [hash]. Nothing there is not an error.
  Future<void> remove(ContentHash hash);
}

/// A content store held in memory, for tests and short-lived tools.
///
/// Copies on the way in and out, so that bytes changed by whoever put them —
/// or whoever got them — cannot end up filed under a hash they no longer
/// match.
class MemoryContentStore implements ContentStore {
  final Map<ContentHash, Uint8List> _entries = {};

  @override
  Future<Uint8List?> get(ContentHash hash) async {
    final bytes = _entries[hash];
    return bytes == null ? null : Uint8List.fromList(bytes);
  }

  @override
  Future<ContentHash> put(List<int> bytes) async {
    final hash = ContentHash.of(bytes);
    _entries.putIfAbsent(hash, () => Uint8List.fromList(bytes));
    return hash;
  }

  @override
  Future<bool> contains(ContentHash hash) async => _entries.containsKey(hash);

  @override
  Future<void> remove(ContentHash hash) async {
    _entries.remove(hash);
  }
}

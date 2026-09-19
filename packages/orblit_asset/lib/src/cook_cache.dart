import 'dart:typed_data';

import 'content_hash.dart';
import 'content_store.dart';
import 'cook_key.dart';

/// One file a cook produced.
///
/// A cook is rarely one file out. A texture becomes a KTX2 for each family the
/// targets need; a model becomes a GLB and the atlas it was packed into. They
/// travel together because they were made together: half of a cook is not a
/// hit, and a cache that could hand back two of three outputs would let a
/// build run on a model whose textures were evicted last week.
class CookOutput {
  const CookOutput({
    required this.name,
    required this.hash,
    required this.bytes,
  }) : assert(bytes >= 0);

  /// What this output is called among the cook's outputs: `wall.astc.ktx2`.
  ///
  /// A name within the cook, not an asset id. Where the file ends up is the
  /// caller's business — cooked beside the source while developing, inside an
  /// app bundle when shipping — and an output that carried a path would have
  /// to be rewritten at every one of those.
  final String name;

  /// What the output is, byte for byte, and how to get it back out of the
  /// store.
  final ContentHash hash;

  /// How many bytes that is, so a cache can add up what it is holding without
  /// reading any of it.
  final int bytes;

  @override
  bool operator ==(Object other) =>
      other is CookOutput &&
      other.name == name &&
      other.hash == hash &&
      other.bytes == bytes;

  @override
  int get hashCode => Object.hash(name, hash, bytes);

  @override
  String toString() => 'CookOutput($name, $hash, $bytes bytes)';
}

/// What one cook produced, all of it.
class CookedAsset {
  CookedAsset(List<CookOutput> outputs)
    : outputs = List.unmodifiable(
        outputs.toList()..sort((a, b) => a.name.compareTo(b.name)),
      );

  /// Every file the cook produced, in name order.
  ///
  /// Sorted rather than left as the importer emitted them, so that two cooks
  /// of the same thing compare equal and a manifest built from them does not
  /// reorder itself between runs.
  final List<CookOutput> outputs;

  /// The output called [name], or null when the cook did not produce one.
  CookOutput? operator [](String name) {
    for (final output in outputs) {
      if (output.name == name) return output;
    }
    return null;
  }

  /// How many bytes the whole cook came to.
  int get bytes => outputs.fold(0, (sum, output) => sum + output.bytes);

  @override
  String toString() => 'CookedAsset(${outputs.join(', ')})';
}

/// Cooked results, filed by what made them.
///
/// The whole point of the pipeline: a rebuild with nothing changed should cook
/// nothing, and changing one texture should recook that texture and nothing
/// else. Both fall out of keying on the recipe — see [CookKey], which is where
/// the care went.
///
/// A miss is never wrong, only slow. Everything here is therefore allowed to
/// answer "no" whenever it is not certain: an entry whose bytes have been
/// evicted, an index it could not read, a store on a disk that has gone away.
/// A hit, by contrast, has to be right, so an entry is only reported when
/// every one of its outputs is still there to be read.
/// Why cooking a key did not work, remembered so that something can say so
/// without cooking again.
///
/// A cache of failures is only ever for *reporting*. A cook never reads one to
/// decide whether to try: importers fail for reasons that are not in the key —
/// a tool that was not installed, a disk that was full, a process the machine
/// killed — and a cache that remembered those as settled would turn a bad
/// afternoon into a project that never builds again.
class CookFailure {
  const CookFailure({required this.reason, required this.at});

  /// What went wrong, as it was reported. A person reads this, so it is the
  /// importer's own words rather than a code.
  final String reason;

  /// When it was recorded, in UTC.
  final DateTime at;

  @override
  String toString() => 'CookFailure($reason, at $at)';
}

abstract class CookCache {
  /// What [key] cooked to last time, or null when this cache cannot say.
  Future<CookedAsset?> lookUp(CookKey key);

  /// Files the outputs of cooking [key], and reports them back as they will
  /// be read.
  ///
  /// Storing the same key twice is not an error. A cook that ran because two
  /// builds started at once, or because an entry was evicted between the look
  /// up and the cook, should not fail on the way out.
  Future<CookedAsset> store(CookKey key, Map<String, List<int>> outputs);

  /// The bytes of one output, or null when they are no longer there.
  Future<Uint8List?> read(ContentHash hash);

  /// What cooking [key] failed with last time, or null when nothing is
  /// remembered about it.
  ///
  /// This is here so that an editor can mark a broken asset without running
  /// the importer that breaks on it. Like every other answer from a cache,
  /// null is always allowed: a cache that has forgotten a failure says the
  /// asset is merely uncooked, which is the truth minus one detail.
  Future<CookFailure?> lookUpFailure(CookKey key);

  /// Remembers that cooking [key] did not work.
  ///
  /// Recording is not refusing. [lookUp] is unaffected and no cook consults
  /// this before working — see [CookFailure] for why remembering a failure as
  /// settled would be wrong.
  Future<void> recordFailure(CookKey key, String reason);
}

/// A cook cache held in memory, for tests and for a single short-lived run.
///
/// Nothing is evicted: a run that outgrows memory has a size problem this
/// would only hide. The disk cache is where the size limit belongs, because
/// that is the one that outlives the process that filled it.
class MemoryCookCache implements CookCache {
  final ContentStore _content = MemoryContentStore();
  final Map<ContentHash, CookedAsset> _entries = {};
  final Map<ContentHash, CookFailure> _failures = {};

  @override
  Future<CookedAsset?> lookUp(CookKey key) async {
    final found = _entries[key.hash];
    if (found == null) return null;
    for (final output in found.outputs) {
      if (!await _content.contains(output.hash)) return null;
    }
    return found;
  }

  @override
  Future<CookedAsset> store(CookKey key, Map<String, List<int>> outputs) async {
    final stored = <CookOutput>[];
    for (final entry in outputs.entries) {
      final hash = await _content.put(entry.value);
      stored.add(
        CookOutput(name: entry.key, hash: hash, bytes: entry.value.length),
      );
    }
    // A key that cooks is a key that no longer failed: the tool that was
    // missing has been installed, or the disk has been cleared.
    _failures.remove(key.hash);
    return _entries[key.hash] = CookedAsset(stored);
  }

  @override
  Future<Uint8List?> read(ContentHash hash) => _content.get(hash);

  @override
  Future<CookFailure?> lookUpFailure(CookKey key) async => _failures[key.hash];

  @override
  Future<void> recordFailure(CookKey key, String reason) async {
    _failures[key.hash] = CookFailure(
      reason: reason,
      at: DateTime.now().toUtc(),
    );
  }
}

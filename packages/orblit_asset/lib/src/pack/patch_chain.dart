import '../content_hash.dart';
import 'asset_pack.dart';

/// A pack in a folder that the chain did not take, and why.
final class SkippedPack {
  const SkippedPack(this.pack, this.reason);

  final AssetPack pack;

  /// One sentence, written to be read by whoever has to fix it.
  final String reason;

  @override
  String toString() => '${_short(pack.hash)}: $reason';
}

/// The packs found in a folder, put in the order a game mounts them.
///
/// A patch names the pack it was built on, so the order is not a matter of
/// file names or dates. It is found by starting at the pack the app shipped
/// and following the packs that name it, then the ones that name those. A
/// folder that lists its files in any order gives the same answer.
///
/// Nothing is mounted here. [ordered] goes to `MountedPacks.patch` in order
/// and [standalone] to `addMod`, and a game that wants to say why a pack was
/// left out reads [skipped].
final class PatchChain {
  const PatchChain._(this.ordered, this.standalone, this.skipped);

  /// Sorts [found] against [shipped], the pack the chain starts from.
  ///
  /// A pack is skipped, with a reason, when it was built for another target,
  /// when the pack it was built on is not here, and when two packs were built
  /// on the same one. The last case is a fork. Nothing says which of the two
  /// is the later, so the chain stops there and does not guess.
  factory PatchChain.of(AssetPack shipped, Iterable<AssetPack> found) {
    final sorting = _Sorting(shipped, found)
      ..follow()
      ..explainTheRest();
    return PatchChain._(sorting.ordered, sorting.standalone, [
      for (final MapEntry(:key, :value) in sorting.skipped.entries)
        SkippedPack(key, value),
    ]);
  }

  /// The patches that stand on the shipped pack, lowest first.
  final List<AssetPack> ordered;

  /// Packs with no base, which go above the chain as mods.
  ///
  /// In the order [PatchChain.of] was given them, which is by hash when they
  /// come from `PackFolder.list`. Which mod wins an id is the order it is
  /// mounted in, and an app that lets players choose sorts these itself.
  final List<AssetPack> standalone;

  final List<SkippedPack> skipped;
}

/// One run of [PatchChain.of], holding what it has worked out so far.
final class _Sorting {
  _Sorting(this._shipped, Iterable<AssetPack> found) {
    for (final pack in found) {
      if (pack.hash != _shipped.hash) _everything[pack.hash] = pack;
    }
    for (final pack in _everything.values) {
      if (pack.target != _shipped.target) {
        skipped[pack] =
            'it is for ${pack.target} and the shipped pack is for '
            '${_shipped.target}';
      } else if (pack.base == null) {
        standalone.add(pack);
      } else {
        _patches.add(pack);
      }
    }
  }

  final AssetPack _shipped;
  final Map<ContentHash, AssetPack> _everything = {};
  final List<AssetPack> _patches = [];

  final List<AssetPack> ordered = [];
  final List<AssetPack> standalone = [];
  final Map<AssetPack, String> skipped = {};

  /// Walks up from the shipped pack, one patch at a time, until nothing
  /// names the top or two things do.
  void follow() {
    var top = _shipped.hash;
    while (ordered.length < _patches.length) {
      final next = [
        for (final pack in _patches)
          if (pack.base == top) pack,
      ];
      if (next.isEmpty) return;
      if (next.length > 1) {
        for (final pack in next) {
          skipped[pack] =
              '${next.length} patches were built on ${_short(top)}, and '
              'nothing says which is later';
        }
        return;
      }
      ordered.add(next.single);
      top = next.single.hash;
    }
  }

  /// Gives every patch that is neither in the chain nor skipped a reason.
  void explainTheRest() {
    for (final pack in _patches) {
      if (ordered.contains(pack) || skipped.containsKey(pack)) continue;
      final base = pack.base!;
      skipped[pack] = _everything.containsKey(base)
          ? 'the patch it was built on, ${_short(base)}, is not in the chain'
          : 'it was built on ${_short(base)} and the shipped pack is '
                '${_short(_shipped.hash)}';
    }
  }
}

String _short(ContentHash hash) => hash.hex.substring(0, 12);

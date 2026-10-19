import '../asset_id.dart';
import '../content_hash.dart';

/// What an update of an installed kit does to each file.
///
/// The project is the person's, so a file is touched only when it is still as
/// `kits.lock` recorded it. A file that differs is kept and listed.
final class KitUpdatePlan {
  KitUpdatePlan({
    required List<AssetId> replace,
    required List<AssetId> add,
    required List<AssetId> remove,
    required List<AssetId> kept,
  }) : replace = List.unmodifiable(replace),
       add = List.unmodifiable(add),
       remove = List.unmodifiable(remove),
       kept = List.unmodifiable(kept);

  /// Files the release changed that the person has not edited.
  final List<AssetId> replace;

  /// Files new in the release with nothing in their place.
  final List<AssetId> add;

  /// Files the release dropped that the person has not edited.
  final List<AssetId> remove;

  /// Files the person edited, deleted or put another file in place of, that
  /// the release would have written or removed.
  final List<AssetId> kept;

  bool get isEmpty =>
      replace.isEmpty && add.isEmpty && remove.isEmpty && kept.isEmpty;
}

enum _Move { nothing, replace, add, remove, keep }

/// Plans an update from what [locked] recorded, what the new release holds
/// ([incoming]) and what is on disk now ([onDisk], null for a missing file).
///
/// All three are keyed by place in the project. A place missing from [onDisk]
/// counts as not there. Answers and changes nothing.
KitUpdatePlan planKitUpdate({
  required Map<AssetId, ContentHash> locked,
  required Map<AssetId, ContentHash> incoming,
  required Map<AssetId, ContentHash?> onDisk,
}) {
  final moves = <_Move, List<AssetId>>{
    for (final move in _Move.values) move: [],
  };
  final places = {...locked.keys, ...incoming.keys}.toList()
    ..sort((a, b) => '$a'.compareTo('$b'));
  for (final place in places) {
    final move = _decide(locked[place], incoming[place], onDisk[place]);
    moves[move]!.add(place);
  }
  return KitUpdatePlan(
    replace: moves[_Move.replace]!,
    add: moves[_Move.add]!,
    remove: moves[_Move.remove]!,
    kept: moves[_Move.keep]!,
  );
}

_Move _decide(ContentHash? was, ContentHash? will, ContentHash? found) {
  // No lock entry means the release has the file.
  if (was == null) return _decideNew(will!, found);
  if (will == was) return _Move.nothing;
  if (will == null && found == null) return _Move.nothing;
  if (found != was) return _Move.keep;
  return will == null ? _Move.remove : _Move.replace;
}

_Move _decideNew(ContentHash will, ContentHash? found) {
  if (found == null) return _Move.add;
  return found == will ? _Move.nothing : _Move.keep;
}

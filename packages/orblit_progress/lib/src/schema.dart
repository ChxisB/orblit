/// A name a game gives to a number.
sealed class ProgressName {
  const ProgressName(this.name);

  final String name;
}

/// A whole unsigned 32 bit integer.
final class Whole extends ProgressName {
  const Whole(super.name);
}

/// [bits] bits of the [Whole] called [of], from bit [low] up. Writing a slice
/// leaves the other bits of that whole as they were.
final class Slice extends ProgressName {
  const Slice(
    super.name, {
    required this.of,
    required this.low,
    required this.bits,
  });

  final String of;
  final int low;
  final int bits;
}

/// Where a name's bits are: in slot [slot], [bits] of them from bit [low].
typedef Place = ({int slot, int low, int bits});

const int _maxSlots = 0xFFFF;
const int _all = 0xFFFFFFFF;

/// The bits of its slot that [place] covers. The shift by 32 is avoided, as it
/// wraps to zero where integers are 32 bit.
int placeMask(Place place) =>
    place.bits == 32 ? _all : ((1 << place.bits) - 1) << place.low & _all;

/// The names a game uses and where each one lives.
///
/// A slot is the number stored for one [Whole], numbered from zero in the
/// order the wholes are listed. A slice shares its whole's slot. Adding names
/// at the end keeps every older slot where it was, so a save from before
/// still loads. Taking a name out or reordering them moves slots, and the
/// game reads the old save under the old schema and writes what it wants to
/// keep by name. [version] says which schema a save was written under.
final class ProgressSchema {
  factory ProgressSchema({
    required int version,
    required List<ProgressName> names,
  }) {
    if (version < 1 || version > 0xFFFF) {
      throw ArgumentError.value(version, 'version', 'is outside 1 to 65535');
    }
    final places = <String, Place>{};
    var slots = 0;
    for (final name in names.whereType<Whole>()) {
      _claim(places, name.name, (slot: slots++, low: 0, bits: 32));
    }
    if (slots > _maxSlots) {
      throw ArgumentError.value(slots, 'names', 'hold too many wholes');
    }
    final wholes = Map.of(places);
    for (final slice in names.whereType<Slice>()) {
      _claim(places, slice.name, _slicePlace(slice, wholes, places));
    }
    return ProgressSchema._(version, slots, places);
  }

  ProgressSchema._(this.version, this.slotCount, this._places);

  final int version;

  /// How many numbers a table holds.
  final int slotCount;
  final Map<String, Place> _places;

  bool has(String name) => _places.containsKey(name);

  /// Where [name] lives. Throws [ArgumentError] for a name not in the schema.
  Place placeOf(String name) =>
      _places[name] ?? (throw ArgumentError.value(name, 'name', 'is unknown'));

  static void _claim(Map<String, Place> places, String name, Place place) {
    if (name.isEmpty) throw ArgumentError.value(name, 'name', 'is empty');
    if (places.containsKey(name)) {
      throw ArgumentError.value(name, 'name', 'is used twice');
    }
    places[name] = place;
  }

  static Place _slicePlace(
    Slice slice,
    Map<String, Place> wholes,
    Map<String, Place> placed,
  ) {
    final parent = wholes[slice.of];
    if (parent == null) {
      throw ArgumentError.value(slice.of, slice.name, 'is not a whole');
    }
    if (slice.bits < 1 || slice.low < 0 || slice.low + slice.bits > 32) {
      throw ArgumentError.value(
        '${slice.low}+${slice.bits}',
        slice.name,
        'does not fit in 32 bits',
      );
    }
    final place = (slot: parent.slot, low: slice.low, bits: slice.bits);
    for (final MapEntry(:key, value: other) in placed.entries) {
      if (wholes.containsKey(key) || other.slot != place.slot) continue;
      if (placeMask(other) & placeMask(place) != 0) {
        throw ArgumentError.value(slice.name, 'name', 'overlaps another slice');
      }
    }
    return place;
  }
}

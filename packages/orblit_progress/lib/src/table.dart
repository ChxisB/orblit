import 'dart:typed_data';

import 'schema.dart';

const int _all = 0xFFFFFFFF;
const int _tableFormat = 1;

/// The slots a write changed, with the new value of each.
///
/// A slot holds a whole and every slice of it, so a delta carries slots and
/// not names.
final class Changes {
  Changes._(this._values);

  /// Reads what [encode] wrote, or returns null when [bytes] are not that,
  /// name a slot [schema] does not have, or name one twice.
  static Changes? tryDecode(ProgressSchema schema, Uint8List bytes) {
    if (bytes.length < 2) return null;
    final data = ByteData.sublistView(bytes);
    final count = data.getUint16(0, Endian.little);
    if (bytes.length != 2 + 6 * count) return null;
    final values = <int, int>{};
    for (var i = 0; i < count; i++) {
      final slot = data.getUint16(2 + 6 * i, Endian.little);
      if (slot >= schema.slotCount) return null;
      values[slot] = data.getUint32(4 + 6 * i, Endian.little);
    }
    return values.length == count ? Changes._(values) : null;
  }

  final Map<int, int> _values;

  bool get isEmpty => _values.isEmpty;

  /// The slots that changed, in order.
  Iterable<int> get slots => _values.keys;

  /// The new value of [slot], or null if it did not change.
  int? valueOf(int slot) => _values[slot];

  Uint8List encode() {
    final bytes = Uint8List(2 + 6 * _values.length);
    final data = ByteData.sublistView(bytes)
      ..setUint16(0, _values.length, Endian.little);
    var at = 2;
    for (final MapEntry(:key, :value) in _values.entries) {
      data
        ..setUint16(at, key, Endian.little)
        ..setUint32(at + 2, value, Endian.little);
      at += 6;
    }
    return bytes;
  }

  @override
  bool operator ==(Object other) =>
      other is Changes &&
      other._values.length == _values.length &&
      _values.entries.every((e) => other._values[e.key] == e.value);

  @override
  int get hashCode => Object.hashAllUnordered(
    _values.entries.map((e) => Object.hash(e.key, e.value)),
  );
}

/// One player's named integers. A table never changes: a write gives a new
/// one.
final class ProgressTable {
  ProgressTable(this.schema) : _slots = Uint32List(schema.slotCount);

  ProgressTable._(this.schema, this._slots);

  /// Reads what [encode] wrote. Names added to [schema] since the save was
  /// made start at zero. Throws [FormatException] for bytes that are not a
  /// table, or that were written under a later schema.
  factory ProgressTable.decode(ProgressSchema schema, Uint8List bytes) {
    if (bytes.length < 5) throw const FormatException('Too short for a table.');
    final data = ByteData.sublistView(bytes);
    final count = data.getUint16(3, Endian.little);
    if (data.getUint8(0) != _tableFormat) {
      throw FormatException('Unknown table format ${data.getUint8(0)}.');
    }
    if (data.getUint16(1, Endian.little) > schema.version ||
        count > schema.slotCount ||
        bytes.length != 5 + 4 * count) {
      throw const FormatException('Not a table of this schema.');
    }
    final slots = Uint32List(schema.slotCount);
    for (var i = 0; i < count; i++) {
      slots[i] = data.getUint32(5 + 4 * i, Endian.little);
    }
    return ProgressTable._(schema, slots);
  }

  final ProgressSchema schema;
  final Uint32List _slots;

  /// The value of [name], read from its own bits.
  int get(String name) {
    final place = schema.placeOf(name);
    return (_slots[place.slot] & placeMask(place)) >> place.low;
  }

  /// A table with [name] set to [value], and what changed. Other names that
  /// share bits with [name] keep theirs. Throws [RangeError] when [value] does
  /// not fit the name's bits.
  ({ProgressTable table, Changes changes}) set(String name, int value) {
    final place = schema.placeOf(name);
    final mask = placeMask(place) >> place.low;
    if (value < 0 || value > mask) {
      throw RangeError.range(value, 0, mask, 'value', '$name holds 0 to $mask');
    }
    final next = Uint32List.fromList(_slots);
    next[place.slot] =
        (_slots[place.slot] & ~placeMask(place) & _all) | (value << place.low);
    final table = ProgressTable._(schema, next);
    return (table: table, changes: table.changesSince(this));
  }

  /// The slots whose value differs from [older]'s.
  Changes changesSince(ProgressTable older) {
    final values = <int, int>{};
    for (var slot = 0; slot < _slots.length; slot++) {
      if (_slots[slot] != older._slots[slot]) values[slot] = _slots[slot];
    }
    return Changes._(values);
  }

  /// A table with [changes] written over this one.
  ProgressTable apply(Changes changes) {
    final next = Uint32List.fromList(_slots);
    for (final MapEntry(:key, :value) in changes._values.entries) {
      next[key] = value;
    }
    return ProgressTable._(schema, next);
  }

  Uint8List encode() {
    final bytes = Uint8List(5 + 4 * _slots.length);
    final data = ByteData.sublistView(bytes)
      ..setUint8(0, _tableFormat)
      ..setUint16(1, schema.version, Endian.little)
      ..setUint16(3, _slots.length, Endian.little);
    for (var i = 0; i < _slots.length; i++) {
      data.setUint32(5 + 4 * i, _slots[i], Endian.little);
    }
    return bytes;
  }
}

import 'dart:math' as math;

import '../pad.dart';

/// One thing a player can press, push or touch.
///
/// A key is named by a string the game supplies. Any string works as long as
/// the same key always gives the same one. The physical key, such as `KeyW`,
/// keeps a binding in place when the keyboard layout changes. A touch region
/// is a name too, for a rectangle the game draws.
sealed class Control {
  const Control();

  /// What makes two controls the same one, within a kind.
  Object get _identity;

  /// The control as JSON, for a saved profile.
  Map<String, Object?> toJson() => switch (this) {
    KeyControl(:final key) => {'key': key},
    PadButtonControl(:final button) => {'padButton': button.name},
    PadAxisControl(:final axis, :final side) => {side.jsonKey: axis.name},
    PadStickControl(:final stick) => {'padStick': stick.name},
    TouchControl(:final region) => {'touch': region},
  };

  /// Reads what [toJson] wrote. Throws a [FormatException] for anything else.
  static Control fromJson(Object? json) {
    if (json is Map<String, Object?> && json.length == 1) {
      final MapEntry(:key, :value) = json.entries.single;
      final control = value is String ? _named(key, value) : null;
      if (control != null) return control;
    }
    throw FormatException('This is not a control: $json');
  }

  @override
  bool operator ==(Object other) =>
      other is Control &&
      other.runtimeType == runtimeType &&
      other._identity == _identity;

  @override
  int get hashCode => Object.hash(runtimeType, _identity);

  @override
  String toString() => '$runtimeType($_identity)';
}

Control? _named(String kind, String name) => switch (kind) {
  'key' => KeyControl(name),
  'padButton' => _find(PadButton.values, name, PadButtonControl.new),
  'padAxis' => _axis(name, AxisSide.both),
  'padAxisPositive' => _axis(name, AxisSide.positive),
  'padAxisNegative' => _axis(name, AxisSide.negative),
  'padStick' => _find(PadStick.values, name, PadStickControl.new),
  'touch' => TouchControl(name),
  _ => null,
};

Control? _axis(String name, AxisSide side) =>
    _find(PadAxis.values, name, (axis) => PadAxisControl(axis, side: side));

Control? _find<E extends Enum>(
  List<E> values,
  String name,
  Control Function(E) make,
) {
  final value = values.asNameMap()[name];
  return value == null ? null : make(value);
}

/// A key, by the name the game gives it.
final class KeyControl extends Control {
  const KeyControl(this.key);

  final String key;

  @override
  Object get _identity => key;
}

/// A button on a pad.
final class PadButtonControl extends Control {
  const PadButtonControl(this.button);

  final PadButton button;

  @override
  Object get _identity => button;
}

/// Which way along an axis a [PadAxisControl] reads.
enum AxisSide {
  /// Both ways, from -1 to 1.
  both('padAxis'),

  /// Only the push above the middle, from 0 to 1.
  positive('padAxisPositive'),

  /// Only the push below the middle, from 0 to 1.
  negative('padAxisNegative');

  const AxisSide(this.jsonKey);

  /// The key a saved control is written under.
  final String jsonKey;

  /// [value] as this side reads it.
  double read(double value) => switch (this) {
    both => value,
    positive => math.max(0, value),
    negative => math.max(0, -value),
  };
}

/// One axis of a pad, such as a trigger or half of a stick.
///
/// A half is a control like a key: it reads 0 to 1, and the binding's scale
/// says which way the action goes. That keeps a rebound direction right when
/// a player pushes a stick left for "steer left".
final class PadAxisControl extends Control {
  const PadAxisControl(this.axis, {this.side = AxisSide.both});

  final PadAxis axis;
  final AxisSide side;

  @override
  Object get _identity => (axis, side);
}

/// A whole stick, as the pair of axes it is. Only a two-axis action takes one.
final class PadStickControl extends Control {
  const PadStickControl(this.stick);

  final PadStick stick;

  @override
  Object get _identity => stick;
}

/// A named region of a touch screen.
final class TouchControl extends Control {
  const TouchControl(this.region);

  final String region;

  @override
  Object get _identity => region;
}

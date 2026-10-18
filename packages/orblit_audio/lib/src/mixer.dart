/// [value] as a linear gain, or an [ArgumentError] for a negative or
/// non-finite one.
double checkedGain(double value, String name) {
  if (!value.isFinite || value < 0) {
    throw ArgumentError.value(value, name, 'must be zero or more');
  }
  return value;
}

/// [value] as a length of time, or an [ArgumentError] for a negative or
/// non-finite one.
double checkedSeconds(double value, String name) {
  if (!value.isFinite || value < 0) {
    throw ArgumentError.value(value, name, 'must be zero or more');
  }
  return value;
}

/// A node in the mixer tree. A voice plays on a bus and every bus above it
/// scales what is heard.
final class Bus {
  Bus._(this.name, this.parent);

  final String name;

  /// Null for the master bus.
  final Bus? parent;

  double _volume = 1;

  bool muted = false;

  double get volume => _volume;

  set volume(double value) => _volume = checkedGain(value, 'volume');

  /// What this bus and every bus above it do to a voice. A mute anywhere in
  /// the chain silences it.
  double get gain => muted ? 0 : _volume * (parent?.gain ?? 1);
}

/// The tree of buses, with the master bus at the root.
final class Mixer {
  Mixer() : master = Bus._('master', null) {
    _buses[master.name] = master;
  }

  final Bus master;

  final Map<String, Bus> _buses = {};

  Bus bus(String name) =>
      _buses[name] ??
      (throw ArgumentError.value(name, 'name', 'there is no such bus'));

  /// Whether [bus] is one of this mixer's.
  bool owns(Bus bus) => identical(_buses[bus.name], bus);

  /// Adds a bus named [name] below [under], or below the master bus.
  Bus add(String name, {Bus? under}) {
    if (_buses.containsKey(name)) {
      throw ArgumentError.value(name, 'name', 'there is already a bus');
    }
    final parent = under ?? master;
    if (!owns(parent)) {
      throw ArgumentError.value(under, 'under', 'belongs to another mixer');
    }
    return _buses[name] = Bus._(name, parent);
  }
}

import 'backend.dart';
import 'layout.dart';
import 'pad.dart';
import 'snapshot.dart';

/// One button as the W3C Gamepad API reports it.
typedef GamepadButtonReading = ({bool pressed, double value});

/// One pad as the W3C Gamepad API reports it, as plain data.
///
/// Plain so that everything between the browser and a [PadState] (the id
/// parsing, the numbering, the scaling, the hot-plug) is exercised on the VM
/// against made-up readings, and only the few lines that touch `navigator`
/// need a browser.
final class GamepadReading {
  const GamepadReading({
    required this.index,
    required this.id,
    required this.buttons,
    required this.axes,
    this.connected = true,
    this.standard = false,
    this.canRumble = false,
  });

  /// The browser's number for this pad. Stays the same when it is replugged.
  final int index;

  /// Whatever the browser calls it. Its shape differs by browser.
  final String id;

  /// Whether the browser says the buttons and axes are in the standard order.
  final bool standard;

  final bool connected;
  final List<GamepadButtonReading> buttons;
  final List<double> axes;

  /// Whether the browser offers a dual-rumble motor pair.
  final bool canRumble;
}

/// What [GamepadApiPads] asks of the platform, and the only part of it that
/// is not plain Dart.
abstract interface class GamepadSource {
  /// Every slot the browser has, with an empty one as null.
  List<GamepadReading?> read();

  void rumble(
    int index, {
    required double strong,
    required double weak,
    required Duration duration,
  });
}

/// How a pad's numbered buttons and axes become the codes [PadLayout] knows.
///
/// Data, so an odd pad is one entry in a map. The default is the standard
/// order the Gamepad API promises when a browser recognises a pad, and an
/// entry is written only for a pad that is not recognised. Every entry is a
/// claim about hardware, which is why none ship in this package.
final class PadMapping {
  const PadMapping({
    this.buttons = const {},
    this.axes = const {},
    this.triggers = const {},
  });

  /// The order the Gamepad API calls "standard".
  static const PadMapping standard = PadMapping(
    buttons: {
      0: 0x130, // south
      1: 0x131, // east
      2: 0x134, // west
      3: 0x133, // north
      4: 0x136, // left shoulder
      5: 0x137, // right shoulder
      8: 0x13a, // back
      9: 0x13b, // start
      10: 0x13d, // left stick press
      11: 0x13e, // right stick press
      12: 0x220, // d-pad up
      13: 0x221, // d-pad down
      14: 0x222, // d-pad left
      15: 0x223, // d-pad right
      16: 0x13c, // guide
    },
    axes: {
      0: 0x00, // left X
      1: 0x01, // left Y
      2: 0x03, // right X
      3: 0x04, // right Y
    },
    triggers: {
      6: 0x02, // left trigger, read from the button's value
      7: 0x05, // right trigger
    },
  );

  /// The key a table of mappings uses for a pad, the same one the layout
  /// table uses.
  static int keyOf(int vendor, int product) => (vendor << 16) | product;

  /// Button index to the code it reports as. A button missing here is ignored.
  final Map<int, int> buttons;

  /// Stick axis index to the code it reports as.
  final Map<int, int> axes;

  /// Button index to the axis code its analogue value reports as. A trigger is
  /// a button in this API, and a button with a value rather than a switch.
  final Map<int, int> triggers;

  /// The scale every axis is reported on. Wide enough that nothing is lost,
  /// and symmetric so that a stick at rest is exactly zero.
  static const int range = 32767;

  /// What each axis can report, for [PadDevice.ranges].
  Map<int, PadAxisRange> get ranges => {
    for (final code in axes.values)
      code: const PadAxisRange(minimum: -range, maximum: range),
    for (final code in triggers.values)
      code: const PadAxisRange(minimum: 0, maximum: range),
  };

  /// [reading] in the numbering [PadLayout] reads. A button or axis the
  /// reading does not have is left out, so a table written for a pad with more
  /// controls than this one reports does no harm.
  PadSnapshot snapshotOf(GamepadReading reading) {
    final down = <int>{};
    for (final entry in buttons.entries) {
      final at = entry.key;
      if (at < reading.buttons.length && reading.buttons[at].pressed) {
        down.add(entry.value);
      }
    }
    final moved = <int, int>{};
    for (final entry in axes.entries) {
      if (entry.key < reading.axes.length) {
        moved[entry.value] = _scaled(reading.axes[entry.key], -1);
      }
    }
    for (final entry in triggers.entries) {
      if (entry.key < reading.buttons.length) {
        moved[entry.value] = _scaled(reading.buttons[entry.key].value, 0);
      }
    }
    return (buttons: down, axes: moved);
  }

  static int _scaled(double value, int low) =>
      // A reading that is not a number is a pad at rest, not a fault.
      value.isNaN ? 0 : (value.clamp(low, 1) * range).round();
}

/// Pads through the W3C Gamepad API, which is how a browser offers them.
///
/// The browser numbers the buttons and gives no stream of changes, so each
/// pad becomes a [SnapshotPad] and the numbers become evdev codes through a
/// [PadMapping]. Nothing above this knows it is not on Linux.
///
/// A browser hides every pad until the player presses a button on one, so a
/// page that polls at load sees none and then sees one arrive.
final class GamepadApiPads implements PadBackend {
  GamepadApiPads(this._source, {this.mappings = const {}});

  final GamepadSource _source;

  /// Mappings for pads the browser does not recognise, by [PadMapping.keyOf].
  /// A pad in neither this nor the browser's standard order is read as if it
  /// were in the standard order, which is a guess.
  final Map<int, PadMapping> mappings;

  final Map<int, _Open> _open = {};

  @override
  List<PadDevice> scan() {
    final current = {
      for (final reading in _source.read())
        if (reading != null && reading.connected) reading.index: reading,
    };

    for (final index in _open.keys.toList()) {
      final reading = current[index];
      if (reading == null || reading.id != _open[index]?.id) {
        _open.remove(index)?.latest = null;
      }
    }
    for (final reading in current.values) {
      final known = _open[reading.index];
      if (known == null) {
        _open[reading.index] = _opened(reading);
      } else {
        known.latest = reading;
      }
    }
    return [for (final open in _open.values) open.pad];
  }

  _Open _opened(GamepadReading reading) {
    final identity = padIdentityOf(reading.id, reading.index);
    final mapping =
        mappings[PadMapping.keyOf(identity.vendor, identity.product)] ??
        PadMapping.standard;
    final open = _Open(reading.id)..latest = reading;
    open.pad = SnapshotPad(
      identity: identity,
      ranges: mapping.ranges,
      alive: () => open.latest != null,
      read: () {
        final latest = open.latest;
        return latest == null
            ? (buttons: <int>{}, axes: <int, int>{})
            : mapping.snapshotOf(latest);
      },
      onRumble: reading.canRumble
          ? ({required strong, required weak, required duration}) =>
                _source.rumble(
                  reading.index,
                  strong: strong,
                  weak: weak,
                  duration: duration,
                )
          : null,
    );
    return open;
  }

  @override
  void close() => _open.clear();
}

/// One pad the browser has shown, and the reading from the latest scan. Null
/// once it has gone, which is how its device learns it is not alive.
final class _Open {
  _Open(this.id);

  final String id;
  GamepadReading? latest;
  late final SnapshotPad pad;
}

final RegExp _vendorProduct = RegExp(
  r'Vendor:\s*([0-9a-f]{1,4})\s+Product:\s*([0-9a-f]{1,4})',
  caseSensitive: false,
);
final RegExp _dashed = RegExp(
  r'^([0-9a-f]{1,4})-([0-9a-f]{1,4})-(.+)$',
  caseSensitive: false,
);
final RegExp _tail = RegExp(r'\s*\([^()]*\)\s*$');

/// A pad's identity from the id a browser gives it.
///
/// Chrome writes `Name (STANDARD GAMEPAD Vendor: 054c Product: 09cc)` and
/// Firefox writes `054c-09cc-Name`, and some Chrome pads carry no numbers at
/// all. What is not in the id is left at nought, which sends the layout to its
/// name table and then to the standard codes.
PadIdentity padIdentityOf(String id, int index) {
  final dashed = _dashed.firstMatch(id);
  final numbers = dashed ?? _vendorProduct.firstMatch(id);
  final named = dashed?.group(3) ?? id.replaceFirst(_tail, '');
  return PadIdentity(
    name: named.trim().isEmpty ? id : named.trim(),
    vendor: int.parse(numbers?.group(1) ?? '0', radix: 16),
    product: int.parse(numbers?.group(2) ?? '0', radix: 16),
    path: 'gamepad:$index',
  );
}

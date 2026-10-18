import 'dart:typed_data';

import 'backend.dart';
import 'evdev.dart';
import 'layout.dart';
import 'pad.dart';

/// Called to ask a pad's motors to run. Magnitudes are already from nought to
/// one.
typedef RumbleFunction =
    void Function({
      required double strong,
      required double weak,
      required Duration duration,
    });

/// A pad that can only be asked where it stands.
///
/// A browser, like most platform gamepad APIs, hands over the whole state each
/// time and no stream of changes. This turns the difference between two
/// snapshots into the bytes evdev would have written, so [Pads] folds in a
/// pad of this kind exactly as it folds a Linux one: the same layouts, the
/// same shaping and the same edges, with nothing above it knowing the
/// difference.
///
/// The price is the one every snapshot has. A button pressed and released
/// between two polls leaves no trace, because nothing was there to see it.
final class SnapshotPad implements PadDevice {
  SnapshotPad({
    required this.identity,
    required PadSnapshot Function() read,
    required bool Function() alive,
    this.ranges = const {},
    RumbleFunction? onRumble,
  }) : _read = read,
       _alive = alive,
       _onRumble = onRumble;

  @override
  final PadIdentity identity;

  /// Nothing is described up front: a snapshot has no capability bitmap. The
  /// layout falls back on vendor, product and name, which is all a platform
  /// with no kernel behind it can offer.
  @override
  EvdevCapabilities get capabilities => EvdevCapabilities.none;

  @override
  final Map<int, PadAxisRange> ranges;

  final PadSnapshot Function() _read;
  final bool Function() _alive;
  final RumbleFunction? _onRumble;

  /// What the last drain saw, so the next can say what changed.
  PadSnapshot _last = (buttons: <int>{}, axes: <int, int>{});

  @override
  bool get isAlive => _alive();

  @override
  bool get canRumble => _onRumble != null;

  @override
  void rumble({
    required double strong,
    required double weak,
    required Duration duration,
  }) => _onRumble?.call(strong: strong, weak: weak, duration: duration);

  @override
  Uint8List drain() {
    if (!isAlive) return Uint8List(0);
    final now = _read();
    final out = BytesBuilder(copy: false);
    for (final code in now.buttons.difference(_last.buttons)) {
      out.add(encodeEvdevEvent(evKey, code, 1));
    }
    for (final code in _last.buttons.difference(now.buttons)) {
      out.add(encodeEvdevEvent(evKey, code, 0));
    }
    for (final axis in now.axes.entries) {
      if (_last.axes[axis.key] != axis.value) {
        out.add(encodeEvdevEvent(evAbs, axis.key, axis.value));
      }
    }
    _last = now;
    if (out.isEmpty) return Uint8List(0);
    out.add(encodeEvdevEvent(evSyn, synReport, 0));
    return out.takeBytes();
  }

  /// Read afresh and not remembered, so the drain that follows still reports a
  /// button already held. [Pads] ignores a press it already knows about, and
  /// the alternative, remembering here, would make a query change what the
  /// next drain says.
  @override
  PadSnapshot resync() => _alive() ? _read() : (buttons: {}, axes: {});

  @override
  void close() {}
}

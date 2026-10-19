import 'dart:js_interop';
import 'dart:js_interop_unsafe';

import 'package:web/web.dart' as web;

import 'gamepad_api.dart';

/// The browser's pads, through `navigator.getGamepads()`.
///
/// Everything here is a copy into plain data, made at the moment of the call.
/// Chrome hands out snapshots and Firefox hands out live objects, and a copy
/// reads the same under both.
final class WebGamepads implements GamepadSource {
  const WebGamepads();

  @override
  List<GamepadReading?> read() {
    final pads = web.window.navigator.getGamepads().toDart;
    return [
      for (final pad in pads)
        if (pad == null) null else _reading(pad),
    ];
  }

  @override
  void rumble(
    int index, {
    required double strong,
    required double weak,
    required Duration duration,
  }) {
    final pads = web.window.navigator.getGamepads().toDart;
    final pad = index < pads.length ? pads[index] : null;
    final actuator = pad == null ? null : _actuatorOf(pad);
    if (actuator == null) return;
    // A rejected effect is a pad that went away between the call and the
    // browser acting on it, which is not worth telling the game about.
    actuator
        .playEffect(
          'dual-rumble',
          web.GamepadEffectParameters(
            duration: duration.inMilliseconds,
            strongMagnitude: strong,
            weakMagnitude: weak,
          ),
        )
        .toDart
        .ignore();
  }

  static GamepadReading _reading(web.Gamepad pad) => GamepadReading(
    index: pad.index,
    id: pad.id,
    connected: pad.connected,
    standard: pad.mapping == 'standard',
    canRumble: _actuatorOf(pad) != null,
    buttons: [
      for (final button in pad.buttons.toDart)
        (pressed: button.pressed, value: button.value),
    ],
    axes: [for (final axis in pad.axes.toDart) axis.toDartDouble],
  );

  /// The dual-rumble actuator, if the pad has one. `package:web` has no
  /// binding for `vibrationActuator`, and the older `hapticActuators` only
  /// does a single pulse.
  static web.GamepadHapticActuator? _actuatorOf(web.Gamepad pad) {
    final actuator = pad['vibrationActuator'];
    if (actuator == null || actuator.isUndefinedOrNull) return null;
    final found = actuator as web.GamepadHapticActuator;
    return (found as JSObject)['type'].dartify() == 'dual-rumble'
        ? found
        : null;
  }
}

import 'backend.dart';
import 'gamepad_api.dart';
import 'web.dart';

/// The browser's pads.
PadBackend platformBackend() => GamepadApiPads(const WebGamepads());

// The other half of the conditional export of LinuxPads in orblit_input.dart,
// for a platform that cannot import dart:ffi at all. It fails where the
// choice is made, the way LinuxPads does anywhere that is not Linux.
import 'backend.dart';

/// Gamepads on a Linux machine, which this platform is not.
///
/// Open, like the class it stands in for, and with the same constructor so a
/// call site compiles on both.
class LinuxPads implements PadBackend {
  LinuxPads({this.devices = '/dev/input', this.sysfs = '/sys/class/input'}) {
    throw UnsupportedError('LinuxPads reads evdev, which is Linux only');
  }

  final String devices;
  final String sysfs;

  @override
  List<PadDevice> scan() => const [];

  @override
  void close() {}
}

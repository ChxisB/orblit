import '../shaping.dart';
import 'control.dart';

/// Which half of a two-axis action a binding feeds.
enum AxisPart { x, y }

/// A control wired to an action.
///
/// The control's value is shaped, then multiplied by [scale]. A key on the
/// left of a steering axis has a scale of -1.
final class Binding {
  const Binding(
    this.control, {
    this.scale = 1,
    this.part = AxisPart.x,
    this.shaping = Shaping.none,
  });

  final Control control;

  /// Multiplies the shaped value. Negative flips the direction.
  final double scale;

  /// The half of a two-axis action this feeds. Other actions ignore it, and so
  /// does a whole stick, which feeds both.
  final AxisPart part;

  /// A dead zone and curve on top of any the pad already applied.
  final Shaping shaping;

  /// This binding on another control.
  ///
  /// The scale, part and shaping stay, so a player who rebinds a key does not
  /// change which way it steers. A whole stick has no half of its own, so
  /// moving to or from one starts again on x. A both-ways axis carries its
  /// own sign, which a key or a half does not, so moving to or from one
  /// starts again at a scale of 1. [scale] and [part] replace what would
  /// stay.
  Binding withControl(Control control, {double? scale, AxisPart? part}) {
    final restartsPart =
        (control is PadStickControl) != (this.control is PadStickControl);
    final restartsScale =
        restartsPart || _bothWays(control) != _bothWays(this.control);
    return Binding(
      control,
      scale: scale ?? (restartsScale ? 1 : this.scale),
      part: part ?? (restartsPart ? AxisPart.x : this.part),
      shaping: shaping,
    );
  }
}

bool _bothWays(Control control) =>
    control is PadAxisControl && control.side == AxisSide.both;

import 'dart:async';

import 'package:flutter/widgets.dart';

/// Where an element sends what it has seen. Null when script named no handler.
typedef TouchSend = void Function(Object? payload);

/// How big a touch element is when nothing in its style or its parent says.
const double _fallback = 120;

/// Where [local] sits in the circle that fills [size], from -1 to 1 each way
/// with y down, brought back to the circle when it is outside.
Offset stickAt(Offset local, Size size) {
  final radius = size.shortestSide / 2;
  if (radius <= 0) return Offset.zero;
  final offset = (local - size.center(Offset.zero)) / radius;
  return offset.distance > 1 ? offset / offset.distance : offset;
}

// Every element here listens to raw pointer events and claims the first
// finger that lands on it. A gesture recogniser would put each finger into an
// arena, and two elements in two arenas is what a thumb on a stick and
// another on a jump button must not be.

/// A thumb stick. Sends `[x, y]`, each from -1 to 1 and positive up and to
/// the right, when it changes. A stick let go sends `[0.0, 0.0]`.
class TouchStick extends StatefulWidget {
  const TouchStick({required this.send, required this.knob, super.key});

  final TouchSend? send;

  /// The colour of the knob. The base is whatever the element's style draws.
  final Color knob;

  @override
  State<TouchStick> createState() => _TouchStickState();
}

class _TouchStickState extends State<TouchStick> {
  int? _pointer;
  Offset _at = Offset.zero;

  void _push(Offset local) {
    final size = context.size;
    if (size == null) return;
    setState(() => _at = stickAt(local, size));
    widget.send?.call(<double>[_at.dx, -_at.dy]);
  }

  void _let(PointerEvent event) {
    if (event.pointer != _pointer) return;
    _pointer = null;
    setState(() => _at = Offset.zero);
    widget.send?.call(const <double>[0, 0]);
  }

  @override
  void dispose() {
    final send = widget.send;
    // After the frame, because a host may rebuild when it hears this.
    if (_pointer != null && send != null) {
      scheduleMicrotask(() => send(const <double>[0, 0]));
    }
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => Listener(
    behavior: HitTestBehavior.opaque,
    onPointerDown: (event) {
      if (_pointer != null) return;
      _pointer = event.pointer;
      _push(event.localPosition);
    },
    onPointerMove: (event) {
      if (event.pointer == _pointer) _push(event.localPosition);
    },
    onPointerUp: _let,
    onPointerCancel: _let,
    child: SizedBox(
      width: _fallback,
      height: _fallback,
      child: Align(
        alignment: Alignment(_at.dx, _at.dy),
        child: FractionallySizedBox(
          widthFactor: 0.4,
          heightFactor: 0.4,
          child: DecoratedBox(
            decoration: BoxDecoration(
              color: widget.knob,
              shape: BoxShape.circle,
            ),
          ),
        ),
      ),
    ),
  );
}

/// A button held under a finger. Sends `true` when a finger lands and `false`
/// when it lifts, slides off the screen or is taken by the system.
class TouchHold extends StatefulWidget {
  const TouchHold({required this.send, required this.child, super.key});

  final TouchSend? send;
  final Widget child;

  @override
  State<TouchHold> createState() => _TouchHoldState();
}

class _TouchHoldState extends State<TouchHold> {
  int? _pointer;

  void _let(PointerEvent event) {
    if (event.pointer != _pointer) return;
    setState(() => _pointer = null);
    widget.send?.call(false);
  }

  @override
  void dispose() {
    final send = widget.send;
    if (_pointer != null && send != null) {
      scheduleMicrotask(() => send(false));
    }
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => Listener(
    behavior: HitTestBehavior.opaque,
    onPointerDown: (event) {
      if (_pointer != null) return;
      setState(() => _pointer = event.pointer);
      widget.send?.call(true);
    },
    onPointerUp: _let,
    onPointerCancel: _let,
    child: Opacity(opacity: _pointer == null ? 1 : 0.6, child: widget.child),
  );
}

/// An area a finger drags across. Sends `[dx, dy]` for each move, in logical
/// pixels and positive up and to the right. It fills the space it is given,
/// and is a square when it is given none.
class TouchSwipe extends StatefulWidget {
  const TouchSwipe({required this.send, super.key});

  final TouchSend? send;

  @override
  State<TouchSwipe> createState() => _TouchSwipeState();
}

class _TouchSwipeState extends State<TouchSwipe> {
  int? _pointer;

  void _let(PointerEvent event) {
    if (event.pointer == _pointer) _pointer = null;
  }

  @override
  Widget build(BuildContext context) => Listener(
    behavior: HitTestBehavior.opaque,
    onPointerDown: (event) => _pointer ??= event.pointer,
    onPointerMove: (event) {
      if (event.pointer != _pointer || event.delta == Offset.zero) return;
      widget.send?.call(<double>[event.delta.dx, -event.delta.dy]);
    },
    onPointerUp: _let,
    onPointerCancel: _let,
    child: LayoutBuilder(
      builder: (context, constraints) => SizedBox(
        width: constraints.hasBoundedWidth ? constraints.maxWidth : _fallback,
        height: constraints.hasBoundedHeight
            ? constraints.maxHeight
            : _fallback,
      ),
    ),
  );
}

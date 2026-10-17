import 'package:vector_math/vector_math_64.dart';

import 'binding.dart';
import 'binding_table.dart';
import 'button_track.dart';
import 'control.dart';
import 'control_source.dart';
import 'input_action.dart';
import 'input_context.dart';

/// The things a game's player can do, and which controls do them.
///
/// The game names its actions and groups their default bindings into
/// contexts. Once a frame it calls [update] with what the devices hold, then
/// asks about actions by name. It never asks about a key.
///
/// Contexts stack. The one switched on last reads first. An action that a
/// higher context binds uses that context's bindings. A higher context that
/// blocks hides every action it does not bind, and everything under it.
final class ActionMap {
  ActionMap({
    required List<InputAction> actions,
    required List<InputContext> contexts,
    List<String> active = const [],
  }) : _actions = _byName(actions),
       bindings = BindingTable(
         kinds: {for (final action in actions) action.name: action.kind},
         contexts: contexts,
       ) {
    for (final action in actions) {
      switch (action.kind) {
        case ActionKind.button:
          _buttons[action.name] = ButtonTrack();
        case ActionKind.axis:
          _values[action.name] = 0;
        case ActionKind.axis2:
          _vectors[action.name] = Vector2.zero();
      }
    }
    active.forEach(activate);
  }

  final Map<String, InputAction> _actions;

  /// The bindings, for rebinding and for saving.
  final BindingTable bindings;

  final List<String> _stack = [];
  final Map<String, ButtonTrack> _buttons = {};
  final Map<String, double> _values = {};
  final Map<String, Vector2> _vectors = {};

  /// Whether [context] is switched on.
  bool isActive(String context) => _stack.contains(context);

  /// Switches [context] on, above every other.
  void activate(String context) {
    if (!bindings.hasContext(context)) {
      throw ArgumentError('There is no context called "$context".');
    }
    _stack
      ..remove(context)
      ..add(context);
  }

  /// Switches [context] off. Does nothing when it is off already.
  void deactivate(String context) => _stack.remove(context);

  /// Reads one frame of the devices, at [now].
  ///
  /// The clock is passed in so that a replay and a test decide what time it
  /// is. Call it once a frame, before asking about any action.
  void update(ControlSource source, {required Duration now}) {
    final shown = _shown();
    for (final action in _actions.values) {
      final list = shown[action.name];
      switch (action.kind) {
        case ActionKind.button:
          _buttons[action.name]!.advance(
            down: list?.any((b) => _scalar(b, source) >= pressPoint),
            now: now,
            gestures: action.gestures,
          );
        case ActionKind.axis:
          _values[action.name] = list == null ? 0 : _sum(list, source);
        case ActionKind.axis2:
          _vectors[action.name] = list == null
              ? Vector2.zero()
              : _combine(list, source);
      }
    }
  }

  /// Whether the button is held.
  bool isDown(String action) => _button(action).isDown;

  /// Whether the button went down in the last [update].
  bool wasPressed(String action) => _button(action).wasPressed;

  /// Whether the button came up in the last [update], or was hidden while
  /// down.
  bool wasReleased(String action) => _button(action).wasReleased;

  /// Whether the button reached its hold time in the last [update]. True
  /// once per press.
  bool wasHeld(String action) => _button(action).wasHeld;

  /// Whether the button came up in the last [update] after a short press.
  bool wasTapped(String action) => _button(action).wasTapped;

  /// Whether the button went down in the last [update] soon after a tap came
  /// up.
  bool wasDoubleTapped(String action) => _button(action).wasDoubleTapped;

  /// An axis, from -1 to 1.
  double value(String action) =>
      _values[action] ?? (throw _refuse(action, 'axis'));

  /// A two-axis action, no longer than 1. A copy, so it can be changed.
  Vector2 vector(String action) =>
      (_vectors[action] ?? (throw _refuse(action, 'axis2'))).clone();

  ButtonTrack _button(String action) =>
      _buttons[action] ?? (throw _refuse(action, 'button'));

  Object _refuse(String name, String wanted) {
    final action = _actions[name];
    return action == null
        ? ArgumentError('There is no action called "$name".')
        : StateError('"$name" is ${action.kind.name}, not $wanted.');
  }

  /// What each action reads from now: the first context from the top that
  /// binds it, stopping after one that blocks.
  Map<String, List<Binding>> _shown() {
    final shown = <String, List<Binding>>{};
    for (final context in _stack.reversed) {
      for (final MapEntry(:key, :value)
          in bindings.bindingsIn(context).entries) {
        shown.putIfAbsent(key, () => value);
      }
      if (bindings.blocks(context)) break;
    }
    return shown;
  }
}

Map<String, InputAction> _byName(List<InputAction> actions) {
  final byName = {for (final action in actions) action.name: action};
  if (byName.length != actions.length) {
    throw ArgumentError('Two actions have the same name.');
  }
  return byName;
}

double _scalar(Binding binding, ControlSource source) =>
    binding.shaping.scalar(source.read(binding.control)) * binding.scale;

double _sum(List<Binding> list, ControlSource source) => list
    .fold(0.0, (sum, binding) => sum + _scalar(binding, source))
    .clamp(-1.0, 1.0)
    .toDouble();

// A whole stick feeds both halves. Any other control feeds the half its
// binding names. Keys on two sides of a diagonal would otherwise be faster
// than one, so the sum is brought back to a length of 1.
Vector2 _combine(List<Binding> list, ControlSource source) {
  final sum = Vector2.zero();
  for (final binding in list) {
    if (binding.control case PadStickControl(:final stick)) {
      sum.add(binding.shaping.stick(source.stick(stick)) * binding.scale);
    } else if (binding.part == AxisPart.x) {
      sum.x += _scalar(binding, source);
    } else {
      sum.y += _scalar(binding, source);
    }
  }
  if (sum.length > 1) sum.normalize();
  return sum;
}

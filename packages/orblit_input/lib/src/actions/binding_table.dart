import 'dart:collection';
import 'dart:math' as math;

import 'binding.dart';
import 'control.dart';
import 'input_action.dart';
import 'input_context.dart';

/// A control a rebinding asked for is already bound in that context.
final class BindingClash implements Exception {
  const BindingClash(this.action, this.control);

  /// The action that has [control].
  final String action;

  final Control control;

  String get message => '$control is already bound to "$action".';

  @override
  String toString() => 'BindingClash: $message';
}

/// Which controls are wired to which actions, in each context.
///
/// Starts as the defaults the contexts were made with. A player changes it
/// with [rebind], and [toJson] writes down only what differs from the
/// defaults, so a default the game changes later still reaches a player who
/// never touched that action.
///
/// Only a [rebind] is checked for clashes, and only inside its own context.
/// A game's defaults may give one control to two actions on purpose.
final class BindingTable {
  BindingTable({
    required Map<String, ActionKind> kinds,
    required List<InputContext> contexts,
  }) : _kinds = Map.unmodifiable(kinds),
       _contexts = {for (final context in contexts) context.name: context} {
    if (_contexts.length != contexts.length) {
      throw ArgumentError('Two contexts have the same name.');
    }
    contexts.forEach(_check);
    _bound = _defaults();
  }

  final Map<String, ActionKind> _kinds;
  final Map<String, InputContext> _contexts;
  late Map<String, Map<String, List<Binding>>> _bound;

  /// Whether a context of this name exists.
  bool hasContext(String context) => _contexts.containsKey(context);

  /// Whether the contexts under [context] go quiet while it is on.
  bool blocks(String context) => _context(context).blocks;

  /// What [context] binds now, by action name.
  Map<String, List<Binding>> bindingsIn(String context) =>
      UnmodifiableMapView(_bound[_context(context).name]!);

  /// The action that already has [control] in [context], or null.
  ///
  /// [slot] is the binding of [action] that is about to take [control]. It is
  /// not a clash with itself.
  String? clash(
    String action,
    Control control, {
    required String context,
    int slot = 0,
  }) {
    _slots(context, action);
    return _holder(context, control, action: action, slot: slot)?.action;
  }

  /// Points binding number [slot] of [action] at [control].
  ///
  /// [slot] may be one past the last binding, which adds one. When another
  /// binding in [context] has [control], this throws a [BindingClash] unless
  /// [swap] is set, which gives that binding the control [slot] had.
  void rebind(
    String action,
    Control control, {
    required String context,
    int slot = 0,
    bool swap = false,
  }) {
    final slots = _slots(context, action);
    RangeError.checkValueInInterval(slot, 0, slots.length, 'slot');
    _requireAccepts(action, control);
    final holder = _holder(context, control, action: action, slot: slot);
    if (holder == null) {
      _put(context, action, slot, control);
      return;
    }
    if (!swap) throw BindingClash(holder.action, control);
    if (slot == slots.length) {
      throw ArgumentError.value(
        slot,
        'slot',
        'A new binding has nothing to swap.',
      );
    }
    final given = slots[slot].control;
    _requireAccepts(holder.action, given);
    _put(context, holder.action, holder.slot, given);
    _put(context, action, slot, control);
  }

  /// The controls the player changed, as JSON. Empty contexts are left out.
  Map<String, Object?> toJson() => {
    'contexts': {
      for (final MapEntry(key: context, value: actions) in _bound.entries)
        if (_changed(context, actions) case final changed
            when changed.isNotEmpty)
          context: changed,
    },
  };

  /// Replaces the changes with those in [json], as [toJson] wrote them.
  ///
  /// Names the game no longer has are skipped, and so are controls an action
  /// can no longer take, so an old profile loads into a newer game. Anything
  /// that is not a profile throws a [FormatException] and changes nothing.
  void load(Object? json) {
    final overrides = _parse(json);
    final next = _defaults();
    for (final MapEntry(key: context, value: actions) in overrides.entries) {
      for (final MapEntry(key: action, value: controls) in actions.entries) {
        _override(next, context, action, controls);
      }
    }
    _bound = next;
  }

  InputContext _context(String context) =>
      _contexts[context] ??
      (throw ArgumentError('There is no context called "$context".'));

  List<Binding> _slots(String context, String action) =>
      _bound[_context(context).name]![action] ??
      (throw ArgumentError('"$context" has no bindings for "$action".'));

  void _check(InputContext context) {
    for (final MapEntry(key: action, value: slots)
        in context.bindings.entries) {
      final kind = _kinds[action];
      if (kind == null) {
        throw ArgumentError(
          '"${context.name}" binds "$action", which is not an action.',
        );
      }
      for (final binding in slots) {
        _requireAccepts(action, binding.control);
        if (!binding.scale.isFinite) {
          throw ArgumentError('A binding of "$action" has no usable scale.');
        }
      }
    }
  }

  void _requireAccepts(String action, Control control) {
    if (_kinds[action]!.accepts(control)) return;
    throw ArgumentError('"$action" cannot take $control.');
  }

  Map<String, Map<String, List<Binding>>> _defaults() => {
    for (final context in _contexts.values) context.name: {...context.bindings},
  };

  ({String action, int slot})? _holder(
    String context,
    Control control, {
    required String action,
    required int slot,
  }) {
    for (final MapEntry(key: name, value: slots) in _bound[context]!.entries) {
      for (var i = 0; i < slots.length; i++) {
        final self = name == action && i == slot;
        if (slots[i].control == control && !self) {
          return (action: name, slot: i);
        }
      }
    }
    return null;
  }

  // Lists are replaced rather than edited, so a list handed out by
  // [bindingsIn] never changes under its reader.
  void _put(String context, String action, int slot, Control control) {
    final slots = [..._slots(context, action)];
    if (slot == slots.length) {
      slots.add(Binding(control));
    } else {
      slots[slot] = slots[slot].withControl(control);
    }
    _bound[context]![action] = List.unmodifiable(slots);
  }

  Map<String, Object?> _changed(
    String context,
    Map<String, List<Binding>> actions,
  ) => {
    for (final MapEntry(key: action, value: slots) in actions.entries)
      if (!_sameControls(slots, _contexts[context]!.bindings[action]!))
        action: [for (final binding in slots) binding.control.toJson()],
  };

  void _override(
    Map<String, Map<String, List<Binding>>> next,
    String context,
    String action,
    List<Control> controls,
  ) {
    final slots = next[context]?[action];
    final kind = _kinds[action];
    if (slots == null || kind == null || !controls.every(kind.accepts)) return;
    next[context]![action] = List.unmodifiable([
      for (var i = 0; i < math.max(slots.length, controls.length); i++)
        if (i >= controls.length)
          slots[i]
        else if (i < slots.length)
          slots[i].withControl(controls[i])
        else
          Binding(controls[i]),
    ]);
  }
}

bool _sameControls(List<Binding> a, List<Binding> b) =>
    a.length == b.length &&
    Iterable<int>.generate(a.length).every((i) => a[i].control == b[i].control);

Map<String, Map<String, List<Control>>> _parse(Object? json) {
  final contexts = _object(
    _object(json, 'The profile')['contexts'] ?? const <String, Object?>{},
    '"contexts"',
  );
  return {
    for (final MapEntry(key: context, value: actions) in contexts.entries)
      context: {
        for (final MapEntry(key: action, value: controls) in _object(
          actions,
          '"$context"',
        ).entries)
          action: _controlList(controls, action),
      },
  };
}

Map<String, Object?> _object(Object? value, String what) =>
    value is Map<String, Object?>
    ? value
    : throw FormatException('$what should be an object.');

List<Control> _controlList(Object? value, String action) =>
    value is List<Object?>
    ? [for (final item in value) Control.fromJson(item)]
    : throw FormatException('"$action" should be a list of controls.');

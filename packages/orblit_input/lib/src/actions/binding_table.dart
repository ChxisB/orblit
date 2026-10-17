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
/// with [rebind], and [toJson] writes down only the slots that differ from
/// the defaults, so a default the game changes later still reaches a player
/// who never touched that slot.
///
/// Only a [rebind] is checked for clashes, and only inside its own context.
/// A game's defaults may give one control to two actions on purpose.
final class BindingTable {
  BindingTable({
    required Map<String, ActionKind> kinds,
    required List<InputContext> contexts,
  }) : _kinds = Map.unmodifiable(kinds),
       _contexts = {for (final context in contexts) context.name: context},
       _bound = {
         for (final context in contexts)
           context.name: <String, List<Binding>>{},
       } {
    if (_contexts.length != contexts.length) {
      throw ArgumentError('Two contexts have the same name.');
    }
    contexts.forEach(_check);
    _replace(_defaults());
  }

  final Map<String, ActionKind> _kinds;
  final Map<String, InputContext> _contexts;
  final Map<String, Map<String, List<Binding>>> _bound;

  /// Whether a context of this name exists.
  bool hasContext(String context) => _contexts.containsKey(context);

  /// Whether the contexts under [context] go quiet while it is on.
  bool blocks(String context) => _context(context).blocks;

  /// What [context] binds now, by action name. A view that follows every
  /// [rebind] and [load]. The list for an action is never edited, only
  /// replaced.
  Map<String, List<Binding>> bindingsIn(String context) =>
      UnmodifiableMapView(_bound[_context(context).name]!);

  /// The action that already has [control] in [context], or null.
  ///
  /// [slot] is the binding of [action] that is about to take [control]. It is
  /// not a clash with itself. A slot that has [control] already is not a
  /// clash either, even when another action shares it, because nothing new is
  /// taken.
  ///
  /// Only the same control counts. A whole stick and the axes it is made of
  /// are different controls, and so are a trigger read as a button and as an
  /// axis.
  String? clash(
    String action,
    Control control, {
    required String context,
    int slot = 0,
  }) {
    _slots(context, action);
    return _holders(
      context,
      control,
      action: action,
      slot: slot,
    ).firstOrNull?.action;
  }

  /// Points binding number [slot] of [action] at [control].
  ///
  /// [slot] may be one past the last binding, which adds one. The binding
  /// keeps the scale and half the slot had, unless [scale] or [part] say
  /// otherwise. A new binding starts at a scale of 1 on x.
  ///
  /// When another binding in [context] has [control], this throws a
  /// [BindingClash] unless [swap] is set, which gives that binding the
  /// control [slot] had. A swap needs exactly one other binding to trade with.
  ///
  /// Throws an [ArgumentError] for a [scale] that is not a finite number.
  void rebind(
    String action,
    Control control, {
    required String context,
    int slot = 0,
    bool swap = false,
    double? scale,
    AxisPart? part,
  }) {
    final slots = _slots(context, action);
    RangeError.checkValueInInterval(slot, 0, slots.length, 'slot');
    if (scale != null && !scale.isFinite) {
      throw ArgumentError.value(scale, 'scale', 'A scale must be finite.');
    }
    _requireAccepts(action, control);
    final holders = _holders(context, control, action: action, slot: slot);
    if (holders.isEmpty) {
      _put(context, action, slot, control, scale: scale, part: part);
      return;
    }
    if (!swap || holders.length > 1) {
      throw BindingClash(holders.first.action, control);
    }
    if (slot == slots.length) {
      throw ArgumentError.value(
        slot,
        'slot',
        'A new binding has nothing to swap.',
      );
    }
    final holder = holders.single;
    final given = slots[slot].control;
    _requireAccepts(holder.action, given);
    _put(context, holder.action, holder.slot, given);
    _put(context, action, slot, control, scale: scale, part: part);
  }

  /// The controls the player changed, as JSON.
  ///
  /// A changed action is written as one entry per slot. A slot still at its
  /// default is null. A changed slot is its control, with a `scale` or a
  /// `part` beside it when the binding does not have the one the default
  /// slot would have kept.
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
  /// can no longer take or this build cannot read, so an old profile loads
  /// into a newer game. A profile that is not made of objects and lists
  /// throws a [FormatException] and changes nothing.
  void load(Object? json) {
    final overrides = _parse(json);
    final next = _defaults();
    for (final MapEntry(key: context, value: actions) in overrides.entries) {
      for (final MapEntry(key: action, value: slots) in actions.entries) {
        _override(next, context, action, slots);
      }
    }
    _replace(next);
  }

  InputContext _context(String context) =>
      _contexts[context] ??
      (throw ArgumentError('There is no context called "$context".'));

  // An action the context does not mention has no bindings there yet, which
  // is not the same as an action that does not exist.
  List<Binding> _slots(String context, String action) =>
      _bound[_context(context).name]![action] ??
      (_kinds.containsKey(action)
          ? const []
          : throw ArgumentError('There is no action called "$action".'));

  void _check(InputContext context) {
    for (final MapEntry(key: action, value: slots)
        in context.bindings.entries) {
      if (!_kinds.containsKey(action)) {
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

  // In place, so the views [bindingsIn] handed out follow a load. A list that
  // comes out the same stays the list it was, because a new list reads as a
  // new meaning and cuts a held button.
  void _replace(Map<String, Map<String, List<Binding>>> next) {
    for (final MapEntry(key: context, value: actions) in next.entries) {
      final held = _bound[context]!;
      held.removeWhere((action, _) => !actions.containsKey(action));
      for (final MapEntry(key: action, value: slots) in actions.entries) {
        if (!_sameBindings(held[action], slots)) held[action] = slots;
      }
    }
  }

  List<({String action, int slot})> _holders(
    String context,
    Control control, {
    required String action,
    required int slot,
  }) {
    final own = _bound[context]![action] ?? const <Binding>[];
    if (slot < own.length && own[slot].control == control) return const [];
    return [
      for (final MapEntry(key: name, value: slots) in _bound[context]!.entries)
        for (var i = 0; i < slots.length; i++)
          if (slots[i].control == control && !(name == action && i == slot))
            (action: name, slot: i),
    ];
  }

  // Lists are replaced rather than edited, so a list handed out by
  // [bindingsIn] never changes under its reader.
  void _put(
    String context,
    String action,
    int slot,
    Control control, {
    double? scale,
    AxisPart? part,
  }) {
    final slots = [..._slots(context, action)];
    if (slot == slots.length) {
      slots.add(Binding(control, scale: scale ?? 1, part: part ?? AxisPart.x));
    } else {
      slots[slot] = slots[slot].withControl(control, scale: scale, part: part);
    }
    _bound[context]![action] = List.unmodifiable(slots);
  }

  Map<String, Object?> _changed(
    String context,
    Map<String, List<Binding>> actions,
  ) {
    final changed = <String, Object?>{};
    for (final MapEntry(key: action, value: slots) in actions.entries) {
      final was = _contexts[context]!.bindings[action] ?? const <Binding>[];
      final changes = _changes(slots, was);
      if (changes.any((change) => change != null)) changed[action] = changes;
    }
    return changed;
  }

  void _override(
    Map<String, Map<String, List<Binding>>> next,
    String context,
    String action,
    List<_Slot?> slots,
  ) {
    final actions = next[context];
    final kind = _kinds[action];
    if (actions == null || kind == null) return;
    // A control the action cannot take leaves its slot at the default, like
    // one that could not be read.
    final fitting = [
      for (final slot in slots)
        slot != null && kind.accepts(slot.control) ? slot : null,
    ];
    actions[action] = _merge(actions[action] ?? const [], fitting);
  }
}

typedef _Slot = ({Control control, double? scale, AxisPart? part});

bool _sameBindings(List<Binding>? a, List<Binding> b) =>
    a != null &&
    a.length == b.length &&
    Iterable<int>.generate(b.length).every((i) => _same(a[i], b[i]));

// Shaping is left out. A rebind and a load carry it over as it was.
bool _same(Binding a, Binding b) =>
    a.control == b.control && a.scale == b.scale && a.part == b.part;

List<Object?> _changes(List<Binding> now, List<Binding> was) => [
  for (var i = 0; i < now.length; i++)
    _change(now[i], i < was.length ? was[i] : null),
];

// What the slot would keep through a rebind to the same control is left out,
// so a profile says only what the player chose.
Object? _change(Binding now, Binding? was) {
  if (was != null && _same(now, was)) return null;
  final kept = (was ?? Binding(now.control)).withControl(now.control);
  return {
    ...now.control.toJson(),
    if (now.scale != kept.scale) 'scale': now.scale,
    if (now.part != kept.part) 'part': now.part.name,
  };
}

// A null slot leaves the binding as it is. Slots past the last binding are
// added, and a null past the last binding adds nothing, so the slots after
// it move up.
List<Binding> _merge(List<Binding> bindings, List<_Slot?> slots) {
  final merged = <Binding>[];
  for (var i = 0; i < math.max(bindings.length, slots.length); i++) {
    final slot = i < slots.length ? slots[i] : null;
    final binding = i < bindings.length ? bindings[i] : null;
    if (slot != null) {
      merged.add(
        (binding ?? Binding(slot.control)).withControl(
          slot.control,
          scale: slot.scale,
          part: slot.part,
        ),
      );
    } else if (binding != null) {
      merged.add(binding);
    }
  }
  return List.unmodifiable(merged);
}

Map<String, Map<String, List<_Slot?>>> _parse(Object? json) {
  final contexts = _object(
    _object(json, 'The profile')['contexts'] ?? const <String, Object?>{},
    '"contexts"',
  );
  return {
    for (final MapEntry(key: context, value: actions) in contexts.entries)
      context: {
        for (final MapEntry(key: action, value: slots) in _object(
          actions,
          '"$context"',
        ).entries)
          action: _slotList(slots, action),
      },
  };
}

Map<String, Object?> _object(Object? value, String what) =>
    value is Map<String, Object?>
    ? value
    : throw FormatException('$what should be an object.');

List<_Slot?> _slotList(Object? value, String action) => value is List<Object?>
    ? [for (final item in value) _readable(item)]
    : throw FormatException('"$action" should be a list of controls.');

// A slot this build cannot read, such as a button a later engine dropped,
// leaves the binding at the default. Refusing it would cost the player the
// whole profile.
_Slot? _readable(Object? json) {
  if (json is! Map<String, Object?>) return null;
  final scale = json['scale'];
  final part = AxisPart.values.asNameMap()[json['part']];
  final badScale =
      json.containsKey('scale') && !(scale is num && scale.isFinite);
  final badPart = json.containsKey('part') && part == null;
  if (badScale || badPart) return null;
  final Control control;
  try {
    control = Control.fromJson(
      {...json}
        ..remove('scale')
        ..remove('part'),
    );
  } on FormatException {
    return null;
  }
  return (
    control: control,
    scale: scale is num ? scale.toDouble() : null,
    part: part,
  );
}

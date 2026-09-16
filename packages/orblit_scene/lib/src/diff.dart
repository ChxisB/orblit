import 'component.dart';
import 'document.dart';
import 'entity.dart';
import 'values.dart';

/// One change to a document, addressed by id.
///
/// By id and not by position, because a position is only true of the document
/// it was measured in. An undo stack full of "the fourth object" is an undo
/// stack that corrupts a scene the moment somebody deletes the third — and
/// that is not a theoretical failure, it is the ordinary consequence of two
/// people editing one scene and merging.
///
/// Every operation knows its own [inverse]. That is what makes a diff
/// something an editor can hold on an undo stack rather than a report: the
/// same machinery that applies a change undoes it, so there is no second
/// implementation to disagree with the first.
sealed class SceneOp {
  const SceneOp();

  /// The operation that puts back what this one changed.
  SceneOp get inverse;

  /// The document with this change made.
  ///
  /// Returns a new document rather than mutating one. A diff is applied
  /// speculatively — to preview a merge, to check that an inverse really
  /// inverts — and a half-applied mutation of somebody's open scene is not a
  /// state worth being able to reach.
  SceneDocument applyTo(SceneDocument document);

  Map<String, Object?> toJson();

  /// One operation out of its JSON, or null when it is not one.
  static SceneOp? fromJson(Object? raw) {
    if (raw is! Map<String, Object?>) return null;
    return switch (raw['op']) {
      'add' => AddEntity.fromJson(raw),
      'remove' => RemoveEntity.fromJson(raw),
      'name' => SetEntityName.fromJson(raw),
      'visible' => SetVisible.fromJson(raw),
      'component' => SetComponent.fromJson(raw),
      'field' => SetField.fromJson(raw),
      'reparent' => Reparent.fromJson(raw),
      'reorder' => Reorder.fromJson(raw),
      'setting' => SetSetting.fromJson(raw),
      _ => null,
    };
  }
}

/// Puts an entity in, immediately after [after] or at the head when it is null.
///
/// Placed by neighbour rather than by index. An index is invalidated by every
/// other operation in the same diff; a neighbour is not, which is what lets
/// the operations in a diff be applied in order without each one having to
/// know what the ones before it did to the numbering.
class AddEntity extends SceneOp {
  const AddEntity(this.entity, {this.after});

  static AddEntity fromJson(Map<String, Object?> json) => AddEntity(
    Values.object(json['entity']),
    after: Values.text(json, 'after'),
  );

  /// The entity as JSON, so an operation is a value and can be stored.
  final Map<String, Object?> entity;

  final String? after;

  String get id => Values.text(entity, 'id') ?? '';

  @override
  SceneOp get inverse => RemoveEntity(entity, after: after);

  @override
  SceneDocument applyTo(SceneDocument document) {
    final added = SceneEntity.fromJson(entity);
    if (added == null || document.contains(added.id)) return document;
    return document.copyWith(
      entities: _inserted(document.entities, added, after),
    );
  }

  @override
  Map<String, Object?> toJson() =>
      Values.pruned({'op': 'add', 'entity': entity, 'after': after});
}

/// Takes an entity out, remembering where it was so it can be put back.
///
/// Children are not touched. A parent link that now names nothing is repaired
/// when the document is next read, and repairing it here would mean a remove
/// and its inverse were not symmetrical — undoing a delete would give back a
/// differently shaped tree from the one that was deleted.
class RemoveEntity extends SceneOp {
  const RemoveEntity(this.entity, {this.after});

  static RemoveEntity fromJson(Map<String, Object?> json) => RemoveEntity(
    Values.object(json['entity']),
    after: Values.text(json, 'after'),
  );

  final Map<String, Object?> entity;

  /// What it sat after, at the moment it was removed.
  final String? after;

  String get id => Values.text(entity, 'id') ?? '';

  @override
  SceneOp get inverse => AddEntity(entity, after: after);

  @override
  SceneDocument applyTo(SceneDocument document) =>
      document.withEntity(id, null);

  @override
  Map<String, Object?> toJson() =>
      Values.pruned({'op': 'remove', 'entity': entity, 'after': after});
}

/// Renames an entity.
class SetEntityName extends SceneOp {
  const SetEntityName(this.id, {required this.from, required this.to});

  static SetEntityName fromJson(Map<String, Object?> json) => SetEntityName(
    Values.text(json, 'id') ?? '',
    from: Values.text(json, 'from') ?? '',
    to: Values.text(json, 'to') ?? '',
  );

  final String id;
  final String from;
  final String to;

  @override
  SceneOp get inverse => SetEntityName(id, from: to, to: from);

  @override
  SceneDocument applyTo(SceneDocument document) {
    final entity = document[id];
    return entity == null
        ? document
        : document.withEntity(id, entity.copyWith(name: to));
  }

  @override
  Map<String, Object?> toJson() => {
    'op': 'name',
    'id': id,
    'from': from,
    'to': to,
  };
}

/// Shows or hides an entity.
class SetVisible extends SceneOp {
  const SetVisible(this.id, {required this.from, required this.to});

  static SetVisible fromJson(Map<String, Object?> json) => SetVisible(
    Values.text(json, 'id') ?? '',
    from: Values.flag(json, 'from', fallback: true),
    to: Values.flag(json, 'to', fallback: true),
  );

  final String id;
  final bool from;
  final bool to;

  @override
  SceneOp get inverse => SetVisible(id, from: to, to: from);

  @override
  SceneDocument applyTo(SceneDocument document) {
    final entity = document[id];
    return entity == null
        ? document
        : document.withEntity(id, entity.copyWith(visible: to));
  }

  @override
  Map<String, Object?> toJson() => {
    'op': 'visible',
    'id': id,
    'from': from,
    'to': to,
  };
}

/// Gives an entity a component, takes one away, or replaces one wholesale.
///
/// A null on either side is the absence of the component, which is why this
/// and [SetField] are separate operations: "the light went away" and "the
/// light got dimmer" are different edits, and a diff that could not tell them
/// apart would undo the first by inventing a light with default values.
class SetComponent extends SceneOp {
  const SetComponent(
    this.id,
    this.type, {
    required this.from,
    required this.to,
  });

  static SetComponent fromJson(Map<String, Object?> json) => SetComponent(
    Values.text(json, 'id') ?? '',
    Values.text(json, 'type') ?? '',
    from: json['from'] is Map<String, Object?>
        ? json['from']! as Map<String, Object?>
        : null,
    to: json['to'] is Map<String, Object?>
        ? json['to']! as Map<String, Object?>
        : null,
  );

  final String id;
  final String type;
  final Map<String, Object?>? from;
  final Map<String, Object?>? to;

  @override
  SceneOp get inverse => SetComponent(id, type, from: to, to: from);

  @override
  SceneDocument applyTo(SceneDocument document) {
    final entity = document[id];
    if (entity == null) return document;
    final next = to;
    return document.withEntity(
      id,
      entity.withComponent(
        type,
        next == null ? null : SceneComponents.read(type, next),
      ),
    );
  }

  @override
  Map<String, Object?> toJson() => Values.pruned({
    'op': 'component',
    'id': id,
    'type': type,
    'from': from,
    'to': to,
  });
}

/// Changes one field of one component.
///
/// The ordinary edit, and the reason a diff of a moved object is four lines
/// rather than the whole object. A null on either side means the field was not
/// written, which for a component that prunes its unset fields is the same
/// thing as not having one.
class SetField extends SceneOp {
  const SetField(
    this.id,
    this.type,
    this.field, {
    required this.from,
    required this.to,
  });

  static SetField fromJson(Map<String, Object?> json) => SetField(
    Values.text(json, 'id') ?? '',
    Values.text(json, 'type') ?? '',
    Values.text(json, 'field') ?? '',
    from: json['from'],
    to: json['to'],
  );

  final String id;
  final String type;
  final String field;
  final Object? from;
  final Object? to;

  @override
  SceneOp get inverse => SetField(id, type, field, from: to, to: from);

  @override
  SceneDocument applyTo(SceneDocument document) {
    final entity = document[id];
    final component = entity?[type];
    if (entity == null || component == null) return document;

    final json = Map<String, Object?>.of(component.toJson());
    if (to == null) {
      json.remove(field);
    } else {
      json[field] = to;
    }
    return document.withEntity(
      id,
      entity.withComponent(type, SceneComponents.read(type, json)),
    );
  }

  @override
  Map<String, Object?> toJson() => Values.pruned({
    'op': 'field',
    'id': id,
    'type': type,
    'field': field,
    'from': from,
    'to': to,
  });
}

/// Hangs an entity from a different parent, or from none.
class Reparent extends SceneOp {
  const Reparent(this.id, {required this.from, required this.to});

  static Reparent fromJson(Map<String, Object?> json) => Reparent(
    Values.text(json, 'id') ?? '',
    from: Values.text(json, 'from'),
    to: Values.text(json, 'to'),
  );

  final String id;
  final String? from;
  final String? to;

  @override
  SceneOp get inverse => Reparent(id, from: to, to: from);

  @override
  SceneDocument applyTo(SceneDocument document) {
    final entity = document[id];
    if (entity == null) return document;
    return document.withEntity(
      id,
      to == null
          ? entity.copyWith(clearParent: true)
          : entity.copyWith(parent: to),
    );
  }

  @override
  Map<String, Object?> toJson() =>
      Values.pruned({'op': 'reparent', 'id': id, 'from': from, 'to': to});
}

/// Moves an entity along the document's order.
///
/// Which is what changes the order it draws and lists in, among its siblings
/// and everywhere else. Stated as the neighbour it now sits after and the one
/// it sat after before, so the operation is exactly invertible.
class Reorder extends SceneOp {
  const Reorder(this.id, {required this.from, required this.to});

  static Reorder fromJson(Map<String, Object?> json) => Reorder(
    Values.text(json, 'id') ?? '',
    from: Values.text(json, 'from'),
    to: Values.text(json, 'to'),
  );

  final String id;

  /// What it sat after before, and what it sits after now. Null is the head
  /// of the document.
  final String? from;
  final String? to;

  @override
  SceneOp get inverse => Reorder(id, from: to, to: from);

  @override
  SceneDocument applyTo(SceneDocument document) {
    final entity = document[id];
    if (entity == null) return document;
    final without = [
      for (final one in document.entities)
        if (one.id != id) one,
    ];
    return document.copyWith(entities: _inserted(without, entity, to));
  }

  @override
  Map<String, Object?> toJson() =>
      Values.pruned({'op': 'reorder', 'id': id, 'from': from, 'to': to});
}

/// Changes one of the scene's own settings, or its name.
class SetSetting extends SceneOp {
  const SetSetting(this.field, {required this.from, required this.to});

  static SetSetting fromJson(Map<String, Object?> json) => SetSetting(
    Values.text(json, 'field') ?? '',
    from: json['from'],
    to: json['to'],
  );

  static const String name = 'name';
  static const String sky = 'sky';
  static const String ambient = 'ambient';
  static const String hour = 'hour';
  static const String cycle = 'cycle';
  static const String hoursPerSecond = 'hoursPerSecond';

  final String field;
  final Object? from;
  final Object? to;

  @override
  SceneOp get inverse => SetSetting(field, from: to, to: from);

  @override
  SceneDocument applyTo(SceneDocument document) {
    if (field == name) {
      final wanted = to;
      return document.copyWith(name: wanted is String ? wanted : document.name);
    }
    final settings = Map<String, Object?>.of(document.settings.toJson());
    final time = Map<String, Object?>.of(Values.object(settings['time']));
    switch (field) {
      case sky:
        settings[sky] = to;
      case ambient:
        settings[ambient] = to;
      case hour || cycle || hoursPerSecond:
        time[field] = to;
        settings['time'] = time;
      default:
        return document;
    }
    return document.copyWith(settings: SceneSettings.fromJson(settings));
  }

  @override
  Map<String, Object?> toJson() =>
      Values.pruned({'op': 'setting', 'field': field, 'from': from, 'to': to});
}

/// What changed between two documents.
///
/// The unit an editor's undo stack stores and the unit a renderer updates
/// from: a diff says what moved, so a scene with four thousand things in it
/// can be brought up to date by touching the one that changed rather than
/// being rebuilt.
class SceneDiff {
  const SceneDiff(this.operations);

  static const SceneDiff none = SceneDiff([]);

  static SceneDiff fromJson(Object? raw) => SceneDiff(
    raw is! List
        ? const []
        : [
            for (final one in raw)
              if (SceneOp.fromJson(one) case final op?) op,
          ],
  );

  final List<SceneOp> operations;

  bool get isEmpty => operations.isEmpty;

  bool get isNotEmpty => operations.isNotEmpty;

  /// Every change that turns [a] into [b].
  ///
  /// Built by applying each operation to a running copy as it is worked out,
  /// rather than by reasoning about what the operations will do to each
  /// other's positions. That is slower and it is the reason the result is
  /// right: an operation that places something by neighbour has to be measured
  /// against the document as it will actually be when that operation runs, and
  /// the cheapest way to know that is to have it in front of you.
  static SceneDiff between(SceneDocument a, SceneDocument b) {
    final operations = <SceneOp>[];
    var running = a;

    void run(SceneOp op) {
      operations.add(op);
      running = op.applyTo(running);
    }

    _settings(a, b, run);

    for (final entity in a.entities) {
      if (b.contains(entity.id)) continue;
      run(RemoveEntity(entity.toJson(), after: _after(running, entity.id)));
    }

    for (final entity in b.entities) {
      if (a.contains(entity.id)) continue;
      final at = b.indexOf(entity.id);
      final before = at > 0 ? b.entities[at - 1].id : null;
      run(
        AddEntity(
          entity.toJson(),
          after: before != null && running.contains(before) ? before : null,
        ),
      );
    }

    for (final was in a.entities) {
      final now = b[was.id];
      if (now == null) continue;
      _entity(was, now, run);
    }

    // Order last, once every entity that belongs in the result is in it. Each
    // step puts one entity where it goes, so at worst there is one operation
    // per entity and usually there are none.
    for (var i = 0; i < b.entities.length; i++) {
      if (i < running.entities.length &&
          running.entities[i].id == b.entities[i].id) {
        continue;
      }
      final id = b.entities[i].id;
      run(
        Reorder(
          id,
          from: _after(running, id),
          to: i > 0 ? b.entities[i - 1].id : null,
        ),
      );
    }

    return SceneDiff(operations);
  }

  /// [document] with every operation applied, in order.
  SceneDocument applyTo(SceneDocument document) {
    var current = document;
    for (final op in operations) {
      current = op.applyTo(current);
    }
    return current;
  }

  /// The diff that undoes this one.
  ///
  /// Reversed as well as inverted, and both halves are needed. Inverting each
  /// operation alone would undo a move before undoing the deletion that
  /// happened after it, and arrive somewhere neither document has ever been.
  SceneDiff get inverse =>
      SceneDiff([for (final op in operations.reversed) op.inverse]);

  List<Object?> toJson() => [for (final op in operations) op.toJson()];

  static void _settings(
    SceneDocument a,
    SceneDocument b,
    void Function(SceneOp) run,
  ) {
    if (a.name != b.name) {
      run(SetSetting(SetSetting.name, from: a.name, to: b.name));
    }
    final was = a.settings.toJson();
    final now = b.settings.toJson();
    for (final field in [SetSetting.sky, SetSetting.ambient]) {
      if (!Values.same(was[field], now[field])) {
        run(SetSetting(field, from: was[field], to: now[field]));
      }
    }
    final wasTime = Values.object(was['time']);
    final nowTime = Values.object(now['time']);
    for (final field in [
      SetSetting.hour,
      SetSetting.cycle,
      SetSetting.hoursPerSecond,
    ]) {
      if (!Values.same(wasTime[field], nowTime[field])) {
        run(SetSetting(field, from: wasTime[field], to: nowTime[field]));
      }
    }
  }

  static void _entity(
    SceneEntity was,
    SceneEntity now,
    void Function(SceneOp) run,
  ) {
    if (was.name != now.name) {
      run(SetEntityName(was.id, from: was.name, to: now.name));
    }
    if (was.visible != now.visible) {
      run(SetVisible(was.id, from: was.visible, to: now.visible));
    }

    final types = <String>{...was.components.keys, ...now.components.keys};
    for (final type in types) {
      final before = was[type]?.toJson();
      final after = now[type]?.toJson();
      if (before == null || after == null) {
        if (!Values.same(before, after)) {
          run(SetComponent(was.id, type, from: before, to: after));
        }
        continue;
      }
      for (final field in <String>{...before.keys, ...after.keys}) {
        if (Values.same(before[field], after[field])) continue;
        run(
          SetField(was.id, type, field, from: before[field], to: after[field]),
        );
      }
    }

    if (was.parent != now.parent) {
      run(Reparent(was.id, from: was.parent, to: now.parent));
    }
  }

  /// What [id] currently sits after, or null when it is at the head.
  static String? _after(SceneDocument document, String id) {
    final at = document.indexOf(id);
    return at > 0 ? document.entities[at - 1].id : null;
  }
}

/// [entities] with [entity] put in immediately after [after].
///
/// At the head when [after] is null, and at the end when it names something
/// that is not there — which can only happen to a diff that has been edited by
/// hand or merged, and losing the entity would be the worse answer.
List<SceneEntity> _inserted(
  List<SceneEntity> entities,
  SceneEntity entity,
  String? after,
) {
  if (after == null) return [entity, ...entities];
  final next = <SceneEntity>[];
  var placed = false;
  for (final one in entities) {
    next.add(one);
    if (one.id == after) {
      next.add(entity);
      placed = true;
    }
  }
  if (!placed) next.add(entity);
  return next;
}

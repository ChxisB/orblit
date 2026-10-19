import 'dart:typed_data';

import 'package:orblit_core/orblit_core.dart';

import 'inspector.dart';

/// The built-in transform, named as the core's `transform` helper names it.
const _builtIn = <String, List<String>>{
  'orblit.LocalTransform': [
    'x',
    'y',
    'z',
    'rotationX',
    'rotationY',
    'rotationZ',
    'rotationW',
    'scaleX',
    'scaleY',
    'scaleZ',
  ],
};

/// An `orblit_core` [World], looked into by component name.
///
/// The world holds no names of its own, so the caller says which component
/// types to look for. A manifest entry, in the shape `orblit_codegen` writes,
/// gives a component its field names. A component with none is listed by name
/// and cannot be set, because a number with no name is too easy to write to the
/// wrong place.
final class CoreWorldInspector implements WorldInspector {
  /// Throws [ArgumentError] when a manifest entry for a type in [types] has a
  /// different number of fields than the type has elements.
  CoreWorldInspector(
    this._world,
    List<ComponentType> types, {
    List<Map<String, Object?>> manifest = const [],
  }) : _byName = {for (final type in types) type.name: type},
       _byId = {for (final type in types) type.id: type},
       _fields = {
         ..._builtIn,
         for (final entry in manifest)
           entry['name']! as String: [
             ...(entry['fields']! as List<Object?>).cast<String>(),
           ],
       } {
    for (final type in types) {
      final fields = _fields[type.name];
      if (fields != null && fields.length != type.arity) {
        throw ArgumentError.value(
          fields,
          'manifest',
          '${type.name} holds ${type.arity} elements, not ${fields.length}',
        );
      }
    }
  }

  final World _world;
  final Map<String, ComponentType> _byName;
  final Map<int, ComponentType> _byId;
  final Map<String, List<String>> _fields;

  @override
  List<EntityRow> entities(List<String> filter) {
    final query = _world.query([for (final name in filter) _type(name)]);
    try {
      return [
        for (final chunk in query.chunks)
          for (final id in chunk.entities)
            (id: id, components: _names(chunk.componentIds)),
      ]..sort((a, b) => a.id.compareTo(b.id));
    } finally {
      query.dispose();
    }
  }

  @override
  Map<String, Map<String, Object?>>? read(int id) {
    if (!_world.isAlive(id)) return null;
    return {
      for (final name in _byName.keys.toList()..sort())
        if (_world.has(id, _byName[name]!)) name: _describe(id, _byName[name]!),
    };
  }

  @override
  void write(int id, String component, String field, num value) {
    final type = _type(component);
    final fields = _fields[component];
    if (fields == null) {
      throw ArgumentError.value(component, 'component', 'has no field names');
    }
    final index = fields.indexOf(field);
    if (index < 0) {
      throw ArgumentError.value(field, 'field', 'is not a field of $component');
    }
    if (!_world.isAlive(id)) throw DeadEntityError(id);
    final bytes = _world.bytesOf(id, type);
    if (bytes == null) {
      throw ArgumentError.value(id, 'id', 'has no $component');
    }
    _put(ByteData.sublistView(bytes), type.kind, index, value);
  }

  ComponentType _type(String name) =>
      _byName[name] ??
      (throw ArgumentError.value(name, 'component', 'unknown'));

  List<String> _names(List<int> ids) => List.unmodifiable(
    [for (final id in ids) _byId[id]?.name ?? '#$id']..sort(),
  );

  Map<String, Object?> _describe(int id, ComponentType type) {
    final fields = _fields[type.name];
    if (fields == null) {
      return {'undescribed': '${type.kind.name} x${type.arity}'};
    }
    final data = ByteData.sublistView(_world.bytesOf(id, type)!);
    return {
      for (var i = 0; i < fields.length; i++)
        fields[i]: _get(data, type.kind, i),
    };
  }
}

/// An element as JSON can carry it. A float that is not finite is its name,
/// because that is exactly what someone looking for a broken transform has
/// found, and the encoder refuses to write it as a number.
Object _get(ByteData data, ComponentKind kind, int index) {
  final at = index * kind.bytesPerElement;
  final value = switch (kind) {
    ComponentKind.float32 => data.getFloat32(at, Endian.host),
    ComponentKind.float64 => data.getFloat64(at, Endian.host),
    ComponentKind.int32 => data.getInt32(at, Endian.host),
    ComponentKind.uint32 => data.getUint32(at, Endian.host),
    ComponentKind.int64 => data.getInt64(at, Endian.host),
    ComponentKind.uint8 => data.getUint8(at),
  };
  return value is double && !value.isFinite ? '$value' : value;
}

// shortcut: an integer wraps to the width of its column, add a range check
// when a component has a field where that would hide a mistake.
void _put(ByteData data, ComponentKind kind, int index, num value) {
  final at = index * kind.bytesPerElement;
  final isFloat =
      kind == ComponentKind.float32 || kind == ComponentKind.float64;
  if (!isFloat && value % 1 != 0) {
    throw ArgumentError.value(value, 'value', 'must be whole for ${kind.name}');
  }
  switch (kind) {
    case ComponentKind.float32:
      data.setFloat32(at, value.toDouble(), Endian.host);
    case ComponentKind.float64:
      data.setFloat64(at, value.toDouble(), Endian.host);
    case ComponentKind.int32:
      data.setInt32(at, value.toInt(), Endian.host);
    case ComponentKind.uint32:
      data.setUint32(at, value.toInt(), Endian.host);
    case ComponentKind.int64:
      data.setInt64(at, value.toInt(), Endian.host);
    case ComponentKind.uint8:
      data.setUint8(at, value.toInt());
  }
}

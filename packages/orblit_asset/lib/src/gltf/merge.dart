import 'dart:convert';
import 'dart:typed_data';

import 'accessor.dart';
import 'document.dart';

/// Points every primitive at the lowest-numbered material identical to its
/// own, and returns how many materials stopped being used.
///
/// Identical means identical: the same JSON, key for key, with one exception.
/// That sounds strict, and it is exactly as strict as it should be — two
/// materials that differ by one number are two materials, and the reason this
/// is worth running at all is that packing a model's textures makes genuinely
/// identical materials out of ones that previously differed only in which
/// image they named.
///
/// The exception is `name`, which is not something a material draws with.
/// Counting it would be the end of the whole exercise: a real model calls its
/// materials `wood` and `metal`, and those are exactly the two the atlas has
/// just made into one. The survivor keeps its own name.
///
/// The unused materials are left in the document rather than removed. Removing
/// one renumbers every material after it, and every `KHR_materials_variants`
/// mapping, every primitive and every extension holding a material index would
/// have to be found and corrected — a great deal of risk to save a few hundred
/// bytes of JSON that compresses to nothing.
int mergeMaterials(GltfDocument document) {
  final materials = document.list('materials');
  if (materials.length < 2) return 0;

  final first = <String, int>{};
  final to = <int, int>{};
  for (var i = 0; i < materials.length; i++) {
    final key = jsonEncode(_identity(materials[i]));
    final already = first[key];
    if (already == null) {
      first[key] = i;
    } else {
      to[i] = already;
    }
  }
  if (to.isEmpty) return 0;

  for (final mesh in document.list('meshes')) {
    final primitives = mesh['primitives'];
    if (primitives is! List) continue;
    for (final primitive in primitives) {
      if (primitive is! Map<String, Object?>) continue;
      final material = primitive['material'];
      if (material is int && to.containsKey(material)) {
        primitive['material'] = to[material];
      }
      // Variants name materials too, and a variant left pointing at a
      // material nothing else uses still works — but pointing it at the
      // survivor is what lets the two variants turn out to be the same one.
      final extensions = primitive['extensions'];
      if (extensions is! Map<String, Object?>) continue;
      final variants = extensions['KHR_materials_variants'];
      if (variants is! Map<String, Object?>) continue;
      final mappings = variants['mappings'];
      if (mappings is! List) continue;
      for (final mapping in mappings) {
        if (mapping is! Map<String, Object?>) continue;
        final material = mapping['material'];
        if (material is int && to.containsKey(material)) {
          mapping['material'] = to[material];
        }
      }
    }
  }
  return to.length;
}

/// JSON with its object keys in order, so two materials written by two
/// exporters in two orders compare equal.
/// What a material draws like, as a string two materials can be compared by.
Object? _identity(Map<String, Object?> material) => _canonical({
      for (final entry in material.entries)
        if (entry.key != 'name') entry.key: entry.value,
    });

Object? _canonical(Object? value) {
  if (value is Map) {
    final keys = value.keys.map((key) => '$key').toList()..sort();
    return {for (final key in keys) key: _canonical(value[key])};
  }
  if (value is List) return [for (final one in value) _canonical(one)];
  // 1 and 1.0 are the same glTF number and different Dart ones.
  if (value is num) return value.toDouble();
  return value;
}

/// Joins the primitives of each mesh that now share everything that makes a
/// draw call, and returns how many primitives stopped existing.
///
/// This is where a model's frame time actually changes. A kitbashed prop set
/// arrives as a hundred primitives because it was authored as a hundred
/// objects, and once they share a material there is no reason left for them to
/// be a hundred draws. Packing the textures is what makes them share one.
///
/// What is refused is as important as what is merged, because a wrong merge is
/// a silently deformed model:
///
///  * only within one mesh, since a mesh is what a node points at and merging
///    across meshes would move geometry to another node's transform;
///  * only triangles, since a strip and a fan cannot be concatenated;
///  * only primitives with the same attributes stored the same way, since the
///    merged buffer is a concatenation and a concatenation of two layouts is
///    neither;
///  * never anything skinned or with morph targets, and never anything a
///    variant swaps, since all three mean the primitive's material or its
///    vertices change after this has run.
int mergePrimitives(GltfDocument document, {int maxVertices = 65536}) {
  var removed = 0;
  for (final mesh in document.list('meshes')) {
    final primitives = mesh['primitives'];
    if (primitives is! List || primitives.length < 2) continue;

    // Grouped by what has to match, keeping the order they appear in: a
    // model's primitive order is the only draw order a glTF expresses, and
    // shuffling it can change how two transparent surfaces overlap.
    final groups = <String, List<Map<String, Object?>>>{};
    final order = <String>[];
    for (final primitive in primitives) {
      if (primitive is! Map<String, Object?>) continue;
      final key = _groupKey(document, primitive);
      if (key == null) continue;
      if (!groups.containsKey(key)) order.add(key);
      (groups[key] ??= []).add(primitive);
    }

    // Each batch replaces its first member where that member already was, and
    // the rest of the batch goes away.
    final replacing = <Map<String, Object?>, Map<String, Object?>>{};
    final dropping = <Map<String, Object?>>{};
    for (final key in order) {
      final group = groups[key]!;
      if (group.length < 2) continue;

      // Split before the index width changes rather than after: one vertex
      // past 65,535 turns every index in the merged primitive from two bytes
      // into four, and that cost is paid on every draw forever.
      var batch = <Map<String, Object?>>[];
      var vertices = 0;
      void close() {
        if (batch.length < 2) return;
        replacing[batch.first] = _join(document, batch);
        dropping.addAll(batch.skip(1));
        removed += batch.length - 1;
      }

      for (final primitive in group) {
        final count = _vertexCount(document, primitive);
        if (batch.isNotEmpty && vertices + count > maxVertices) {
          close();
          batch = [];
          vertices = 0;
        }
        batch.add(primitive);
        vertices += count;
      }
      close();
    }
    if (replacing.isEmpty) continue;

    final rebuilt = <Object?>[
      for (final primitive in primitives)
        if (primitive is! Map<String, Object?>)
          primitive
        else if (replacing.containsKey(primitive))
          replacing[primitive]
        else if (!dropping.contains(primitive))
          primitive,
    ];
    primitives
      ..clear()
      ..addAll(rebuilt);
  }
  return removed;
}

/// What two primitives have to agree on before they can become one, or null
/// when this primitive cannot be merged at all.
String? _groupKey(GltfDocument document, Map<String, Object?> primitive) {
  final mode = primitive['mode'] ?? 4;
  if (mode != 4) return null;
  if (primitive.containsKey('targets')) return null;

  final extensions = primitive['extensions'];
  if (extensions is Map<String, Object?>) {
    if (extensions.containsKey('KHR_materials_variants')) return null;
    if (extensions.containsKey('KHR_draco_mesh_compression')) return null;
  }

  final attributes = primitive['attributes'];
  if (attributes is! Map<String, Object?>) return null;
  if (attributes.containsKey('JOINTS_0') ||
      attributes.containsKey('WEIGHTS_0')) {
    return null;
  }
  if (attributes['POSITION'] is! int) return null;

  final names = attributes.keys.toList()..sort();
  final parts = <String>['material=${primitive['material']}'];
  for (final name in names) {
    final index = attributes[name];
    if (index is! int) return null;
    final Map<String, Object?> accessor;
    try {
      accessor = document.accessor(index);
    } on FormatException {
      return null; // Sparse, or not there. Either way, left alone.
    }
    parts.add('$name:${accessor['type']}:${accessor['componentType']}:'
        '${accessor['normalized'] == true}');
  }
  final indices = primitive['indices'];
  if (indices != null && indices is! int) return null;
  if (indices is int) {
    try {
      document.accessor(indices);
    } on FormatException {
      return null;
    }
  }
  return parts.join('|');
}

int _vertexCount(GltfDocument document, Map<String, Object?> primitive) {
  final attributes = primitive['attributes'] as Map<String, Object?>;
  return document.accessorCount(attributes['POSITION'] as int);
}

/// Several primitives as one.
Map<String, Object?> _join(
  GltfDocument document,
  List<Map<String, Object?>> group,
) {
  final first = group.first;
  final names =
      (first['attributes'] as Map<String, Object?>).keys.toList()..sort();

  final attributes = <String, Object?>{};
  for (final name in names) {
    final bytes = BytesBuilder(copy: false);
    var count = 0;
    List<double>? low;
    List<double>? high;
    for (final primitive in group) {
      final accessor =
          (primitive['attributes'] as Map<String, Object?>)[name] as int;
      final elements = document.accessorCount(accessor);
      bytes.add(document.accessorBytes(accessor));
      count += elements;
      if (name != 'POSITION') continue;
      // glTF requires min and max on POSITION, and something believes them:
      // a stale bounding box culls geometry that is on screen, which looks
      // like objects blinking out at certain camera angles.
      final positions = document.readVec3(accessor);
      low ??= [double.infinity, double.infinity, double.infinity];
      high ??= [-double.infinity, -double.infinity, -double.infinity];
      for (var i = 0; i < positions.length; i += 3) {
        for (var c = 0; c < 3; c++) {
          if (positions[i + c] < low[c]) low[c] = positions[i + c];
          if (positions[i + c] > high[c]) high[c] = positions[i + c];
        }
      }
    }
    final template =
        (first['attributes'] as Map<String, Object?>)[name] as int;
    attributes[name] = document.addLike(
      template,
      bytes.takeBytes(),
      count,
      min: low,
      max: high,
    );
  }

  // Indices are renumbered as the vertices they point at move up the merged
  // buffer. A primitive with none is drawn in order, so its indices are the
  // range it occupies.
  final indices = <int>[];
  var base = 0;
  for (final primitive in group) {
    final count = _vertexCount(document, primitive);
    final accessor = primitive['indices'];
    if (accessor is int) {
      for (final index in document.readIndices(accessor)) {
        indices.add(base + index);
      }
    } else {
      for (var i = 0; i < count; i++) {
        indices.add(base + i);
      }
    }
    base += count;
  }

  return <String, Object?>{
    'attributes': attributes,
    'indices': document.addIndices(indices),
    if (first['material'] != null) 'material': first['material'],
    // `extras` is somebody's data and the first primitive's is as good an
    // answer as there is; `mode` is 4 by definition here, which is the
    // default, so it is left out.
    if (first['extras'] != null) 'extras': first['extras'],
  };
}

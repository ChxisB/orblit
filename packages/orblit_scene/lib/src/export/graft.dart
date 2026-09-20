import 'dart:typed_data';

import 'package:orblit_mesh/orblit_mesh.dart';

/// A model that could not be copied into an export, and why.
///
/// Thrown rather than reported, because the alternative to copying a model in
/// is writing a file that silently lacks it. A scene that quietly lost a
/// building is worse than an export that stopped and said which file it could
/// not read.
class GraftFailure implements Exception {
  const GraftFailure(this.message);

  final String message;

  @override
  String toString() => message;
}

/// Copies every part of [model] into [into], returning its roots' new indices.
///
/// glTF has no way for one file to point at another and say "draw that here".
/// A scene that draws an imported model therefore cannot reference it; it has
/// to contain it. So the model's arrays are appended to ours and every index
/// inside them is moved on by however many entries were already there — its
/// meshes point at our accessors, its nodes at our meshes, its materials at
/// our textures, and so on through every cross-reference the format has.
///
/// The awkward part is that the format keeps adding cross-references, and an
/// extension may add one anywhere. So the links are a table rather than a
/// hundred lines of hand-written index arithmetic: one row per path, and a new
/// extension that carries an index is one row, in one place, which is the
/// difference between a bug that stops the build and a bug that shifts a
/// texture by one and is found by somebody looking at a wall a month later.
///
/// Only self-contained sources are accepted. A model whose bytes or images
/// live in a separate file is refused by name, because the export would
/// otherwise be a file that opens and is missing its skin. Cooked models are
/// single-buffer with their images inside, so in practice this refuses the
/// hand-made and the half-unpacked, which is what it is for.
Grafted graft({
  required Map<String, Object?> into,
  required GltfBuffer buffer,
  required Map<String, Object?> model,
  required Uint8List bytes,
  required String where,
}) {
  _refuseLooseFiles(model, where);
  final first = (into['nodes'] as List?)?.length ?? 0;

  // Where each of the source's arrays lands. Captured before anything is
  // copied: once copying starts the lengths move, and every index in the
  // source was written against the source's own numbering.
  final base = <String, int>{
    'accessors': buffer.accessors.length,
    'bufferViews': buffer.views.length,
    'animations': _count(into, 'animations'),
    'cameras': _count(into, 'cameras'),
    'images': _count(into, 'images'),
    'materials': _count(into, 'materials'),
    'meshes': _count(into, 'meshes'),
    'nodes': _count(into, 'nodes'),
    'samplers': _count(into, 'samplers'),
    'skins': _count(into, 'skins'),
    'textures': _count(into, 'textures'),
    'lights': _extensionOf(into, _lights, 'lights').length,
    'variants': _extensionOf(into, _variants, 'variants').length,
  };

  // The source's bytes go on the end of ours, and its views move with them.
  // Alignment survives because the builder pads to four and the format asks
  // for no more than four.
  final at = bytes.isEmpty ? 0 : buffer.addBytes(bytes);
  for (final raw in _items(model, 'bufferViews')) {
    final view = _clone(raw);
    view['buffer'] = 0;
    view['byteOffset'] = ((view['byteOffset'] as int?) ?? 0) + at;
    buffer.views.add(view);
  }

  for (final name in _arrays) {
    final items = _items(model, name);
    if (items.isEmpty) continue;
    final links = _links[name] ?? const <String, String>{};
    final destination = name == 'accessors'
        ? buffer.accessors
        : (into.putIfAbsent(name, () => <Map<String, Object?>>[])
              as List<Object?>);
    for (final raw in items) {
      final item = _clone(raw);
      for (final link in links.entries) {
        _shift(item, link.key.split('.'), base[link.value]!);
      }
      // A material's textures are reached through texture infos, and every
      // extension that adds a map adds another place one can sit. Rather than
      // list them, they are recognised by shape: a map with an index and
      // nothing in it that is not part of a texture info.
      if (name == 'materials') _shiftTextures(item, base['textures']!);
      destination.add(item);
    }
  }

  // Made only when there is something to put in it. An extension object with
  // an empty list in it is a document claiming to use an extension it does
  // not, which the validator is right to refuse.
  for (final extension in const [
    (_lights, 'lights'),
    (_variants, 'variants'),
  ]) {
    final theirs = _extensionOf(model, extension.$1, extension.$2);
    if (theirs.isEmpty) continue;
    _extension(into, extension.$1, extension.$2).addAll(theirs.map(_clone));
  }

  for (final key in const ['extensionsUsed', 'extensionsRequired']) {
    final theirs = (model[key] as List?)?.whereType<String>() ?? const [];
    if (theirs.isEmpty) continue;
    final ours = (into.putIfAbsent(key, () => <String>[]) as List<Object?>);
    for (final name in theirs) {
      if (!ours.contains(name)) ours.add(name);
    }
  }

  return Grafted(
    roots: _rootsOf(model, base['nodes']!),
    from: first,
    count: ((into['nodes'] as List?)?.length ?? 0) - first,
    shareable:
        _items(model, 'skins').isEmpty && _items(model, 'animations').isEmpty,
  );
}

/// One model, copied in, and where its nodes landed.
class Grafted {
  const Grafted({
    required this.roots,
    required this.from,
    required this.count,
    required this.shareable,
  });

  /// The nodes to hang under whatever is placing the model.
  final List<int> roots;

  /// The first node this graft wrote, and how many it wrote.
  final int from;
  final int count;

  /// Whether placing the model a second time can share this copy's data.
  ///
  /// False for anything skinned or animated. A skin names the joints it bends
  /// by node, and an animation names the nodes it moves, so a second copy
  /// sharing them would be bent and moved by the first copy's bones — two
  /// models in different places moving as one. Those get copied again in
  /// full; everything else, which is most of what a scene places twice,
  /// shares every byte it has.
  final bool shareable;
}

/// Places an already-grafted model again, without copying its data.
///
/// A scene that puts the same tree down two hundred times should be one tree
/// and two hundred placements, not two hundred trees. glTF already allows
/// this — a mesh may be drawn by any number of nodes — so what has to be
/// copied is the nodes and nothing else: no bytes, no accessors, no
/// materials, no textures. For a model of any size that is the difference
/// between a file somebody can ship and one they cannot.
List<int> regraft({
  required Map<String, Object?> into,
  required Grafted first,
}) {
  final nodes = into['nodes']! as List<Object?>;
  final by = nodes.length - first.from;
  for (var i = 0; i < first.count; i++) {
    final copy = _clone(nodes[first.from + i]! as Map<String, Object?>);
    _shift(copy, const ['children', '*'], by);
    nodes.add(copy);
  }
  return [for (final root in first.roots) root + by];
}

/// The top-level arrays that are copied wholesale, and the order does not
/// matter because every base was taken before any of them moved.
const List<String> _arrays = [
  'accessors',
  'animations',
  'cameras',
  'images',
  'materials',
  'meshes',
  'nodes',
  'samplers',
  'skins',
  'textures',
];

/// Every place one array's entry names another's, as a path inside an entry.
///
/// A `*` stands for every item of a list or every value of a map. The name on
/// the right is the array the number at that path counts into — which is the
/// whole of the arithmetic, stated once.
const Map<String, Map<String, String>> _links = {
  'accessors': {
    'bufferView': 'bufferViews',
    'sparse.indices.bufferView': 'bufferViews',
    'sparse.values.bufferView': 'bufferViews',
  },
  'animations': {
    // A channel's `sampler` counts into the animation's own samplers, not a
    // document-level array, so it is deliberately not here.
    'samplers.*.input': 'accessors',
    'samplers.*.output': 'accessors',
    'channels.*.target.node': 'nodes',
  },
  'images': {'bufferView': 'bufferViews'},
  'meshes': {
    'primitives.*.indices': 'accessors',
    'primitives.*.attributes.*': 'accessors',
    'primitives.*.targets.*.*': 'accessors',
    'primitives.*.material': 'materials',
    'primitives.*.extensions.KHR_draco_mesh_compression.bufferView':
        'bufferViews',
    'primitives.*.extensions.KHR_materials_variants.mappings.*.material':
        'materials',
    'primitives.*.extensions.KHR_materials_variants.mappings.*.variants.*':
        'variants',
  },
  'nodes': {
    'camera': 'cameras',
    'children.*': 'nodes',
    'mesh': 'meshes',
    'skin': 'skins',
    'extensions.KHR_lights_punctual.light': 'lights',
    'extensions.EXT_mesh_gpu_instancing.attributes.*': 'accessors',
  },
  'skins': {
    'inverseBindMatrices': 'accessors',
    'joints.*': 'nodes',
    'skeleton': 'nodes',
  },
  'textures': {
    'source': 'images',
    'sampler': 'samplers',
    // Every texture extension that supplies an image — basis, webp, ddsn —
    // spells it `source`, so one row covers all of them and the ones to come.
    'extensions.*.source': 'images',
  },
};

const String _lights = 'KHR_lights_punctual';
const String _variants = 'KHR_materials_variants';

/// Everything a texture info may hold besides its index.
///
/// The list is what makes recognising one safe: a map with an `index` and a
/// key outside this set is something else, and is left alone.
const Set<String> _textureInfo = {
  'index',
  'texCoord',
  'scale',
  'strength',
  'extensions',
  'extras',
};

int _count(Map<String, Object?> json, String name) =>
    (json[name] as List?)?.length ?? 0;

List<Map<String, Object?>> _items(Map<String, Object?> json, String name) => [
  for (final item in (json[name] as List?) ?? const [])
    if (item is Map) item.cast<String, Object?>(),
];

/// The list at `extensions.<name>.<key>`, made if it is not there yet.
///
/// Only to be called when something is about to go into it: an extension
/// object nobody fills is a claim the document cannot back up.
List<Object?> _extension(Map<String, Object?> json, String name, String key) {
  final extensions =
      json.putIfAbsent('extensions', () => <String, Object?>{})
          as Map<String, Object?>;
  final extension =
      extensions.putIfAbsent(name, () => <String, Object?>{})
          as Map<String, Object?>;
  return extension.putIfAbsent(key, () => <Object?>[]) as List<Object?>;
}

/// The same list, read only, without writing an empty one into a document
/// that had none.
List<Map<String, Object?>> _extensionOf(
  Map<String, Object?> json,
  String name,
  String key,
) {
  final extensions = json['extensions'];
  if (extensions is! Map) return const [];
  final extension = extensions[name];
  if (extension is! Map) return const [];
  return _items(extension.cast<String, Object?>(), key);
}

/// A private copy, so that grafting the same model twice does not write the
/// first graft's numbering into the second.
Map<String, Object?> _clone(Map<String, Object?> value) =>
    _cloned(value)! as Map<String, Object?>;

Object? _cloned(Object? value) {
  if (value is Map) {
    return <String, Object?>{
      for (final entry in value.entries) '${entry.key}': _cloned(entry.value),
    };
  }
  if (value is List) return <Object?>[for (final item in value) _cloned(item)];
  return value;
}

/// Adds [by] to whatever integer sits at [steps] inside [node].
void _shift(Object? node, List<String> steps, int by) {
  if (by == 0 || node == null || steps.isEmpty) return;
  final key = steps.first;

  if (steps.length == 1) {
    if (key != '*') {
      if (node is Map) {
        final value = node[key];
        if (value is int) node[key] = value + by;
      }
    } else if (node is List) {
      for (var i = 0; i < node.length; i++) {
        final value = node[i];
        if (value is int) node[i] = value + by;
      }
    } else if (node is Map) {
      for (final entry in node.entries.toList()) {
        if (entry.value is int) node[entry.key] = (entry.value as int) + by;
      }
    }
    return;
  }

  final rest = steps.sublist(1);
  if (key != '*') {
    if (node is Map) _shift(node[key], rest, by);
  } else if (node is List) {
    for (final value in node) {
      _shift(value, rest, by);
    }
  } else if (node is Map) {
    for (final value in node.values) {
      _shift(value, rest, by);
    }
  }
}

/// Moves every texture info found anywhere inside a material.
void _shiftTextures(Object? node, int by) {
  if (by == 0) return;
  if (node is List) {
    for (final item in node) {
      _shiftTextures(item, by);
    }
    return;
  }
  if (node is! Map) return;

  final index = node['index'];
  if (index is int && node.keys.every(_textureInfo.contains)) {
    node['index'] = index + by;
  }
  for (final value in node.values) {
    _shiftTextures(value, by);
  }
}

/// The model's own roots, moved on by [by].
///
/// A file with no scenes is legal and means a library of nodes rather than
/// something to draw; what it would draw is every node nobody claims as a
/// child, so that is what gets hung under ours.
List<int> _rootsOf(Map<String, Object?> model, int by) {
  final scenes = _items(model, 'scenes');
  if (scenes.isNotEmpty) {
    final which = model['scene'];
    final scene = which is int && which >= 0 && which < scenes.length
        ? scenes[which]
        : scenes.first;
    return [
      for (final node in (scene['nodes'] as List?) ?? const [])
        if (node is int) node + by,
    ];
  }

  final nodes = _items(model, 'nodes');
  final claimed = <int>{};
  for (final node in nodes) {
    for (final child in (node['children'] as List?) ?? const []) {
      if (child is int) claimed.add(child);
    }
  }
  return [
    for (var i = 0; i < nodes.length; i++)
      if (!claimed.contains(i)) i + by,
  ];
}

void _refuseLooseFiles(Map<String, Object?> model, String where) {
  final buffers = _items(model, 'buffers');
  if (buffers.length > 1) {
    throw GraftFailure(
      '$where keeps its data in ${buffers.length} buffers. An export can copy '
      'in a model that carries its bytes with it; cook it first, or save it '
      'as a single-buffer GLB.',
    );
  }
  if (buffers.isNotEmpty && buffers.first['uri'] != null) {
    throw GraftFailure(
      '$where keeps its bytes in ${buffers.first['uri']}, beside it rather '
      'than inside it. An export can only copy in a model that is whole; save '
      'it as a GLB and try again.',
    );
  }
  for (final image in _items(model, 'images')) {
    final uri = image['uri'];
    if (uri is String && !uri.startsWith('data:')) {
      throw GraftFailure(
        '$where reads a texture from $uri, beside it rather than inside it. '
        'An export can only copy in a model that is whole; save it as a GLB '
        'with its textures embedded and try again.',
      );
    }
  }
}

import 'surface_error.dart';
import 'surface_lines.dart';
import 'surface_modes.dart';
import 'surface_param.dart';

/// The result of reading one `.oshader`.
final class SurfaceTranslation {
  const SurfaceTranslation({
    required this.material,
    required this.params,
    required this.origins,
    required this.includes,
  });

  /// The Filament material file text.
  final String material;
  final List<SurfaceParam> params;

  /// Where each line of [material] came from, in order. The lines the
  /// translator wrote itself point at the last line of the author's file.
  final List<SurfaceOrigin> origins;

  /// The files that were included, as project paths.
  final Set<String> includes;

  /// Where matc's `line` (one-based) came from.
  SurfaceOrigin originOf(int line) =>
      origins[(line - 1).clamp(0, origins.length - 1)];
}

const _fragmentArguments =
    'inout vec3 ALBEDO, inout float ALPHA, inout float METALLIC, '
    'inout float ROUGHNESS, inout float AO, inout vec3 EMISSION, '
    'inout vec3 NORMAL_MAP, vec2 UV, float TIME';

final _fragmentHead = RegExp(r'\bvoid\s+fragment\s*\(\s*\)');
final _uniformStart = RegExp(r'^\s*(instance\s+)?uniform\b');
final _uniformShape = RegExp(
  r'^\s*(instance\s+)?uniform\s+(\w+)\s+(\w+)\s*(?::([^=]*))?(?:=(.*))?;\s*$',
);
final _modeLine = RegExp(r'^\s*render_mode\s+([^;]*);\s*$');
final _typeLine = RegExp(r'^\s*shader_type\s+(\w+)\s*;\s*$');
final _groupLine = RegExp(r'^\s*group_uniforms\s*([\w.]*)\s*;\s*$');
final _unsupported = RegExp(r'^\s*(?:varying\b|void\s+(vertex|light)\s*\()');

/// Turns an `.oshader` into a Filament material file.
///
/// Each declaration becomes a `#define` on the line it was written on, and the
/// entry point is added after the author's text, so a line number matc reports
/// in the material file is the author's own plus a fixed offset. Only text
/// from an `#include` shifts that, and [SurfaceTranslation.origins] says by
/// how much.
SurfaceTranslation translateSurface({
  required String name,
  required String path,
  required String source,
  SurfaceReader? read,
}) {
  final includes = <String>{};
  final lines = expandSurface(
    path,
    source,
    read ?? (_) => null,
    seen: includes,
  );
  final state = _Reading();
  final body = _rewrite(lines, state).toList();
  final last = lines.last.origin;
  if (!state.hasFragment) {
    throw SurfaceError(
      SurfaceOrigin(path, 1),
      'there is no fragment() function',
    );
  }
  final head = _head(name, state);
  final tail = _entry(state);
  return SurfaceTranslation(
    material: [...head, ...body.map((l) => l.text), ...tail].join('\n'),
    params: state.params,
    origins: [
      for (final _ in head) SurfaceOrigin(path, 1),
      ...body.map((l) => l.origin),
      for (final _ in tail) last,
    ],
    includes: includes,
  );
}

/// What the walk over the author's lines has found so far.
final class _Reading {
  final modes = SurfaceModes();
  final params = <SurfaceParam>[];
  String? group;
  bool hasFragment = false;
  bool usesNormal = false;
}

Iterable<SurfaceLine> _rewrite(List<SurfaceLine> lines, _Reading state) sync* {
  var depth = 0;
  for (var i = 0; i < lines.length; i++) {
    final line = lines[i];
    if (depth == 0) {
      final used = _declaration(lines, i, state);
      if (used != null) {
        yield used.first;
        for (var k = 1; k < used.consumed; k++) {
          yield lines[i + k].withText('');
        }
        i += used.consumed - 1;
        continue;
      }
    }
    depth += _braces(line.text);
    yield line.withText(_asFragment(line.text, state));
  }
}

int _braces(String text) =>
    '{'.allMatches(text).length - '}'.allMatches(text).length;

String _asFragment(String text, _Reading state) {
  state.usesNormal = state.usesNormal || text.contains('NORMAL_MAP');
  if (!_fragmentHead.hasMatch(text)) return text;
  state.hasFragment = true;
  return text.replaceFirst(
    _fragmentHead,
    'void orblit_fragment($_fragmentArguments)',
  );
}

/// A top level declaration read from `lines[at]` on, the text to put on its
/// first line, and how many lines it used. Null when the line is not one.
({SurfaceLine first, int consumed})? _declaration(
  List<SurfaceLine> lines,
  int at,
  _Reading state,
) {
  final line = lines[at];
  final text = line.text;
  final origin = line.origin;
  final unsupported = _unsupported.firstMatch(text);
  if (unsupported != null) {
    final word = unsupported.group(1) == null
        ? 'varying'
        : '${unsupported.group(1)}()';
    throw SurfaceError(origin, '`$word` is not supported yet');
  }
  if (_uniformStart.hasMatch(text)) return _uniform(lines, at, state);
  final handled = _directive(text, origin, state);
  return handled ? (first: line.withText(''), consumed: 1) : null;
}

bool _directive(String text, SurfaceOrigin at, _Reading state) {
  final type = _typeLine.firstMatch(text);
  if (type != null) {
    if (type.group(1) != 'spatial') {
      throw SurfaceError(at, 'shader_type ${type.group(1)} is not supported');
    }
    return true;
  }
  final modes = _modeLine.firstMatch(text);
  if (modes != null) {
    for (final mode in modes.group(1)!.split(',')) {
      state.modes.add(mode.trim(), at);
    }
    return true;
  }
  final group = _groupLine.firstMatch(text);
  if (group != null) {
    state.group = group.group(1)!.isEmpty ? null : group.group(1);
    return true;
  }
  return false;
}

({SurfaceLine first, int consumed}) _uniform(
  List<SurfaceLine> lines,
  int at,
  _Reading state,
) {
  final origin = lines[at].origin;
  var end = at;
  var joined = lines[at].text;
  while (!joined.contains(';')) {
    end++;
    if (end >= lines.length) {
      throw SurfaceError(origin, 'a uniform needs a `;`');
    }
    joined = '$joined ${lines[end].text}';
  }
  final match = _uniformShape.firstMatch(joined);
  if (match == null) throw SurfaceError(origin, 'cannot read this uniform');
  final param = _param(match, origin, state.group);
  state.params.add(param);
  final alias = param.type == SurfaceType.texture
      ? 'materialParams_${param.name}'
      : 'materialParams.${param.name}';
  return (
    first: lines[at].withText('#define ${param.name} $alias'),
    consumed: end - at + 1,
  );
}

SurfaceParam _param(RegExpMatch match, SurfaceOrigin at, String? group) {
  final type = SurfaceType.ofWord(match.group(2)!);
  if (type == null) throw SurfaceError(at, 'unknown type `${match.group(2)}`');
  final instance = match.group(1) != null;
  if (instance && type == SurfaceType.texture) {
    throw SurfaceError(at, 'a texture cannot be an instance uniform');
  }
  final hints = match.group(4) == null
      ? const <String, List<String>>{}
      : parseHints(match.group(4)!, at);
  final fallback = match.group(5);
  return SurfaceParam(
    name: match.group(3)!,
    type: type,
    line: at.line,
    instance: instance,
    group: group,
    hints: hints,
    defaultValue: fallback == null ? null : parseDefault(type, fallback, at),
  );
}

List<String> _head(String name, _Reading state) {
  final modes = state.modes;
  final id = name.replaceAll(RegExp(r'\W'), '_');
  return [
    'material {',
    '    name : "$id",',
    '    shadingModel : ${modes.unshaded ? 'unlit' : 'lit'},',
    '    blending : ${modes.blending},',
    if (modes.culling != null) '    culling : ${modes.culling},',
    if (modes.doubleSided) '    doubleSided : true,',
    if (modes.depthWrite != null) '    depthWrite : ${modes.depthWrite},',
    if (modes.depthCulling != null) '    depthCulling : ${modes.depthCulling},',
    '    requires : [ uv0 ],',
    if (state.params.isNotEmpty) '    parameters : [',
    for (var i = 0; i < state.params.length; i++)
      '        { type : ${state.params[i].type.matType}, '
          'name : "${state.params[i].name}" }${i + 1 < state.params.length ? ',' : ''}',
    if (state.params.isNotEmpty) '    ],',
    '}',
    'fragment {',
  ];
}

List<String> _entry(_Reading state) {
  final modes = state.modes;
  final color = modes.premultiply
      ? 'vec4(ALBEDO * ALPHA, ALPHA)'
      : 'vec4(ALBEDO, ALPHA)';
  return [
    // Before the entry point, which writes `material.baseColor` and
    // `material.emissive`: an alias of the same name would rewrite those into
    // fields that do not exist. Filament's own code follows the block and has
    // a `c` and an `n` of its own, so an alias must not outlive the author's
    // text either.
    for (final p in state.params) '#undef ${p.name}',
    '    void material(inout MaterialInputs material) {',
    '        vec3 ALBEDO = vec3(1.0);',
    '        float ALPHA = 1.0;',
    '        float METALLIC = 0.0;',
    '        float ROUGHNESS = 0.5;',
    '        float AO = 1.0;',
    '        vec3 EMISSION = vec3(0.0);',
    '        vec3 NORMAL_MAP = vec3(0.0, 0.0, 1.0);',
    '        orblit_fragment(ALBEDO, ALPHA, METALLIC, ROUGHNESS, AO, EMISSION, '
        'NORMAL_MAP, getUV0(), getUserTime().x);',
    if (state.usesNormal && !modes.unshaded)
      '        material.normal = NORMAL_MAP;',
    '        prepareMaterial(material);',
    '        material.baseColor = $color;',
    if (!modes.unshaded) ...[
      '        material.metallic = METALLIC;',
      '        material.roughness = ROUGHNESS;',
      '        material.ambientOcclusion = AO;',
    ],
    '        material.emissive = vec4(EMISSION, 1.0);',
    '    }',
    '}',
  ];
}

import 'package:orblit_light/orblit_light.dart';
import 'package:orblit_mesh/orblit_mesh.dart';
import 'package:vector_math/vector_math_64.dart';

import '../component.dart';
import '../components/drawing.dart';
import '../components/transform.dart';
import '../document.dart';
import '../entity.dart';
import '../material.dart';

/// A scene as OBJ, with the material library that goes beside it.
class ObjScene {
  const ObjScene({
    required this.obj,
    required this.mtl,
    this.problems = const [],
  });

  final String obj;
  final String mtl;
  final List<String> problems;
}

/// Writes [scene] as one flattened OBJ and its material library.
///
/// OBJ has no tree, no animation, no lights and no cameras, so this is a
/// photograph of the scene rather than the scene: every mesh is baked into
/// world space and given a group of its own, which is as much of the
/// structure as the format can hold. What it is good for is handing a shape
/// to something that reads nothing else — a CNC path, a physics tool, an
/// ancient importer — and for that it is the best format there is.
///
/// Everything the format cannot carry is listed in [ObjScene.problems] rather
/// than dropped in silence, so an export names what it left behind.
ObjScene sceneToObj(
  SceneDocument scene, {
  MaterialLibrary? materials,
  String library = 'scene.mtl',
}) {
  final obj = StringBuffer()
    ..writeln('# Written by Orblit')
    ..writeln('mtllib $library');
  final problems = <String>[];
  final named = <String, Map<String, Object?>>{};
  final nameOf = <String, String>{};

  final worlds = <String, Matrix4>{};
  Matrix4 worldOf(SceneEntity entity) {
    final cached = worlds[entity.id];
    if (cached != null) return cached;

    final transform = entity[SceneComponents.transform];
    final local = transform is TransformComponent
        ? _matrixOf(transform)
        : Matrix4.identity();
    final parent = entity.parent == null ? null : scene[entity.parent!];
    final world = parent == null ? local : worldOf(parent).multiplied(local);
    return worlds[entity.id] = world;
  }

  // OBJ counts vertices from one and counts them across the whole file, not
  // per object, so the running totals below are the format's own bookkeeping
  // rather than an optimisation.
  var vertices = 1;
  var normals = 1;
  var coordinates = 1;
  final taken = <String>{};

  for (final entity in scene.entities) {
    if (!entity.visible) continue;
    final mesh = entity[SceneComponents.mesh];
    if (mesh is! MeshComponent) {
      if (entity.has(SceneComponents.light) ||
          entity.has(SceneComponents.camera)) {
        problems.add(
          '${entity.name} is a light or a camera, and OBJ has no word for '
          'either. Export as glTF to keep it.',
        );
      }
      continue;
    }

    if (mesh.asset != null && mesh.geometry == null) {
      problems.add(
        '${entity.name} draws ${mesh.asset} from a file, and an OBJ cannot '
        'point at another model. Export as glTF to keep it.',
      );
      continue;
    }

    final geometry = mesh.geometry ?? mesh.shape?.build();
    if (geometry == null) continue;
    final triangles = geometry.triangulate();
    if (triangles.indices.isEmpty) continue;

    final world = worldOf(entity);
    // A normal is not a direction the way a corner is a place: scale a box
    // along one axis and its sloped faces tilt the other way. The inverse
    // transpose is what turns the corners' transform into the normals'.
    final direction = (world.getRotation()..invert()).transposed();

    final material = entity[SceneComponents.material];
    final asset = material is MaterialComponent ? material.asset : null;
    // Two entities that end up saying the same thing are one material, and
    // they are compared by what they say rather than by where it came from:
    // a colour a material overrides is not a difference. A file that restated
    // it per object would be four hundred identical blocks.
    final written = _mtl(asset, materials, mesh.colour);
    final key = written.entries.map((e) => '${e.key} ${e.value}').join('\n');
    final name = nameOf[key] ??= _name(entity, asset, taken);
    named[name] = written;

    obj
      ..writeln()
      ..writeln('o ${_clean(entity.name)}')
      ..writeln('g ${_clean(entity.name)}');

    final count = triangles.positions.length ~/ 3;
    for (var i = 0; i < count; i++) {
      final at = world.transformed3(
        Vector3(
          triangles.positions[i * 3],
          triangles.positions[i * 3 + 1],
          triangles.positions[i * 3 + 2],
        ),
      );
      obj.writeln('v ${_f(at.x)} ${_f(at.y)} ${_f(at.z)}');
    }

    final hasNormals = triangles.normals.length == triangles.positions.length;
    if (hasNormals) {
      for (var i = 0; i < count; i++) {
        final n = direction.transformed(
          Vector3(
            triangles.normals[i * 3],
            triangles.normals[i * 3 + 1],
            triangles.normals[i * 3 + 2],
          ),
        )..normalize();
        obj.writeln('vn ${_f(n.x)} ${_f(n.y)} ${_f(n.z)}');
      }
    }

    final hasUvs = triangles.uvs.length == count * 2;
    if (hasUvs) {
      for (var i = 0; i < count; i++) {
        // OBJ's v runs up from the bottom of the picture and glTF's runs down
        // from the top, so one of them has to be turned over and it is this
        // one.
        obj.writeln(
          'vt ${_f(triangles.uvs[i * 2])} ${_f(1 - triangles.uvs[i * 2 + 1])}',
        );
      }
    }

    obj.writeln('usemtl $name');
    for (var i = 0; i + 2 < triangles.indices.length; i += 3) {
      final face = [
        for (var c = 0; c < 3; c++)
          _corner(
            triangles.indices[i + c],
            vertices,
            hasUvs ? coordinates : null,
            hasNormals ? normals : null,
          ),
      ];
      obj.writeln('f ${face.join(' ')}');
    }
    if (triangles.groups.length > 1) {
      problems.add(
        '${entity.name} paints ${triangles.groups.length} materials onto its '
        'faces, and an OBJ group wears one. It is written in its first.',
      );
    }

    vertices += count;
    if (hasNormals) normals += count;
    if (hasUvs) coordinates += count;
  }

  final mtl = StringBuffer()..writeln('# Written by Orblit');
  for (final entry in named.entries) {
    mtl
      ..writeln()
      ..writeln('newmtl ${entry.key}');
    for (final value in entry.value.entries) {
      mtl.writeln('${value.key} ${value.value}');
    }
  }

  return ObjScene(obj: obj.toString(), mtl: mtl.toString(), problems: problems);
}

String _corner(int index, int vertices, int? coordinates, int? normals) {
  final v = index + vertices;
  if (coordinates == null && normals == null) return '$v';
  if (normals == null) return '$v/${index + coordinates!}';
  if (coordinates == null) return '$v//${index + normals}';
  return '$v/${index + coordinates}/${index + normals}';
}

Matrix4 _matrixOf(TransformComponent transform) =>
    Matrix4.translation(transform.position)
      ..multiply(Matrix4.rotationZ(radians(transform.rotation.z)))
      ..multiply(Matrix4.rotationY(radians(transform.rotation.y)))
      ..multiply(Matrix4.rotationX(radians(transform.rotation.x)))
      ..multiply(Matrix4.diagonal3(transform.scale));

/// A name no other material in this file has.
String _name(SceneEntity entity, String? asset, Set<String> taken) {
  final stem = _clean(
    asset == null ? entity.name : asset.split('/').last.split('.').first,
  );
  if (taken.add(stem)) return stem;
  var n = 2;
  while (!taken.add('$stem.$n')) {
    n++;
  }
  return '$stem.$n';
}

/// OBJ splits its lines on spaces, so a name with one in it is two names.
String _clean(String name) {
  final trimmed = name.trim().replaceAll(RegExp(r'\s+'), '_');
  return trimmed.isEmpty ? 'unnamed' : trimmed;
}

/// One material, as the lines an MTL file states it in.
///
/// The `P` keys are the de-facto PBR extension every modern tool that still
/// reads OBJ understands; the `K` keys beside them are what everything older
/// reads, so the file says the same thing twice in two vocabularies rather
/// than choosing which half of the world to work with.
Map<String, Object?> _mtl(
  String? asset,
  MaterialLibrary? materials,
  Tint colour,
) {
  final resolved = asset == null ? null : materials?.resolve(asset);
  final base = resolved?.numbers('baseColour');
  final linear = colour.linear;
  final rgb = base != null && base.length >= 3
      ? [base[0], base[1], base[2]]
      : [linear.x, linear.y, linear.z];
  final alpha = base != null && base.length == 4 ? base[3] : 1.0;

  final roughness = resolved?.number('roughness') ?? 0.8;
  final metallic = resolved?.number('metallic') ?? 0.0;
  final emissive = resolved?.numbers('emissive');
  final maps = resolved?.maps ?? const <String, String>{};

  return {
    'Kd': rgb.map(_f).join(' '),
    // Shininess from roughness by the usual inversion: mirror-smooth is a
    // thousand, fully rough is nothing.
    'Ns': _f((1 - roughness) * (1 - roughness) * 1000),
    // Metals reflect their own colour; everything else reflects about four
    // per cent of white, which is the number the whole of PBR is built on.
    'Ks': [
      for (final c in rgb) _f(c * metallic + 0.04 * (1 - metallic)),
    ].join(' '),
    'Pr': _f(roughness),
    'Pm': _f(metallic),
    if (emissive != null && emissive.length >= 3)
      'Ke': emissive.take(3).map(_f).join(' '),
    'd': _f(alpha),
    'illum': resolved?.choice('shading') == 'unlit' ? 0 : 2,
    if (maps['baseColour'] != null) 'map_Kd': maps['baseColour'],
    if (maps['normal'] != null) 'norm': maps['normal'],
    if (maps['metallicRoughness'] != null) 'map_Pr': maps['metallicRoughness'],
    if (maps['occlusion'] != null) 'map_Ka': maps['occlusion'],
    if (maps['emissive'] != null) 'map_Ke': maps['emissive'],
  };
}

/// A number short enough to read and exact enough to reopen.
String _f(double value) {
  if (!value.isFinite) return '0';
  if (value == value.roundToDouble() && value.abs() < 1e9) {
    return value.toInt().toString();
  }
  final text = value.toStringAsFixed(6);
  var end = text.length;
  while (end > 0 && text[end - 1] == '0') {
    end--;
  }
  if (end > 0 && text[end - 1] == '.') end--;
  return text.substring(0, end);
}

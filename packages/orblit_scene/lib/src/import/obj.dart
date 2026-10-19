import 'dart:typed_data';

import 'package:orblit_light/orblit_light.dart';
import 'package:orblit_mesh/orblit_mesh.dart';
import 'package:vector_math/vector_math_64.dart';

import '../component.dart';
import '../components/drawing.dart';
import '../document.dart';
import '../entity.dart';
import 'gltf.dart';

/// Reads a scene out of the bytes of a Wavefront `.obj`.
///
/// Each `o` or `g` group becomes one entity holding its faces as editable
/// geometry, which is how a glTF node with a mesh is read. An OBJ has no
/// tree, no lights and no cameras, so the entities sit at the top level and
/// have no transform.
///
/// [files] holds the `.mtl` the file names. What [readObj] could not read comes
/// back in [SceneImported.problems].
SceneImported objToScene(
  Uint8List bytes, {
  String name = 'Scene',
  Map<String, Uint8List> files = const {},
}) {
  final ObjModel model;
  try {
    model = readObj(bytes, files: files);
  } on FormatException catch (error) {
    throw SceneFormatException(error.message);
  }
  final problems = [...model.problems];
  final entities = <SceneEntity>[];
  for (final group in model.groups) {
    final part = model.meshOf(group);
    final wears = {for (final face in part.faces) face.material};
    if (wears.length > 1) {
      problems.add(
        'Group ${group.name} wears ${wears.length} materials. The colour of '
        'the first was kept for all of it.',
      );
    }
    entities.add(
      SceneEntity(
        id: 'group-${entities.length}',
        name: group.name,
        components: {SceneComponents.mesh: _mesh(model, part)},
      ),
    );
  }
  return SceneImported(
    document: SceneDocument(name: name, entities: entities),
    problems: problems,
  );
}

MeshComponent _mesh(ObjModel model, Mesh part) {
  final at = part.faces.first.material;
  // Faces are drawn in one colour, so the material indices stop here.
  for (final face in part.faces) {
    face.material = 0;
  }
  if (at >= model.materials.length) {
    return MeshComponent(geometry: part, authored: true);
  }
  final colour = model.materials[at].colour;
  return MeshComponent(
    geometry: part,
    colour: Tint.fromLinear(Vector3(colour[0], colour[1], colour[2])),
    authored: true,
  );
}

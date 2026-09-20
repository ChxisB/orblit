import 'dart:convert';
import 'dart:typed_data';

import 'package:orblit_mesh/orblit_mesh.dart';

import '../document.dart';
import '../material.dart';
import 'gltf.dart';
import 'obj.dart';

/// What a whole scene can be written as.
///
/// FBX is deliberately not here. Reading one is worth the trouble because
/// people have them; writing one is a proprietary format with no public
/// specification, and everything that opens an FBX opens a glTF.
enum SceneFormat {
  /// JSON with the bytes in a file beside it. The one to read in a text
  /// editor, and the one to put in version control.
  gltf('glTF', '.gltf'),

  /// The same document with the bytes packed in after it. One file, and the
  /// one to ship.
  glb('GLB', '.glb'),

  /// Corners and faces in world space, with a material library. Everything
  /// reads it and it keeps almost nothing.
  obj('OBJ', '.obj');

  const SceneFormat(this.label, this.extension);

  /// As it is written in a menu.
  final String label;

  final String extension;

  /// Whether the format keeps the tree, the lights, the cameras and the
  /// animation, rather than a flattened copy of the shapes.
  bool get keepsScene => this != SceneFormat.obj;
}

/// The files an export produced, and what it could not carry.
class SceneWritten {
  const SceneWritten(this.files, {this.problems = const []});

  /// In the order they should be saved. The first is the one to name the
  /// export after; the rest are the sidecars it points at.
  final List<Written> files;

  /// What the format could not hold, in the order it was met. Empty is the
  /// ordinary case and means nothing was lost.
  final List<String> problems;

  Written get first => files.first;
}

/// Writing a whole scene out.
extension SceneExport on SceneDocument {
  /// This scene as [format].
  ///
  /// [materials] resolves the `.omat` paths the scene names, without which a
  /// material is the colour its mesh was drawn in. [files] supplies the bytes
  /// of anything the scene points at — an imported model, a texture — keyed
  /// by the project path the scene names it by.
  ///
  /// Bytes rather than a way to read them, deliberately. This package has no
  /// filesystem in it and is not going to acquire one: the same export has to
  /// run in the editor, in a cook step and in a browser, and only the caller
  /// knows which of those it is in.
  SceneWritten writeAs(
    SceneFormat format, {
    String name = 'scene',
    MaterialLibrary? materials,
    Map<String, Uint8List> files = const {},
  }) {
    switch (format) {
      case SceneFormat.obj:
        final written = sceneToObj(
          this,
          materials: materials,
          library: '$name.mtl',
        );
        return SceneWritten([
          Written('$name.obj', _utf8(written.obj)),
          Written('$name.mtl', _utf8(written.mtl)),
        ], problems: written.problems);

      case SceneFormat.glb:
        final written = sceneToGltf(this, materials: materials, files: files);
        return SceneWritten([
          Written('$name.glb', glbBytes(written.json, written.binary)),
        ], problems: written.problems);

      case SceneFormat.gltf:
        final written = sceneToGltf(this, materials: materials, files: files);
        final empty = written.binary.isEmpty;
        final text = gltfText(
          written.json,
          buffer: empty ? null : '$name.bin',
          byteLength: empty ? null : written.binary.length,
        );
        return SceneWritten([
          Written('$name.gltf', _utf8(text)),
          if (!empty) Written('$name.bin', written.binary),
        ], problems: written.problems);
    }
  }
}

Uint8List _utf8(String text) => Uint8List.fromList(utf8.encode(text));

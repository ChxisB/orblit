import 'dart:typed_data';

import 'package:orblit_mesh/orblit_mesh.dart';

import '../document.dart';
import 'gltf.dart';
import 'obj.dart';

/// Reads a scene out of the bytes of a `.glb`, a `.gltf` or an `.obj`.
///
/// An OBJ is read as one entity for each of its groups, by [objToScene]. It
/// is told from glTF by its first byte, because no OBJ starts with a brace
/// and no glTF starts with anything else.
///
/// Which of the two it is comes from the bytes rather than from a name: a
/// `.gltf` that is really a GLB is an ordinary thing to be handed, and a
/// reader that trusts the extension fails on it with a JSON error nobody can
/// act on.
///
/// [files] supplies whatever the document points at by name — the `.bin`
/// beside a `.gltf` is the one that matters — keyed by the URI the document
/// names it by. Bytes rather than a way to read them, deliberately, and for
/// the reason the export side takes them: this package has no filesystem in
/// it and is not going to acquire one, because the same code has to run in the
/// editor, in a cook step and in a browser.
///
/// Throws [SceneFormatException] only when there is nothing to read at all —
/// bytes that are neither a GLB nor JSON, or JSON that is not a document.
/// Everything short of that comes back in [SceneImported.problems], because a
/// scene with one unreadable node is still a scene worth opening.
SceneImported readSceneFrom(
  Uint8List bytes, {
  String name = 'Scene',
  Map<String, Uint8List> files = const {},
}) {
  if (!_isGltf(bytes)) return objToScene(bytes, name: name, files: files);
  final ({Map<String, Object?> json, Uint8List? binary}) parts;
  try {
    parts = gltfParts(bytes, files: files);
  } on FormatException catch (error) {
    throw SceneFormatException(error.message);
  }
  return gltfToScene(parts.json, binary: parts.binary, name: name);
}

/// Whether [bytes] begin as a GLB does, or as the JSON object of a `.gltf`.
///
/// A `[` counts, so a JSON array is refused as a bad glTF and not read as an
/// OBJ that has no faces.
bool _isGltf(Uint8List bytes) {
  if (bytes.length >= 4 && String.fromCharCodes(bytes, 0, 4) == 'glTF') {
    return true;
  }
  // A `.gltf` saved with a byte order mark still starts as JSON.
  final bom =
      bytes.length >= 3 &&
      bytes[0] == 0xEF &&
      bytes[1] == 0xBB &&
      bytes[2] == 0xBF;
  final first = bytes.indexWhere((byte) => byte > 32, bom ? 3 : 0);
  return first >= 0 && (bytes[first] == 0x7B || bytes[first] == 0x5B);
}

import 'dart:convert';
import 'dart:typed_data';

import 'package:orblit_mesh/orblit_mesh.dart';

import '../document.dart';
import 'gltf.dart';

/// Reads a scene out of the bytes of a `.glb` or a `.gltf`.
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
  final chunks = glbChunks(bytes);
  if (chunks != null) {
    return gltfToScene(chunks.json, binary: chunks.binary, name: name);
  }

  final Object? decoded;
  try {
    decoded = jsonDecode(utf8.decode(bytes));
  } on FormatException catch (error) {
    throw SceneFormatException(
      'This is neither a GLB nor glTF JSON: ${error.message}',
    );
  }
  if (decoded is! Map<String, Object?>) {
    throw const SceneFormatException('A glTF document must be a JSON object.');
  }

  return gltfToScene(decoded, binary: _buffer(decoded, files), name: name);
}

/// The bytes the document's first buffer names, wherever they are.
///
/// Only the first, because that is the only one anything here writes and the
/// only one an accessor in a document we produced ever points into. A document
/// with several is read as far as its first, and the accessors that reach past
/// it say so themselves.
Uint8List? _buffer(Map<String, Object?> json, Map<String, Uint8List> files) {
  final buffers = json['buffers'];
  if (buffers is! List || buffers.isEmpty) return null;
  final first = buffers.first;
  if (first is! Map<String, Object?>) return null;

  final uri = first['uri'];
  if (uri is! String || uri.isEmpty) return null;

  if (uri.startsWith('data:')) {
    final comma = uri.indexOf(',');
    // Only base64 is worth reading: a percent-encoded binary buffer is legal
    // and is also several times the size, so nothing writes one.
    if (comma < 0 || !uri.substring(0, comma).endsWith(';base64')) return null;
    try {
      return base64Decode(uri.substring(comma + 1));
    } on FormatException {
      return null;
    }
  }

  // Named twice, because a URI is escaped and a filename is not: a buffer
  // written beside a scene called `old town.gltf` arrives as `old%20town.bin`
  // and is on disk under the name with the space in it.
  return files[uri] ?? files[Uri.decodeComponent(uri)];
}

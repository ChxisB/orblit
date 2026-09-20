import 'dart:convert';
import 'dart:typed_data';

import 'package:orblit_asset/src/gltf/atlas_pixels.dart';
import 'package:orblit_asset/src/gltf/document.dart';
import 'package:orblit_asset/src/gltf/model_atlas.dart';
import 'package:orblit_sprite/orblit_sprite.dart' show decodePng, encodePng;

/// A GLB holding `json` and `binary`, for tests that need a real container
/// rather than a mock of one.
Uint8List glb(Map<String, Object?> json, Uint8List binary) {
  final text = Uint8List.fromList(utf8.encode(jsonEncode(json)));
  final jsonLength = text.length + (4 - text.length % 4) % 4;
  final binLength = binary.length + (4 - binary.length % 4) % 4;
  final length = 12 + 8 + jsonLength + 8 + binLength;

  final out = Uint8List(length);
  final data = ByteData.sublistView(out);
  data.setUint32(0, 0x46546C67, Endian.little);
  data.setUint32(4, 2, Endian.little);
  data.setUint32(8, length, Endian.little);
  data.setUint32(12, jsonLength, Endian.little);
  data.setUint32(16, 0x4E4F534A, Endian.little);
  out.fillRange(20, 20 + jsonLength, 0x20);
  out.setRange(20, 20 + text.length, text);
  final at = 20 + jsonLength;
  data.setUint32(at, binLength, Endian.little);
  data.setUint32(at + 4, 0x004E4942, Endian.little);
  out.setRange(at + 8, at + 8 + binary.length, binary);
  return out;
}

/// A solid PNG, for tests that need an image a decoder will accept and a
/// colour they can recognise again on the far side of a pack.
Uint8List solidPng(int width, int height, List<int> rgba) {
  final pixels = Uint8List(width * height * 4);
  for (var i = 0; i < pixels.length; i += 4) {
    pixels.setRange(i, i + 4, rgba);
  }
  return encodePng(width, height, pixels);
}

/// One unit quad per material, each with its own UVs.
///
/// The smallest thing that is still a model: enough for the atlas to have
/// something to pack, to rewrite and to merge, and small enough that a failing
/// test says which texel is wrong.
class QuadModel {
  final Map<String, Object?> json = {
    'asset': {'version': '2.0'},
    'images': <Object?>[],
    'textures': <Object?>[],
    'materials': <Object?>[],
    'meshes': [
      {'primitives': <Object?>[]},
    ],
    'nodes': <Object?>[
      <String, Object?>{'mesh': 0},
    ],
    'scenes': <Object?>[
      <String, Object?>{
        'nodes': [0],
      },
    ],
    'scene': 0,
  };
  final BytesBuilder _bin = BytesBuilder(copy: false);
  int _at = 0;

  List<Object?> get _images => json['images'] as List<Object?>;
  List<Object?> get _textures => json['textures'] as List<Object?>;
  List<Object?> get materials => json['materials'] as List<Object?>;
  List<Object?> get primitives =>
      (json['meshes'] as List).first['primitives'] as List<Object?>;

  int _view(List<int> bytes) {
    final views = json.putIfAbsent('bufferViews', () => <Object?>[]) as List;
    views.add({'buffer': 0, 'byteOffset': _at, 'byteLength': bytes.length});
    _bin.add(bytes);
    _at += bytes.length;
    final pad = (4 - _at % 4) % 4;
    _bin.add(Uint8List(pad));
    _at += pad;
    return views.length - 1;
  }

  int _accessor(Map<String, Object?> value) {
    final list = json.putIfAbsent('accessors', () => <Object?>[]) as List;
    list.add(value);
    return list.length - 1;
  }

  /// Adds a texture over a solid image, returning its texture index.
  int texture(List<int> rgba, {int size = 8}) {
    _images.add({
      'bufferView': _view(solidPng(size, size, rgba)),
      'mimeType': 'image/png',
    });
    _textures.add({'source': _images.length - 1});
    return _textures.length - 1;
  }

  /// Adds a material, returning its index.
  int material(Map<String, Object?> value) {
    materials.add(value);
    return materials.length - 1;
  }

  /// Adds a quad drawn with [material], its corners at [uv].
  int quad(int material, {List<double>? uv, double x = 0}) {
    final positions = Float32List.fromList([
      x, 0, 0, //
      x + 1, 0, 0,
      x + 1, 1, 0,
      x, 1, 0,
    ]);
    final coords = Float32List.fromList(
      uv ?? const [0.0, 0.0, 1.0, 0.0, 1.0, 1.0, 0.0, 1.0],
    );
    final indices = Uint16List.fromList([0, 1, 2, 0, 2, 3]);

    final position = _accessor({
      'bufferView': _view(Uint8List.sublistView(positions)),
      'componentType': 5126,
      'count': 4,
      'type': 'VEC3',
      'min': [x, 0, 0],
      'max': [x + 1, 1, 0],
    });
    final texCoord = _accessor({
      'bufferView': _view(Uint8List.sublistView(coords)),
      'componentType': 5126,
      'count': 4,
      'type': 'VEC2',
    });
    final index = _accessor({
      'bufferView': _view(Uint8List.sublistView(indices)),
      'componentType': 5123,
      'count': 6,
      'type': 'SCALAR',
    });

    primitives.add(<String, Object?>{
      'attributes': <String, Object?>{
        'POSITION': position,
        'TEXCOORD_0': texCoord,
      },
      'indices': index,
      'material': material,
      'mode': 4,
    });
    return primitives.length - 1;
  }

  Uint8List toGlb() {
    final bytes = _bin.toBytes();
    json['buffers'] = [
      {'byteLength': bytes.length},
    ];
    return glb(json, bytes);
  }
}

/// Decodes the PNGs a [QuadModel] stored in its buffer.
ImageDecoder decoderOver(GltfDocument document) => (index, definition) async {
  final view = definition['bufferView'] as int?;
  if (view == null) return null;
  final png = decodePng(document.viewBytes(view));
  return Rgba(png.width, png.height, png.pixels);
};

/// The texel at [x], [y].
List<int> texelAt(Rgba image, int x, int y) {
  final at = (y * image.width + x) * 4;
  return image.pixels.sublist(at, at + 4);
}

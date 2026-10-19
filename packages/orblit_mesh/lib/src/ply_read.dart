import 'dart:convert';
import 'dart:math' as math;
import 'dart:typed_data';

import 'package:vector_math/vector_math_64.dart';

import 'mesh.dart';
import 'read_limits.dart';
import 'uv.dart';

/// Reads the PLY in [bytes], text or binary of either byte order, as a mesh.
///
/// Takes the `x`, `y` and `z` of each `vertex`, the coordinates when they are
/// named `s` and `t`, `u` and `v`, or `texture_u` and `texture_v`, and the
/// `vertex_indices` or `vertex_index` list of each `face`, kept as the polygon
/// it is. Everything else in the file is read past by the types its header
/// declares: colours, normals, edges and any other element.
///
/// Throws [FormatException] when the file is not a PLY, is cut short, holds a
/// number that is not finite, names a point that is not there, has no face, or
/// passes [limits]. A file of splats is refused by name, because the splat
/// loader reads those.
///
/// shortcut: the vertices must come before the faces, as every writer puts
/// them, and a file in the other order is refused as naming points that are
/// not there. Read the faces first and check them after when one turns up.
Mesh readPly(Uint8List bytes, {ReadLimits limits = const ReadLimits()}) {
  if (bytes.length > limits.bytes) {
    throw FormatException('A PLY over ${limits.bytes} bytes is not read.');
  }
  final header = _Header.parse(bytes, limits);
  final values = switch (header.format) {
    'ascii' => _Words(bytes, header.bodyStart),
    final other => _Binary(
      bytes,
      header.bodyStart,
      other == 'binary_little_endian' ? Endian.little : Endian.big,
    ),
  };
  return _Body(header, values, limits).read();
}

/// How many bytes each type a header may name takes in a binary file.
const _sizes = {
  'char': 1,
  'int8': 1,
  'uchar': 1,
  'uint8': 1,
  'short': 2,
  'int16': 2,
  'ushort': 2,
  'uint16': 2,
  'int': 4,
  'int32': 4,
  'uint': 4,
  'uint32': 4,
  'float': 4,
  'float32': 4,
  'double': 8,
  'float64': 8,
};

/// Types a list's length may be written in.
const _whole = {
  'char',
  'int8',
  'uchar',
  'uint8',
  'short',
  'int16',
  'ushort',
  'uint16',
  'int',
  'int32',
  'uint',
  'uint32',
};

/// The most a header may take, which is far more than any real one does.
const _headerBytes = 64 << 10;

/// Fields only a splat file has. A mesh has none of them.
const _splatFields = {'f_dc_0', 'opacity', 'scale_0', 'rot_0'};

final _space = RegExp(r'\s+');

final class _Property {
  const _Property(this.name, this.type, [this.lengthType]);

  final String name;

  /// The type of the value, or of each item when this is a list.
  final String type;

  /// The type the length of a list is written in, or null for one value.
  final String? lengthType;

  bool get isList => lengthType != null;
}

final class _Element {
  _Element(this.name, this.count);

  final String name;
  final int count;
  final List<_Property> properties = [];
}

final class _Header {
  const _Header(this.format, this.elements, this.bodyStart);

  final String format;
  final List<_Element> elements;
  final int bodyStart;

  static _Header parse(Uint8List bytes, ReadLimits limits) {
    final window = latin1.decode(
      Uint8List.sublistView(bytes, 0, math.min(bytes.length, _headerBytes)),
    );
    final end = window.indexOf('end_header');
    if (end < 0 || !window.startsWith('ply')) {
      throw const FormatException('This is not a PLY.');
    }
    var body = end + 'end_header'.length;
    if (body < bytes.length && bytes[body] == 13) body++;
    if (body >= bytes.length || bytes[body] != 10) {
      throw const FormatException('The PLY header does not end in a line.');
    }
    final lines = window.substring(0, end).split('\n');
    return _lines(lines, limits, body + 1);
  }

  static _Header _lines(List<String> lines, ReadLimits limits, int body) {
    String? format;
    final elements = <_Element>[];
    for (final line in lines) {
      final words = line.trim().split(_space);
      switch (words.first) {
        case 'format':
          format = words.length > 1 ? words[1] : null;
        case 'element':
          elements.add(_element(words, elements, limits));
        case 'property':
          if (elements.isEmpty) {
            throw const FormatException('A property of nothing.');
          }
          elements.last.properties.add(_property(words));
      }
    }
    const known = {'ascii', 'binary_little_endian', 'binary_big_endian'};
    if (!known.contains(format)) {
      throw FormatException('The PLY format "$format" is not one of three.');
    }
    return _Header(format!, elements, body);
  }

  static _Element _element(
    List<String> words,
    List<_Element> before,
    ReadLimits limits,
  ) {
    final count = words.length == 3 ? int.tryParse(words[2]) : null;
    final most = words.length > 1 && words[1] == 'face'
        ? limits.faces
        : limits.vertices;
    if (count == null || count < 0 || count > most) {
      throw FormatException('"${words.join(' ')}" is not a size to read.');
    }
    if (before.any((e) => e.name == words[1])) {
      throw FormatException('The PLY has two ${words[1]} elements.');
    }
    return _Element(words[1], count);
  }

  static _Property _property(List<String> words) {
    final list = words.length == 5 && words[1] == 'list';
    final type = list ? words[3] : (words.length == 3 ? words[1] : '');
    final lengthType = list ? words[2] : null;
    if (!_sizes.containsKey(type) || (list && !_whole.contains(lengthType))) {
      throw FormatException('"${words.join(' ')}" is not a property.');
    }
    return _Property(words.last, type, lengthType);
  }
}

/// The values of a PLY body, one type at a time.
abstract class _Values {
  num read(String type);

  /// Takes [count] values of [type] and drops them.
  void skip(String type, int count) {
    for (var i = 0; i < count; i++) {
      read(type);
    }
  }
}

final class _Words extends _Values {
  _Words(this._bytes, this._at);

  final Uint8List _bytes;
  int _at;

  @override
  num read(String type) {
    while (_at < _bytes.length && _bytes[_at] <= 32) {
      _at++;
    }
    final start = _at;
    while (_at < _bytes.length && _bytes[_at] > 32) {
      _at++;
    }
    // No number is this long, and a longer word is a binary file read as text.
    if (start == _at || _at - start > 40) {
      throw const FormatException('The PLY data ends or is not numbers.');
    }
    final word = String.fromCharCodes(_bytes, start, _at);
    return num.tryParse(word) ??
        (throw FormatException('"$word" is not a number.'));
  }
}

final class _Binary extends _Values {
  _Binary(Uint8List bytes, this._at, this._endian)
    : _data = ByteData.sublistView(bytes);

  final ByteData _data;
  final Endian _endian;
  int _at;

  @override
  num read(String type) {
    final at = _take(_sizes[type]!);
    return switch (type) {
      'char' || 'int8' => _data.getInt8(at),
      'uchar' || 'uint8' => _data.getUint8(at),
      'short' || 'int16' => _data.getInt16(at, _endian),
      'ushort' || 'uint16' => _data.getUint16(at, _endian),
      'int' || 'int32' => _data.getInt32(at, _endian),
      'uint' || 'uint32' => _data.getUint32(at, _endian),
      'float' || 'float32' => _data.getFloat32(at, _endian),
      _ => _data.getFloat64(at, _endian),
    };
  }

  @override
  void skip(String type, int count) => _take(_sizes[type]! * count);

  /// Moves past [size] bytes and answers where they began.
  int _take(int size) {
    if (_at + size > _data.lengthInBytes) {
      throw const FormatException('The PLY data ends too soon.');
    }
    _at += size;
    return _at - size;
  }
}

final class _Body {
  _Body(this._header, this._values, this._limits);

  final _Header _header;
  final _Values _values;
  final ReadLimits _limits;
  final Mesh _mesh = Mesh();
  final List<Vector2> _uvs = [];
  var _corners = 0;

  Mesh read() {
    for (final element in _header.elements) {
      switch (element.name) {
        case 'vertex':
          _vertices(element);
        case 'face':
          _faces(element);
        default:
          _skipElement(element);
      }
      if (_mesh.vertexCount > 0 && _mesh.faces.isNotEmpty) break;
    }
    if (_mesh.faces.isEmpty) {
      throw const FormatException('This PLY has no faces, so no model.');
    }
    return _mesh;
  }

  /// Where in the row of five a property goes: x, y, z, then the two
  /// coordinates. Minus one for anything else.
  static int _slot(_Property p) {
    if (p.isList) return -1;
    return switch (p.name) {
      'x' => 0,
      'y' => 1,
      'z' => 2,
      's' || 'u' || 'texture_u' || 'texture_s' => 3,
      't' || 'v' || 'texture_v' || 'texture_t' => 4,
      _ => -1,
    };
  }

  void _vertices(_Element element) {
    if (element.properties.any((p) => _splatFields.contains(p.name))) {
      throw const FormatException(
        'This PLY holds splats, which the splat loader reads and the model '
        'readers do not.',
      );
    }
    final slots = element.properties.map(_slot).toList();
    if (![0, 1, 2].every(slots.contains)) {
      throw const FormatException('The PLY vertices have no x, y and z.');
    }
    final hasUv = slots.contains(3) && slots.contains(4);
    final row = Float64List(5);
    for (var n = 0; n < element.count; n++) {
      for (var i = 0; i < slots.length; i++) {
        final value = _read(element.properties[i]);
        if (slots[i] >= 0) row[slots[i]] = value.toDouble();
      }
      if (!row.every((v) => v.isFinite)) {
        throw const FormatException('A PLY vertex is not a finite number.');
      }
      _mesh.positions.add(Vector3(row[0], row[1], row[2]));
      if (hasUv) _uvs.add(Vector2(row[3], row[4]));
    }
  }

  /// The value of [p], or for a list the number of items it dropped.
  num _read(_Property p) {
    if (!p.isList) return _values.read(p.type);
    final count = _length(p);
    _values.skip(p.type, count);
    return count;
  }

  int _length(_Property p) {
    final length = _wholeNumber(_values.read(p.lengthType!));
    if (length < 0) {
      throw const FormatException('A PLY list is of negative length.');
    }
    return length;
  }

  static int _wholeNumber(num value) {
    if (value is int) return value;
    if (value.isFinite && value == value.truncateToDouble()) {
      return value.toInt();
    }
    throw FormatException('$value is not a whole number.');
  }

  void _faces(_Element element) {
    final corners = element.properties
        .where(
          (p) =>
              p.isList &&
              (p.name == 'vertex_indices' || p.name == 'vertex_index'),
        )
        .firstOrNull;
    if (corners == null) {
      throw const FormatException('The PLY faces have no vertex_indices.');
    }
    for (var n = 0; n < element.count; n++) {
      List<int>? points;
      for (final p in element.properties) {
        if (identical(p, corners)) {
          points = _points(p);
        } else {
          _read(p);
        }
      }
      _mesh.faces.add(
        Face(
          points!,
          uv: _uvs.isEmpty
              ? null
              : FaceUv(manual: [for (final i in points) _uvs[i].clone()]),
        ),
      );
    }
  }

  List<int> _points(_Property p) {
    final count = _length(p);
    if (count < 3 || count > _limits.corners) {
      throw FormatException('A PLY face has $count corners.');
    }
    _corners += count;
    if (_corners > _limits.indices) {
      throw FormatException('More than ${_limits.indices} face corners.');
    }
    return [
      for (var i = 0; i < count; i++)
        _point(_wholeNumber(_values.read(p.type))),
    ];
  }

  int _point(int index) {
    if (index < 0 || index >= _mesh.vertexCount) {
      throw FormatException(
        'A PLY face names point $index, which is not there.',
      );
    }
    return index;
  }

  void _skipElement(_Element element) {
    if (element.properties.isEmpty) return;
    for (var n = 0; n < element.count; n++) {
      element.properties.forEach(_read);
    }
  }
}

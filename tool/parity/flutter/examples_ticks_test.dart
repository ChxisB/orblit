// Writes dotnet/fixtures/orblit_examples/ticks.json: what each engine example
// sends the renderer over its first 300 frames. Every frame is a whole scene,
// so the fixture keeps digests rather than the scenes. A digest of the values
// and one of the shape, meaning each key's type and length, for every frame
// and for every key across all frames. A port that differs is told the first
// frame and the key. It needs the Flutter package it reads, so
// tool/parity/flutter/regen.sh copies it into that package's test folder,
// runs it there and removes the copy.
import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_examples/orblit_examples.dart';

const _frames = 300;

// The view's own number, which the renderer gives and an example never sets.
const _dropped = {'textureId'};

/// FNV-1a over 64 bits. The C# test writes the same bytes the same way.
final class _Digest {
  var _hash = 0xcbf29ce484222325;

  void bytes(List<int> data) {
    for (final byte in data) {
      _hash = (_hash ^ byte) * 0x100000001b3;
    }
  }

  String get hex =>
      ((_hash >> 32) & 0xFFFFFFFF).toRadixString(16).padLeft(8, '0') +
      (_hash & 0xFFFFFFFF).toRadixString(16).padLeft(8, '0');
}

typedef _Form = ({String tag, int length, List<int> bytes});

Uint8List _raw(TypedData data) =>
    data.buffer.asUint8List(data.offsetInBytes, data.lengthInBytes);

List<int> _le32(int value) =>
    Uint8List(4)..buffer.asByteData().setInt32(0, value, Endian.little);

/// A string as its UTF-8 length and then its UTF-8 bytes.
List<int> _text(String value) {
  final encoded = utf8.encode(value);
  return [..._le32(encoded.length), ...encoded];
}

/// The type letter, the length and the bytes of one message value.
_Form _form(Object value) => switch (value) {
  final Float32List l => (tag: 'f', length: l.length, bytes: _raw(l)),
  final Int32List l => (tag: 'i', length: l.length, bytes: _raw(l)),
  final Int64List l => (tag: 'l', length: l.length, bytes: _raw(l)),
  final Uint8List l => (tag: 'b', length: l.length, bytes: l),
  final List<String> l => (
    tag: 's',
    length: l.length,
    bytes: [for (final s in l) ..._text(s)],
  ),
  final bool b => (tag: 'z', length: 1, bytes: [if (b) 1 else 0]),
  final double d => (
    tag: 'd',
    length: 1,
    bytes: _raw(Float64List.fromList([d])),
  ),
  final String s => (tag: 't', length: 1, bytes: _text(s)),
  _ => throw StateError('no form for ${value.runtimeType}'),
};

void _shape(_Digest into, _Form form) => into
  ..bytes(ascii.encode(form.tag))
  ..bytes(_le32(form.length));

void _value(_Digest into, _Form form) {
  _shape(into, form);
  into.bytes(form.bytes);
}

Map<String, Object?> _replay(Example example) {
  final camera = GalleryCamera.from(example.viewpoint).toRenderCamera();
  final frames = [
    for (var i = 0; i < _frames; i++)
      {
        for (final MapEntry(:key, :value)
            in example.scene(camera, i / 60).toMessage(1, at: i / 60).entries)
          if (!_dropped.contains(key)) key: _form(value),
      },
  ];
  final keys = {for (final frame in frames) ...frame.keys}.toList()..sort();
  final values = <String>[];
  final shapes = <String>[];
  for (final frame in frames) {
    final value = _Digest();
    final shape = _Digest();
    for (final key in frame.keys.toList()..sort()) {
      value.bytes(_text(key));
      shape.bytes(_text(key));
      _value(value, frame[key]!);
      _shape(shape, frame[key]!);
    }
    values.add(value.hex);
    shapes.add(shape.hex);
  }
  final keyValues = <String, String>{};
  final keyShapes = <String, String>{};
  for (final key in keys) {
    final value = _Digest();
    final shape = _Digest();
    for (final frame in frames) {
      // An absent key is a letter no value uses, so absent and empty differ.
      final form = frame[key] ?? (tag: 'n', length: 0, bytes: const <int>[]);
      _value(value, form);
      _shape(shape, form);
    }
    keyValues[key] = value.hex;
    keyShapes[key] = shape.hex;
  }
  return {
    'op': 'ticks',
    'name': example.name,
    'values': values,
    'shapes': shapes,
    'keyValues': keyValues,
    'keyShapes': keyShapes,
  };
}

String _commit(String root) {
  String git(List<String> args) =>
      (Process.runSync('git', args, workingDirectory: root).stdout as String)
          .trim();
  const path = 'packages/orblit_examples';
  final dirty = git(['status', '--porcelain', '--', path])
      .split('\n')
      .where((line) => line.isNotEmpty && !line.contains('/test/zz_'))
      .isNotEmpty;
  final commit = git(['log', '-1', '--format=%H', '--', path]);
  return dirty ? '$commit+dirty' : commit;
}

void main() {
  test('writes the example ticks fixture', () {
    TestWidgetsFlutterBinding.ensureInitialized();
    final root = Directory('../..').absolute.path;
    final cases = [for (final example in engineExamples()) _replay(example)];
    final body = cases.map((c) => '    ${jsonEncode(c)}').join(',\n');
    final file = File('$root/dotnet/fixtures/orblit_examples/ticks.json');
    file.parent.createSync(recursive: true);
    file.writeAsStringSync(
      '{\n'
      '  "package": "orblit_examples",\n'
      '  "sourceCommit": ${jsonEncode(_commit(root))},\n'
      '  "cases": [\n$body\n  ]\n'
      '}\n',
    );
  });
}

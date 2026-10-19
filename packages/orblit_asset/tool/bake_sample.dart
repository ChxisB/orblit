import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:vector_math/vector_math_64.dart';

/// Writes what the headless pixel check draws: a model, the same model baked
/// into six placements, and the placements.
///
/// Usage: `dart run tool/bake_sample.dart model.glb outdir [gap] [scale]`
///
/// The outputs are `single.glb`, `merged.glb` and `placements.txt`, with
/// sixteen numbers to a line in column-major order. The placements are a grid
/// of two rows of three, each turned a little further than the one before, and
/// the last is mirrored so the check sees winding and normals. A scale above
/// 1 makes every copy larger, for a model that is small beside the camera.
Future<void> main(List<String> arguments) async {
  if (arguments.length < 2) {
    stderr.writeln(
      'usage: bake_sample.dart <model.glb> <out directory> [gap] [scale]',
    );
    exit(2);
  }
  final model = File(arguments[0]).readAsBytesSync();
  final gap = arguments.length > 2 ? double.parse(arguments[2]) : 1.5;
  final scale = arguments.length > 3 ? double.parse(arguments[3]) : 1.0;
  final placements = [
    for (var i = 0; i < 6; i++)
      Matrix4.translation(Vector3((i % 3 - 1) * gap, 0, (i ~/ 3 - 0.5) * -gap))
        ..rotateY(0.4 * i)
        ..scaleByVector3(Vector3.all(scale)),
  ];
  placements.last.scaleByVector3(Vector3(-1, 1, 1));

  final merged = await bakeInstances(model, placements);
  if (merged == null) {
    stderr.writeln('bake_sample: the model cannot be baked');
    exit(1);
  }
  final out = Directory(arguments[1])..createSync(recursive: true);
  File('${out.path}/single.glb').writeAsBytesSync(model);
  File('${out.path}/merged.glb').writeAsBytesSync(merged);
  File('${out.path}/placements.txt').writeAsStringSync(
    '${placements.map((m) => m.storage.join(' ')).join('\n')}\n',
  );
  final each = _draws(model);
  stdout.writeln(
    '${placements.length} copies: $each draws each, '
    '${each * placements.length} in all, ${_draws(merged)} merged',
  );
}

/// The primitives a viewer draws for [glb]: one for each primitive of each
/// node that holds a mesh.
int _draws(Uint8List glb) {
  final length = ByteData.sublistView(glb).getUint32(12, Endian.little);
  final json =
      jsonDecode(utf8.decode(glb.sublist(20, 20 + length)))
          as Map<String, Object?>;
  final meshes = json['meshes']! as List<Object?>;
  var count = 0;
  for (final node in json['nodes']! as List<Object?>) {
    final mesh = (node! as Map<String, Object?>)['mesh'];
    if (mesh is int) {
      count += ((meshes[mesh]! as Map)['primitives'] as List).length;
    }
  }
  return count;
}

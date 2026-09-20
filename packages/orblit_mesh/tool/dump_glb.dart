// Writes one .glb per shape into a directory, for the validator to read.
import 'dart:io';

import 'package:orblit_mesh/orblit_mesh.dart';

void main(List<String> args) {
  final into = Directory(args.first)..createSync(recursive: true);

  void write(String name, List<int> bytes) =>
      File('${into.path}/$name.glb').writeAsBytesSync(bytes);

  for (final kind in ShapeKind.values) {
    write(kind.name, Shape(kind: kind).build().toGlb(name: kind.name));
  }

  write('empty', Mesh().toGlb());

  final painted = Shape.of(ShapeKind.cube).build();
  for (var i = 0; i < painted.faces.length; i++) {
    painted.faces[i].material = i % 3;
  }
  write(
    'painted',
    painted.toGlb(
      name: 'painted',
      materials: const [
        GlbMaterial(name: 'Brick', colour: [0.6, 0.2, 0.15, 1]),
        GlbMaterial(name: 'Glass', metallic: 1, roughness: 0.05),
        GlbMaterial(name: 'Leaf', cutout: true, doubleSided: true),
      ],
    ),
  );
}

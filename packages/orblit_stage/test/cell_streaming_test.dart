import 'dart:convert';
import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_cells/orblit_cells.dart';
import 'package:orblit_scene/orblit_scene.dart';
import 'package:orblit_stage/orblit_stage.dart';
import 'package:vector_math/vector_math_64.dart';

/// A cell of ten rocks laid out inside it, in world coordinates.
Uint8List rocks(Grid grid, Cell cell) {
  final centre = grid.centerOf(cell);
  return utf8.encode(
    SceneDocument(
      entities: [
        for (var i = 0; i < 10; i++)
          SceneEntity(
            id: 'rock$i',
            name: 'rock',
            components: {
              SceneComponents.transform: TransformComponent(
                position: Vector3(centre.x + i * 0.1, 0, centre.z),
              ),
              SceneComponents.mesh: const MeshComponent(asset: 'rock.glb'),
            },
          ),
      ],
    ).encode(),
  );
}

String positions(OrblitDocumentView view) =>
    (view.scene.objects.map((o) => o.transform.storage.join(',')).toList()
          ..sort())
        .join('\n');

void main() {
  test('a view fed cells draws what the streamed document holds', () async {
    final grid = Grid(10);
    var document = SceneDocument();
    final view = OrblitDocumentView(document);
    final streamer = CellStreamer(
      window: CellWindow(grid, loadRadius: 1),
      fetch: (cell) async => rocks(grid, cell),
      apply: (diff) {
        document = diff.applyTo(document);
        view.apply(diff);
      },
    );

    // Walks east across the world, a cell at a time, letting fetches land.
    for (var x = 5.0; x < 65; x += 10) {
      for (var frame = 0; frame < 6; frame++) {
        streamer.step(x, 5);
        await Future<void>.delayed(Duration.zero);
      }
      expect(view.scene.objects.length, document.length, reason: 'at $x');
      expect(
        positions(view),
        positions(OrblitDocumentView(document)),
        reason: 'at $x',
      );
    }

    // A cell stays until it is two away, so it does not flicker on a border.
    // At 55 that keeps cells 3 to 6, and nothing from the cells before it.
    expect(streamer.loaded.every((c) => c.x >= 3 && c.x <= 6), isTrue);
    expect(view.scene.objects.length, streamer.loaded.length * 10);
    expect(
      view.scene.objects.every((o) => o.transform.getTranslation().x > 30),
      isTrue,
    );
  });
}

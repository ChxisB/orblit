import 'package:orblit_scene/orblit_scene.dart';
import 'package:test/test.dart';

void main() {
  group('a terrain component', () {
    test('names no file and casts shadows when the file says nothing', () {
      final terrain = TerrainComponent.fromJson(const {});
      expect(terrain.file, isNull);
      expect(terrain.castShadows, isTrue);
      expect(terrain.receiveShadows, isTrue);
    });

    test('survives the round trip through a scene file', () {
      final document = SceneDocument(
        name: 'Scene',
        entities: [
          SceneEntity(
            id: 'ground',
            name: 'Ground',
            components: {
              SceneComponents.transform: TransformComponent(),
              SceneComponents.terrain: const TerrainComponent(
                file: 'terrain/world.oterrain',
                castShadows: false,
              ),
            },
          ),
        ],
      );

      final back = SceneDocument.decode(document.encode()).document;
      final terrain =
          back['ground']![SceneComponents.terrain]! as TerrainComponent;
      expect(terrain.file, 'terrain/world.oterrain');
      expect(terrain.castShadows, isFalse);
      expect(terrain.receiveShadows, isTrue);
    });

    test('writes only what is not the default', () {
      expect(const TerrainComponent(file: 'a.oterrain').toJson(), {
        'file': 'a.oterrain',
      });
      expect(const TerrainComponent().toJson(), isEmpty);
    });
  });
}

import 'package:orblit_scene/orblit_scene.dart';
import 'package:orblit_test/orblit_test.dart';
import 'package:test/test.dart';

import 'support.dart';

void main() {
  final scene = SceneDocument(
    entities: [
      thing('floor', name: 'Floor'),
      thing('crate1', name: 'Crate'),
      thing('crate2', name: 'Crate'),
      thing('street1/lamp3/bulb', name: 'Bulb'),
    ],
  );

  test('finds the one entity with a name', () {
    expect(scene.entityNamed('Floor').id, 'floor');
  });

  test('says what the names are when none matches', () {
    expect(
      () => scene.entityNamed('Roof'),
      throwsA(
        isA<StateError>().having(
          (e) => e.message,
          'message',
          allOf(contains('"Roof"'), contains('"Floor"'), contains('"Bulb"')),
        ),
      ),
    );
  });

  test('says which ids share a name when two do', () {
    expect(
      () => scene.entityNamed('Crate'),
      throwsA(
        isA<StateError>().having(
          (e) => e.message,
          'message',
          allOf(contains('crate1'), contains('crate2')),
        ),
      ),
    );
  });

  test('finds an entity inside a prefab instance by its path', () {
    expect(scene.entityAt('street1/lamp3/bulb').name, 'Bulb');
    expect(() => scene.entityAt('street1/lamp9/bulb'), throwsStateError);
  });
}

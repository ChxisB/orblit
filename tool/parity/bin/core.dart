import 'dart:typed_data';

import 'package:orblit_core/orblit_core.dart';
import 'package:orblit_parity_tool/fixture.dart';

/// Builds the world the C# test builds, from the same steps in the same order.
///
/// Both sides call the same C++, so this checks the wrapper: the handles, the
/// layouts, the views and the counts that each language reads back. The turns
/// are literals rather than sine and cosine, so no libm difference can reach a
/// local transform.
void main(List<String> args) {
  final world = World();
  final t = world.registerTransforms();
  final position = world.registerComponent(
    'Position',
    kind: ComponentKind.float32,
    arity: 3,
  );
  final velocity = world.registerComponent(
    'Velocity',
    kind: ComponentKind.float32,
    arity: 3,
  );

  final handles = <int>[];
  for (var i = 0; i < 40; i++) {
    final entity = world.createEntity();
    final turned = i % 3 == 0;
    world.add(
      entity,
      t.local,
      transform(
        x: i.toDouble(),
        y: (i % 5) * 0.5,
        z: -i * 0.25,
        rotationY: turned ? 0.7071068 : 0,
        rotationW: turned ? 0.7071068 : 1,
        scaleX: 1 + (i % 4) * 0.5,
      ),
    );
    world.add(entity, t.world);
    if (i.isOdd) {
      world.add(entity, t.parent, Int64List.fromList([handles[i - 1]]));
    }
    world.add(entity, position, Float32List.fromList([i.toDouble(), 0, 0]));
    world.add(entity, velocity, Float32List.fromList([i * 0.5, 1, 2]));
    handles.add(entity);
  }

  for (var i = 0; i < handles.length; i += 7) {
    world.destroyEntity(handles[i]);
  }
  for (var i = 0; i < 5; i++) {
    final entity = world.createEntity();
    world.add(entity, t.local, transform(x: 100.0 + i));
    world.add(entity, t.world);
    world.add(entity, position);
    handles.add(entity);
  }
  for (var i = 0; i < 40; i += 5) {
    if (world.isAlive(handles[i])) world.remove(handles[i], velocity);
  }

  final propagated = world.propagateTransforms();
  world
    ..tick(0.25)
    ..tick(0.25);

  writeFixture(
    root: args.single,
    package: 'orblit_core',
    name: 'core',
    cases: [
      {
        'kind': 'world',
        'entityCount': world.entityCount,
        'componentCount': world.componentCount,
        'version': world.version,
        'elapsed': world.elapsed,
        'propagated': propagated,
      },
      for (final entity in handles)
        {
          'kind': 'entity',
          'id': entity,
          'alive': world.isAlive(entity),
          'position': world.float32Of(entity, position)?.toList(),
          'velocity': world.float32Of(entity, velocity)?.toList(),
          'world': world.float32Of(entity, t.world)?.toList(),
        },
    ],
  );
  world.dispose();
}

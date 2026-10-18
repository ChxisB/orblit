import 'dart:async';
import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_stage/orblit_stage.dart';
import 'package:vector_math/vector_math_64.dart';

OrblitObject rock(
  int key, {
  num x = 0,
  num z = 0,
  bool visible = true,
  String? mesh = 'rock.glb',
  bool casts = true,
  int layer = 0,
  OrblitAnimation? animation,
  List<double>? morphWeights,
  List<OrblitJointPose>? joints,
  int? variant,
}) => OrblitObject(
  key: key,
  transform: Matrix4.translation(Vector3(x.toDouble(), 0, z.toDouble())),
  colour: Vector3(0.5, 0.25, 0.125),
  mesh: mesh,
  material: 1,
  castShadows: casts,
  layer: layer,
  visible: visible,
  animation: animation,
  morphWeights: morphWeights,
  joints: joints,
  variant: variant,
);

OrblitScene sceneOf(List<OrblitObject> objects) => OrblitScene(
  objects: objects,
  camera: OrblitCamera(position: Vector3(0, 5, 10), target: Vector3.zero()),
);

List<OrblitObject> rocks(int count, {num x = 0}) => [
  for (var i = 0; i < count; i++) rock(i, x: x + i),
];

/// A merge wired to a fake baker and a fake resource store.
class Rig {
  Rig({int settle = 2, int minimum = 3, int maxBakes = 8}) {
    merge = OrblitStaticMerge(
      bake: _bake,
      provide: (name, bytes) async {
        await provideGate?.future;
        provided[name] = bytes;
      },
      release: (name) async {
        released.add(name);
        return provided.remove(name) != null;
      },
      onReady: () => ready++,
      settle: settle,
      minimum: minimum,
      cellSize: 10,
      maxBakes: maxBakes,
    );
  }

  late final OrblitStaticMerge merge;
  final calls = <(String, List<Matrix4>)>[];
  final provided = <String, Uint8List>{};
  final released = <String>[];
  var ready = 0;

  /// Makes the baker wait, or the provide call wait.
  Completer<void>? bakeGate;
  Completer<void>? provideGate;
  Object? failure;
  var declines = false;

  Future<Uint8List?> _bake(String mesh, List<Matrix4> placements) async {
    calls.add((mesh, placements));
    await bakeGate?.future;
    if (failure != null) throw failure!;
    return declines ? null : Uint8List(4);
  }

  /// Observes [objects] for [frames] frames, then lets any bake land.
  Future<OrblitScene> watch(
    List<OrblitObject> objects, [
    int frames = 1,
  ]) async {
    final scene = sceneOf(objects);
    for (var i = 0; i < frames; i++) {
      merge.observe(scene);
    }
    await pumpEventQueue();
    return scene;
  }

  List<OrblitObject> drawn(List<OrblitObject> objects) =>
      merge.replace(sceneOf(objects)).objects;
}

List<double> xs(List<Matrix4> placements) => [
  for (final m in placements) m.getTranslation().x,
];

bool isMerged(OrblitObject object) => object.key >= 0x4d470000;

void main() {
  test('a group below the minimum is left alone', () async {
    final rig = Rig(minimum: 6);
    final objects = rocks(5);
    await rig.watch(objects, 5);

    expect(rig.calls, isEmpty);
    expect(rig.drawn(objects), hasLength(5));
  });

  test('a settled group becomes one object with the group settings', () async {
    final rig = Rig();
    final objects = [
      for (var i = 0; i < 5; i++) rock(i, x: i, casts: false, layer: 2),
    ];
    await rig.watch(objects, 2);
    expect(rig.calls, isEmpty, reason: 'still moving, by the rule of settle');

    await rig.watch(objects);
    expect(rig.calls, hasLength(1));
    expect(rig.calls.single.$1, 'rock.glb');
    expect(xs(rig.calls.single.$2), [0, 1, 2, 3, 4]);
    expect(rig.provided, hasLength(1));
    expect(rig.ready, 1);

    final out = rig.drawn(objects);
    expect(out, hasLength(1));
    final merged = out.single;
    expect(isMerged(merged), isTrue);
    expect(merged.mesh, rig.provided.keys.single);
    expect(merged.mesh, startsWith(OrblitResources.scheme));
    expect(merged.transform, Matrix4.identity());
    expect(merged.material, 1);
    expect(merged.colour, Vector3(0.5, 0.25, 0.125));
    expect(merged.castShadows, isFalse);
    expect(merged.layer, 2);
  });

  test('objects are drawn singly until the bake lands', () async {
    final rig = Rig()..bakeGate = Completer<void>();
    final objects = rocks(4);
    await rig.watch(objects, 3);

    expect(rig.calls, hasLength(1));
    expect(rig.drawn(objects), hasLength(4));
    expect(rig.ready, 0);

    rig.bakeGate!.complete();
    await pumpEventQueue();
    expect(rig.drawn(objects), hasLength(1));
    expect(rig.ready, 1);
  });

  test(
    'a member that moves puts the group back and bakes without it',
    () async {
      final rig = Rig();
      final objects = rocks(5);
      await rig.watch(objects, 3);
      expect(rig.drawn(objects), hasLength(1));

      final moved = [rock(0, x: 6.5), ...objects.skip(1)];
      // Before anything has observed the change.
      expect(rig.drawn(moved), hasLength(5));

      await rig.watch(moved);
      expect(rig.calls, hasLength(2));
      expect(xs(rig.calls.last.$2), [1, 2, 3, 4]);
      final out = rig.drawn(moved);
      expect(out, hasLength(2));
      expect(out.where(isMerged), hasLength(1));
      expect(out.firstWhere((o) => !isMerged(o)).key, 0);

      // It settles in its new place and joins again.
      await rig.watch(moved, 2);
      expect(rig.calls, hasLength(3));
      expect(xs(rig.calls.last.$2), [6.5, 1, 2, 3, 4]);
      expect(rig.drawn(moved), hasLength(1));
    },
  );

  test('hiding one object leaves the others drawn', () async {
    final rig = Rig();
    final objects = rocks(5);
    await rig.watch(objects, 3);

    final hidden = [
      ...objects.take(2),
      rock(2, x: 2, visible: false),
      ...objects.skip(3),
    ];
    expect(rig.drawn(hidden), hasLength(5), reason: 'all single at once');

    await rig.watch(hidden);
    expect(xs(rig.calls.last.$2), [0, 1, 3, 4]);
    final out = rig.drawn(hidden);
    expect(out, hasLength(2));
    expect(out.firstWhere((o) => !isMerged(o)).visible, isFalse);
  });

  test('an object that leaves the scene puts its group back', () async {
    final rig = Rig();
    final objects = rocks(5);
    await rig.watch(objects, 3);

    final fewer = objects.take(4).toList();
    expect(rig.drawn(fewer), hasLength(4));
    await rig.watch(fewer);
    expect(xs(rig.calls.last.$2), [0, 1, 2, 3]);
    expect(rig.drawn(fewer), hasLength(1));
  });

  test('a group that falls below the minimum goes back for good', () async {
    final rig = Rig(minimum: 4);
    final objects = rocks(4);
    await rig.watch(objects, 3);
    expect(rig.drawn(objects), hasLength(1));

    final fewer = objects.take(3).toList();
    await rig.watch(fewer, 3);
    expect(rig.calls, hasLength(1));
    expect(rig.drawn(fewer), hasLength(3));
  });

  test('objects that keep moving never start a bake', () async {
    final rig = Rig();
    for (var frame = 0; frame < 8; frame++) {
      await rig.watch([for (var i = 0; i < 5; i++) rock(i, x: i + frame)]);
    }

    expect(rig.calls, isEmpty);
  });

  test('two squares of ground are two merges', () async {
    final rig = Rig();
    final objects = [
      for (var i = 0; i < 3; i++) rock(i, x: 1 + i),
      for (var i = 0; i < 3; i++) rock(10 + i, x: 11 + i),
    ];
    await rig.watch(objects, 3);

    expect(rig.calls, hasLength(2));
    expect(rig.calls.map((c) => c.$2.length), [3, 3]);
    final out = rig.drawn(objects);
    expect(out, hasLength(2));
    expect(out.map((o) => o.key).toSet(), hasLength(2));
  });

  test('another mesh, another merge', () async {
    final rig = Rig();
    final objects = [
      for (var i = 0; i < 3; i++) rock(i, x: i),
      for (var i = 0; i < 3; i++) rock(10 + i, x: i, mesh: 'tree.glb'),
    ];
    await rig.watch(objects, 3);

    expect(rig.calls.map((c) => c.$1).toSet(), {'rock.glb', 'tree.glb'});
    expect(rig.drawn(objects), hasLength(2));
  });

  test('what moves by itself is not merged', () async {
    final rig = Rig();
    final alone = [
      rock(10, animation: const OrblitAnimation(clip: 0)),
      rock(11, morphWeights: const [0.5]),
      rock(12, joints: const []),
      rock(13, variant: 1),
      rock(14, visible: false),
      rock(15, mesh: null),
    ];
    final objects = [...rocks(3), ...alone];
    await rig.watch(objects, 3);

    expect(rig.calls, hasLength(1));
    expect(xs(rig.calls.single.$2), [0, 1, 2]);
    final out = rig.drawn(objects);
    expect(out.where((o) => !isMerged(o)).map((o) => o.key), [
      10,
      11,
      12,
      13,
      14,
      15,
    ]);
  });

  test('a baker that says no is not asked again for that model', () async {
    final rig = Rig()..declines = true;
    final objects = rocks(5);
    await rig.watch(objects, 3);
    expect(rig.calls, hasLength(1));
    expect(rig.merge.declined, {'rock.glb': null});
    expect(rig.drawn(objects), hasLength(5));

    await rig.watch([rock(0, x: 7), ...objects.skip(1)], 4);
    expect(rig.calls, hasLength(1));
  });

  test('a baker that throws leaves the objects and keeps the error', () async {
    final rig = Rig()..failure = StateError('no luck');
    final objects = rocks(5);
    await rig.watch(objects, 3);

    expect(rig.merge.declined['rock.glb'], isA<StateError>());
    expect(rig.drawn(objects), hasLength(5));
    expect(rig.ready, 0);
  });

  test('an error in onReady is not blamed on the model', () async {
    late final OrblitStaticMerge merge;
    merge = OrblitStaticMerge(
      bake: (mesh, placements) async => Uint8List(4),
      provide: (name, bytes) async {},
      release: (name) async => true,
      onReady: () => throw StateError('the caller'),
      settle: 0,
      minimum: 3,
    );
    // The error reaches the zone, as any callback error would, and the merge
    // still stands.
    final errors = <Object>[];
    await runZonedGuarded(() async {
      merge.observe(sceneOf(rocks(3)));
      await pumpEventQueue();
    }, (error, _) => errors.add(error));

    expect(errors.single, isA<StateError>());
    expect(merge.declined, isEmpty);
    expect(merge.replace(sceneOf(rocks(3))).objects, hasLength(1));
  });

  test('a group stops rebuilding after maxBakes', () async {
    final rig = Rig(maxBakes: 2);
    final objects = rocks(5);
    await rig.watch(objects, 3);
    final first = [rock(0, x: 6.5), ...objects.skip(1)];
    await rig.watch(first);
    final second = [first.first, rock(1, x: 7.5), ...objects.skip(2)];
    await rig.watch(second);

    expect(rig.calls, hasLength(2));
    expect(rig.drawn(second), hasLength(5));
  });

  test('a bake that was overtaken is dropped', () async {
    final rig = Rig()..bakeGate = Completer<void>();
    final objects = rocks(5);
    await rig.watch(objects, 3);
    await rig.watch([rock(0, x: 6.5), ...objects.skip(1)]);
    expect(rig.calls, hasLength(2));

    rig.bakeGate!.complete();
    await pumpEventQueue();
    expect(rig.provided, hasLength(1));
    expect(rig.ready, 1);
    final out = rig.drawn([rock(0, x: 6.5), ...objects.skip(1)]);
    expect(out.where(isMerged), hasLength(1));
  });

  test('with no settle a move in place still bakes again', () async {
    final rig = Rig(settle: 0);
    final objects = rocks(4);
    await rig.watch(objects);
    expect(rig.drawn(objects), hasLength(1));

    final moved = [rock(0, x: 6.5), ...objects.skip(1)];
    await rig.watch(moved);
    expect(rig.calls, hasLength(2));
    expect(xs(rig.calls.last.$2), [6.5, 1, 2, 3]);
    expect(rig.drawn(moved), hasLength(1));
  });

  test('a merge is not drawn without the members it was made of', () async {
    final rig = Rig();
    final objects = rocks(5);
    await rig.watch(objects, 3);

    final other = rocks(5, x: 3);
    expect(rig.drawn(other), hasLength(5));
    expect(rig.merge.replace(sceneOf(const [])).objects, isEmpty);
  });

  test('dispose releases what was provided', () async {
    final rig = Rig();
    await rig.watch(rocks(5), 3);
    final name = rig.provided.keys.single;

    await rig.merge.dispose();
    expect(rig.released, [name]);
    expect(rig.provided, isEmpty);
    expect(rig.drawn(rocks(5)), hasLength(5));
  });

  test('a file that lands after dispose is released at once', () async {
    final rig = Rig()..provideGate = Completer<void>();
    await rig.watch(rocks(5), 3);
    await rig.merge.dispose();

    rig.provideGate!.complete();
    await pumpEventQueue();
    expect(rig.released, hasLength(1));
    expect(rig.provided, isEmpty);
    expect(rig.ready, 0);
  });

  test('two merges never share a file name', () async {
    final a = Rig();
    final b = Rig();
    await a.watch(rocks(5), 3);
    await b.watch(rocks(5), 3);

    expect(a.provided.keys.single, isNot(b.provided.keys.single));
  });

  test('settings that cannot work are refused', () {
    Future<Uint8List?> bake(String mesh, List<Matrix4> placements) async =>
        null;
    expect(
      () => OrblitStaticMerge(bake: bake, settle: -1),
      throwsArgumentError,
    );
    expect(
      () => OrblitStaticMerge(bake: bake, minimum: 1),
      throwsArgumentError,
    );
    expect(
      () => OrblitStaticMerge(bake: bake, cellSize: 0),
      throwsArgumentError,
    );
    expect(
      () => OrblitStaticMerge(bake: bake, maxBakes: 0),
      throwsArgumentError,
    );
  });
}

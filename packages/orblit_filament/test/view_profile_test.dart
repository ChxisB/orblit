import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_profile/orblit_profile.dart';
import 'package:vector_math/vector_math_64.dart';

// The view's send, timed. The platform side is a stand-in that answers the
// way the real one does, so this checks the spans and nothing about drawing.

void main() {
  const channel = MethodChannel('orblit_filament');

  testWidgets('times the upload and the submit of a scene', (tester) async {
    final calls = <String>[];
    tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(channel, (
      call,
    ) async {
      calls.add(call.method);
      return call.method == 'create' ? 7 : null;
    });
    addTearDown(
      () => tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(
        channel,
        null,
      ),
    );
    final profiler = Profiler()..frame();

    await tester.pumpWidget(
      Directionality(
        textDirection: TextDirection.ltr,
        child: SizedBox(
          width: 200,
          height: 100,
          child: OrblitView(
            scene: OrblitScene(
              objects: const [],
              camera: OrblitCamera(
                position: Vector3(0, 2, 10),
                target: Vector3.zero(),
              ),
            ),
            profiler: profiler,
          ),
        ),
      ),
    );
    await tester.pump();
    await tester.pump();
    profiler.frame();

    expect(calls, containsAllInOrder(['create', 'setScene']));
    final spans = profiler.snapshot()['spans']! as Map<String, Object?>;
    expect(spans.keys, containsAll(['upload', 'submit']));
  });
}

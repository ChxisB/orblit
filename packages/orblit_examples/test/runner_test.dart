import 'dart:convert';
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_examples/src/examples/runner.dart';
import 'package:orblit_examples/src/examples/runner_art.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:vector_math/vector_math_64.dart' hide Colors;

void main() {
  const step = 1 / 60;

  test('the autopilot gets through a minute of every run it starts', () {
    // Which is the test that the game is fair: every row it lays has a way
    // through, found by something that only has the player's three moves.
    final game = RunnerExample()..autopilot = true;
    for (var run = 0; run < 3; run++) {
      game.restart();
      for (var i = 0; i < 60 * 60; i++) {
        game.update(step);
        expect(
          game.phase,
          RunnerPhase.running,
          reason:
              'run ${run + 1} ended at ${game.travelled.round()} m, '
              '${(i * step).toStringAsFixed(1)} s in',
        );
      }
      expect(game.travelled, greaterThan(900));
      expect(game.coins, greaterThan(20));
      expect(game.score, greaterThan(game.travelled.floor()));
    }
  });

  test('a run nobody steers ends in a crash', () {
    final game = RunnerExample()..start();
    var seconds = 0.0;
    while (game.phase == RunnerPhase.running && seconds < 60) {
      game.update(step);
      seconds += step;
    }
    expect(game.phase, RunnerPhase.over);
    expect(game.best, game.score);

    // And the score stops when the run does, rather than counting on while
    // the runner tumbles.
    final score = game.score;
    for (var i = 0; i < 120; i++) {
      game.update(step);
    }
    expect(game.score, score);
  });

  test('with crashing turned off, nothing ends the run', () {
    final game = RunnerExample()
      ..invincible = true
      ..start();
    for (var i = 0; i < 60 * 60; i++) {
      game.update(step);
    }
    expect(game.phase, RunnerPhase.running);
  });

  test('the whole scene is described, frame after frame', () {
    final game = RunnerExample();
    final camera = OrblitCamera(
      position: Vector3(0, 2, 8),
      target: Vector3.zero(),
    );
    // Before the models have reached the renderer — which in a test they
    // never do — only what needs no file is drawn.
    final first = game.scene(camera, 0);
    expect(first.objects.where((one) => one.mesh != null), isEmpty);
    expect(first.populations, hasLength(11));

    game
      ..autopilot = true
      ..start();
    for (var i = 1; i <= 600; i++) {
      final scene = game.scene(camera, i * step);
      final keys = scene.objects.map((one) => one.key).toList();
      expect(keys.toSet(), hasLength(keys.length), reason: 'frame $i');
    }
  });

  group('the files it makes', () {
    final art = RunnerArt.build();

    test('are named by version, so a changed model is a new name', () {
      for (final name in art.files.keys) {
        expect(
          name,
          startsWith('orblit:resource/runner/v${RunnerArt.version}/'),
        );
      }
    });

    test('are binary glTF that says what it holds', () {
      for (final MapEntry(key: name, value: bytes) in art.files.entries) {
        if (!name.endsWith('.glb')) continue;
        final data = ByteData.sublistView(bytes);
        expect(data.getUint32(0, Endian.little), 0x46546C67, reason: name);
        expect(data.getUint32(4, Endian.little), 2, reason: name);
        expect(data.getUint32(8, Endian.little), bytes.length, reason: name);

        final jsonLength = data.getUint32(12, Endian.little);
        expect(data.getUint32(16, Endian.little), 0x4E4F534A, reason: name);
        expect(jsonLength % 4, 0, reason: name);
        final json = jsonDecode(
          utf8.decode(bytes.sublist(20, 20 + jsonLength)),
        ) as Map<String, Object?>;

        final binAt = 20 + jsonLength;
        final binLength = data.getUint32(binAt, Endian.little);
        expect(data.getUint32(binAt + 4, Endian.little), 0x004E4942);
        expect(binAt + 8 + binLength, bytes.length, reason: name);

        final buffers = json['buffers']! as List;
        expect((buffers.single as Map)['byteLength'], binLength);
        for (final view in json['bufferViews']! as List) {
          final at = (view as Map)['byteOffset'] as int? ?? 0;
          expect(at % 4, 0, reason: name);
          expect(
            at + (view['byteLength'] as int),
            lessThanOrEqualTo(binLength),
          );
        }
        expect((json['meshes']! as List).single, isNotNull, reason: name);
      }
    });

    test('include textures as PNG', () {
      for (final name in [RunnerArt.asphalt, RunnerArt.grass]) {
        final bytes = art.files[name]!;
        expect(bytes.sublist(0, 8), [137, 80, 78, 71, 13, 10, 26, 10]);
      }
    });
  });

  testWidgets('is played from the keyboard', (tester) async {
    final game = RunnerExample();
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: Builder(builder: (context) => game.overlay(context, () {})!),
        ),
      ),
    );
    await tester.pump();
    expect(find.text('Tap or press Space to run'), findsOneWidget);

    await tester.sendKeyEvent(LogicalKeyboardKey.space);
    expect(game.phase, RunnerPhase.running);

    for (final key in [
      LogicalKeyboardKey.arrowLeft,
      LogicalKeyboardKey.arrowUp,
      LogicalKeyboardKey.keyD,
      LogicalKeyboardKey.arrowDown,
    ]) {
      await tester.sendKeyEvent(key);
      game.update(step);
    }
    expect(game.phase, RunnerPhase.running);

    await tester.sendKeyEvent(LogicalKeyboardKey.escape);
    expect(game.phase, RunnerPhase.paused);
    await tester.sendKeyEvent(LogicalKeyboardKey.enter);
    expect(game.phase, RunnerPhase.running);
  });
}

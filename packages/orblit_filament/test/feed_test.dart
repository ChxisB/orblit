import 'dart:typed_data';

import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:vector_math/vector_math_64.dart';

/// A feed as it crosses to the plugin and into the scene message.
///
/// What the platform does with a picture is native and is checked there.
/// What is checked here is that the calls say what every plugin reads and
/// that a feed travels in the video arrays the renderer already takes.
void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  const channel = MethodChannel('orblit_filament');
  final calls = <MethodCall>[];
  Object? Function(MethodCall call) answer = (_) => 7;

  setUp(() {
    calls.clear();
    answer = (_) => 7;
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(channel, (call) async {
          calls.add(call);
          return answer(call);
        });
  });

  tearDown(() {
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(channel, null);
  });

  Future<OrblitFeedSource> made() =>
      OrblitFeedSource.create(width: 1280, height: 720);

  OrblitScene sceneWith({
    List<OrblitVideo> videos = const [],
    List<OrblitFeed> feeds = const [],
    List<OrblitMaterial> materials = const [],
  }) => OrblitScene(
    objects: const [],
    videos: videos,
    feeds: feeds,
    materials: materials,
    camera: OrblitCamera(position: Vector3(0, 0, 5), target: Vector3.zero()),
  );

  group('a source', () {
    test('is made with its size and answers its number', () async {
      final source = await made();

      expect(calls.single.method, 'createFeed');
      expect(calls.single.arguments, {'width': 1280, 'height': 720});
      expect(source.id, 7);
      expect(source.width, 1280);
      expect(source.height, 720);
    });

    test('refuses a size with nothing in it before asking the platform', () {
      expect(
        OrblitFeedSource.create(width: 0, height: 720),
        throwsArgumentError,
      );
      expect(calls, isEmpty);
    });

    test('says so where the platform has no feeds', () {
      answer = (_) => throw MissingPluginException();
      expect(made(), throwsUnsupportedError);
    });

    test('says so where the web plugin has no code for it', () {
      answer = (_) => throw PlatformException(code: 'not-implemented');
      expect(made(), throwsUnsupportedError);
    });

    test('passes on any other platform failure', () {
      answer = (_) => throw PlatformException(code: 'no-feed', message: 'x');
      expect(made(), throwsA(isA<PlatformException>()));
    });

    test('does not take a number that is not one', () {
      answer = (_) => 0;
      expect(made(), throwsStateError);
    });

    test('is released once however many times it is asked', () async {
      final source = await made();
      calls.clear();

      await source.release();
      await source.release();

      expect(calls, hasLength(1));
      expect(calls.single.method, 'releaseFeed');
      expect(calls.single.arguments, {'feed': 7});
      expect(source.isReleased, isTrue);
    });
  });

  group('a feed in a scene', () {
    test('is named for its number and carries its orientation', () async {
      final source = await made();
      final feed = OrblitFeed(
        key: 3,
        source: source,
        rotation: 3,
        mirror: true,
      );

      expect(feed.path, 'feed:7');
      expect(feed.flags, (3 << 2) | 16);
      expect(
        OrblitFeed(key: 3, source: source).flags,
        0,
        reason: 'upright and not mirrored',
      );
    });

    test('is refused a fifth quarter turn', () async {
      final source = await made();
      expect(
        () => OrblitFeed(key: 1, source: source, rotation: 4),
        throwsA(isA<AssertionError>()),
      );
    });

    test(
      'travels after the videos, with neutral rate, volume and seek',
      () async {
        final source = await made();
        final message = sceneWith(
          videos: const [OrblitVideo(key: 5, path: '/tmp/a.mp4', rate: 2)],
          feeds: [OrblitFeed(key: 9, source: source, rotation: 1)],
        ).toMessage(0);

        expect(message['videoKeys'], Int64List.fromList([5, 9]));
        expect(message['videoPaths'], ['/tmp/a.mp4', 'feed:7']);
        expect(message['videoFlags'], Int32List.fromList([3, 1 << 2]));
        final params = message['videoParams']! as Float32List;
        expect(params.sublist(4), [1, 1, -1, 0]);
      },
    );

    test('is what a screen points at by its place in the list', () async {
      final source = await made();
      final message = sceneWith(
        videos: const [OrblitVideo(key: 5, path: '/tmp/a.mp4')],
        feeds: [OrblitFeed(key: 9, source: source)],
        materials: const [
          OrblitMaterial(key: 1, shading: OrblitShading.video, video: 9),
          OrblitMaterial(key: 2, shading: OrblitShading.video, video: 5),
        ],
      ).toMessage(0);

      expect(message['materialVideos'], Int32List.fromList([1, 0]));
    });

    test('may not share a key with a video', () async {
      final source = await made();
      final scene = sceneWith(
        videos: const [OrblitVideo(key: 5, path: '/tmp/a.mp4')],
        feeds: [OrblitFeed(key: 5, source: source)],
        materials: const [OrblitMaterial(key: 1)],
      );

      expect(() => scene.toMessage(0), throwsArgumentError);
    });

    test('is kept when the scene is copied', () async {
      final source = await made();
      final scene = sceneWith(feeds: [OrblitFeed(key: 9, source: source)]);

      expect(scene.copyWith(videos: const []).feeds, hasLength(1));
    });
  });
}

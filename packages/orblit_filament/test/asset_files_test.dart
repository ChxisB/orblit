import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_filament/orblit_filament.dart';

/// Where a bundled asset sits, as the plugin answers it.
///
/// The lookup is native, and a device is what checks it. What is checked
/// here is the message and what the caller makes of each answer.
void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  const channel = MethodChannel('orblit_filament/resources');
  final calls = <MethodCall>[];
  Object? Function(MethodCall call) answer = (_) => null;

  setUp(() {
    calls.clear();
    answer = (_) => null;
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(channel, (call) async {
          calls.add(call);
          return answer(call);
        });
  });

  tearDown(() {
    debugDefaultTargetPlatformOverride = null;
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(channel, null);
  });

  test('an asset is asked for by its key and answered as a range', () async {
    debugDefaultTargetPlatformOverride = TargetPlatform.android;
    answer = (_) => {'path': '/proc/self/fd/41', 'offset': 4096, 'length': 900};

    final file = await OrblitAssetFiles.of('assets/cooked/game.android.opack');

    expect(calls.single.method, 'assetFile');
    expect(calls.single.arguments, {'key': 'assets/cooked/game.android.opack'});
    expect(
      file,
      const OrblitAssetFile(
        path: '/proc/self/fd/41',
        offset: 4096,
        length: 900,
      ),
    );
  });

  test('an asset the application does not have is null', () async {
    debugDefaultTargetPlatformOverride = TargetPlatform.android;
    answer = (_) => throw PlatformException(code: 'not-found');

    expect(await OrblitAssetFiles.of('assets/none.opack'), isNull);
  });

  test('any other failure reaches the caller', () async {
    debugDefaultTargetPlatformOverride = TargetPlatform.android;
    answer = (_) => throw PlatformException(code: 'orblit_failed');

    expect(
      OrblitAssetFiles.of('assets/a.opack'),
      throwsA(isA<PlatformException>()),
    );
  });

  test('an answer that is not a range is an error', () async {
    debugDefaultTargetPlatformOverride = TargetPlatform.android;
    answer = (_) => {'path': '/x', 'offset': 'zero', 'length': 3};

    expect(OrblitAssetFiles.of('assets/a.opack'), throwsStateError);
  });

  test('another platform is not asked and has no range', () async {
    for (final platform in [
      TargetPlatform.iOS,
      TargetPlatform.macOS,
      TargetPlatform.linux,
      TargetPlatform.windows,
    ]) {
      debugDefaultTargetPlatformOverride = platform;
      expect(await OrblitAssetFiles.of('assets/a.opack'), isNull);
      expect(await OrblitAssetFiles.release('assets/a.opack'), isFalse);
    }
    expect(calls, isEmpty);
  });

  test('releasing says whether the plugin held the asset', () async {
    debugDefaultTargetPlatformOverride = TargetPlatform.android;
    answer = (_) => true;

    expect(await OrblitAssetFiles.release('assets/a.opack'), isTrue);
    expect(calls.single.method, 'releaseAssetFile');
    expect(calls.single.arguments, {'key': 'assets/a.opack'});
  });

  test('an asset with no key is refused before it is sent', () {
    debugDefaultTargetPlatformOverride = TargetPlatform.android;

    expect(() => OrblitAssetFiles.of(''), throwsArgumentError);
    expect(() => OrblitAssetFiles.release(''), throwsArgumentError);
    expect(calls, isEmpty);
  });
}

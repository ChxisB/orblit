import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_filament/orblit_filament.dart';

/// Bytes by name, as they cross to the plugin.
///
/// The store itself is native and checked by the C ABI's own test; what is
/// checked here is that the message says what every plugin reads.
void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  const channel = MethodChannel('orblit_filament/resources');
  final calls = <MethodCall>[];

  setUp(() {
    calls.clear();
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(channel, (call) async {
          calls.add(call);
          return call.method == 'release' ? true : null;
        });
  });

  tearDown(() {
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(channel, null);
  });

  test('a name is the path under the renderer\'s own prefix', () {
    expect(
      OrblitResources.nameFor('models/robot.glb'),
      'orblit:resource/models/robot.glb',
    );
  });

  test('providing sends the name and the bytes as they are', () async {
    final bytes = Uint8List.fromList([1, 2, 3, 250]);
    await OrblitResources.provide('orblit:resource/a.glb', bytes);

    expect(calls, hasLength(1));
    expect(calls.single.method, 'provide');
    final args = calls.single.arguments as Map;
    expect(args['name'], 'orblit:resource/a.glb');
    expect(args['bytes'], bytes);
  });

  test('releasing says whether there was anything to let go of', () async {
    expect(await OrblitResources.release('orblit:resource/a.glb'), isTrue);
    expect(calls.single.method, 'release');
  });

  test('a resource with no name is refused before it is sent', () {
    expect(
      () => OrblitResources.provide('', Uint8List(1)),
      throwsArgumentError,
    );
    expect(() => OrblitResources.release(''), throwsArgumentError);
    expect(calls, isEmpty);
  });
}

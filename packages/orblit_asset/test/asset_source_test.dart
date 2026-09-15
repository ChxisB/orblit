import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:test/test.dart';

final robot = AssetId.parse('models/robot.glb');
final crate = AssetId.parse('models/crate.glb');
final missing = AssetId.parse('models/missing.glb');

Matcher notFound(AssetId id) => throwsA(
  isA<AssetNotFound>()
      .having((error) => error.id, 'id', id)
      .having((error) => error.message, 'message', contains('$id')),
);

void main() {
  group('a memory source', () {
    test('reads what it was given', () async {
      final source = MemoryAssetSource({
        robot: [1, 2, 3],
      });
      expect(await source.read(robot), [1, 2, 3]);
      expect(await source.exists(robot), isTrue);
    });

    test('says which asset it does not have', () async {
      final source = MemoryAssetSource();
      expect(source.read(missing), notFound(missing));
      expect(await source.exists(missing), isFalse);
    });

    test('changing what was read does not change what is stored', () async {
      final given = [1, 2, 3];
      final source = MemoryAssetSource({robot: given});
      given[0] = 9;

      final first = await source.read(robot);
      first[1] = 9;
      expect(await source.read(robot), [1, 2, 3]);
    });
  });

  group('a callback source', () {
    test('reads whatever the function gives', () async {
      final source = CallbackAssetSource(
        (id) async => id == robot ? Uint8List.fromList([4, 5]) : null,
      );
      expect(await source.read(robot), [4, 5]);
      expect(await source.exists(robot), isTrue);
    });

    test('takes null to mean the asset is not there', () async {
      final source = CallbackAssetSource((id) async => null);
      expect(source.read(missing), notFound(missing));
      expect(await source.exists(missing), isFalse);
    });

    test('passes on any other failure rather than calling it missing', () {
      final source = CallbackAssetSource(
        (id) async => throw StateError('the bundle is broken'),
      );
      expect(source.read(robot), throwsStateError);
    });
  });

  group('a layered source', () {
    final top = MemoryAssetSource({
      robot: [1],
    });
    final bottom = MemoryAssetSource({
      robot: [2],
      crate: [3],
    });
    final layered = LayeredAssetSource([top, bottom]);

    test(
      'an asset in an earlier layer hides the same one further down',
      () async {
        expect(await layered.read(robot), [1]);
      },
    );

    test('an asset only further down is still found', () async {
      expect(await layered.read(crate), [3]);
      expect(await layered.exists(crate), isTrue);
    });

    test('an asset in no layer is not found', () async {
      expect(layered.read(missing), notFound(missing));
      expect(await layered.exists(missing), isFalse);
    });

    test('with no layers at all, nothing is found', () async {
      final empty = LayeredAssetSource([]);
      expect(empty.read(robot), notFound(robot));
      expect(await empty.exists(robot), isFalse);
    });

    test('a layer that fails to read is not skipped over', () {
      final broken = CallbackAssetSource(
        (id) async => throw StateError('the disk is on fire'),
      );
      expect(
        LayeredAssetSource([broken, bottom]).read(robot),
        throwsStateError,
      );
    });

    test('changing the list it was built from changes nothing', () async {
      final layers = <AssetSource>[bottom];
      final source = LayeredAssetSource(layers);
      layers.insert(0, top);
      expect(await source.read(robot), [2]);
    });
  });
}

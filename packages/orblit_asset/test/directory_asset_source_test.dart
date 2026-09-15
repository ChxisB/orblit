@TestOn('vm')
library;

import 'dart:io';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:test/test.dart';

void main() {
  late Directory temp;
  late String root;
  late String outside;

  String at(String base, String relative) =>
      [base, ...relative.split('/')].join(Platform.pathSeparator);

  Future<void> write(String path, List<int> bytes) async {
    final file = File(path);
    await file.parent.create(recursive: true);
    await file.writeAsBytes(bytes);
  }

  setUp(() async {
    temp = await Directory.systemTemp.createTemp('orblit_asset_source_');
    root = at(temp.path, 'project');
    outside = at(temp.path, 'elsewhere');
    await write(at(root, 'models/robot.glb'), [1, 2, 3]);
    await write(at(outside, 'secret.txt'), [4, 5, 6]);
  });

  tearDown(() => temp.delete(recursive: true));

  group(
    'a directory source',
    () {
      test('reads a file by its id, slashes being folders', () async {
        final source = DirectoryAssetSource(root);
        final id = AssetId.parse('models/robot.glb');
        expect(await source.read(id), [1, 2, 3]);
        expect(await source.exists(id), isTrue);
      });

      test('a file that is not there is not found', () async {
        final source = DirectoryAssetSource(root);
        final id = AssetId.parse('models/missing.glb');
        expect(source.read(id), throwsA(isA<AssetNotFound>()));
        expect(await source.exists(id), isFalse);
      });

      test('a folder is not an asset', () async {
        final source = DirectoryAssetSource(root);
        final id = AssetId.parse('models');
        expect(source.read(id), throwsA(isA<AssetNotFound>()));
        expect(await source.exists(id), isFalse);
      });

      test(
        'a directory that does not exist has nothing in it, and says so',
        () async {
          final gone = at(temp.path, 'nowhere');
          final source = DirectoryAssetSource(gone);
          final id = AssetId.parse('models/robot.glb');
          expect(
            source.read(id),
            throwsA(
              isA<AssetNotFound>().having(
                (error) => error.message,
                'message',
                contains('does not exist'),
              ),
            ),
          );
          expect(await source.exists(id), isFalse);
        },
      );

      test('a root reached through a link still reads', () async {
        // Every temporary directory on macOS already is one, /var being a link
        // to /private/var; this makes the same true on every platform.
        final linked = at(temp.path, 'linked-project');
        await Link(linked).create(root);
        expect(
          await DirectoryAssetSource(
            linked,
          ).read(AssetId.parse('models/robot.glb')),
          [1, 2, 3],
        );
      });

      test('a link to a file elsewhere inside the root is followed', () async {
        await Link(
          at(root, 'models/alias.glb'),
        ).create(at(root, 'models/robot.glb'));
        final source = DirectoryAssetSource(root);
        expect(await source.read(AssetId.parse('models/alias.glb')), [1, 2, 3]);
      });
    },
    skip: Platform.isWindows ? 'links need extra privileges on Windows' : false,
  );

  group(
    'a directory source refuses to read outside its root',
    () {
      // Checked by the reason rather than just the exception, so that none of
      // these can pass merely because the link was broken and there was no
      // file to find.
      final refusedAsOutside = throwsA(
        isA<AssetNotFound>().having(
          (error) => error.message,
          'message',
          contains('outside'),
        ),
      );

      test('through a linked folder that leads out', () async {
        await Link(at(root, 'leak')).create(outside);
        final source = DirectoryAssetSource(root);
        final id = AssetId.parse('leak/secret.txt');

        expect(source.read(id), refusedAsOutside);
        expect(await source.exists(id), isFalse);
      });

      test('through a linked file that leads out', () async {
        await Link(at(root, 'secret.txt')).create(at(outside, 'secret.txt'));
        final source = DirectoryAssetSource(root);
        final id = AssetId.parse('secret.txt');

        expect(source.read(id), refusedAsOutside);
        expect(await source.exists(id), isFalse);
      });

      test('through a relative link that climbs out with ".."', () async {
        await Link(at(root, 'models/up')).create('../../elsewhere');
        final source = DirectoryAssetSource(root);
        expect(
          source.read(AssetId.parse('models/up/secret.txt')),
          refusedAsOutside,
        );
      });

      test(
        'through a sibling whose name only starts with the root\'s',
        () async {
          // "project-evil" begins with "project", so a check on the bare prefix
          // without the separator after it would let this through.
          final sibling = '$root-evil';
          await write(at(sibling, 'secret.txt'), [7]);
          await Link(at(root, 'next-door')).create(sibling);
          expect(
            DirectoryAssetSource(
              root,
            ).read(AssetId.parse('next-door/secret.txt')),
            refusedAsOutside,
          );
        },
      );
    },
    skip: Platform.isWindows ? 'links need extra privileges on Windows' : false,
  );
}

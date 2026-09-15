import 'package:orblit_asset/orblit_asset.dart';
import 'package:test/test.dart';

void main() {
  group('an asset id', () {
    test('a well-formed id is kept exactly as it was written', () {
      expect(AssetId.parse('models/robot.glb').toString(), 'models/robot.glb');
      expect(AssetId.parse('robot.glb').toString(), 'robot.glb');
      expect(
        AssetId.parse('textures/metal plate.png').toString(),
        'textures/metal plate.png',
      );
    });

    test('two ids spelt the same are the same id', () {
      final a = AssetId.parse('models/robot.glb');
      final b = AssetId.parse('models/robot.glb');
      expect(a, b);
      expect(a.hashCode, b.hashCode);
      expect({a, b}, hasLength(1));
      expect(a, isNot(AssetId.parse('models/Robot.glb')));
    });

    test('its name and directory are the two halves of its path', () {
      final id = AssetId.parse('models/robots/robot.glb');
      expect(id.name, 'robot.glb');
      expect(id.directory, 'models/robots');
    });

    test('an id at the root has an empty directory, not a missing one', () {
      final id = AssetId.parse('robot.glb');
      expect(id.name, 'robot.glb');
      expect(id.directory, '');
    });

    test('the extension is lower-case and has no dot', () {
      expect(AssetId.parse('models/ROBOT.GLB').extension, 'glb');
      expect(AssetId.parse('scene.tar.gz').extension, 'gz');
    });

    test('a name with no dot, or only a leading one, has no extension', () {
      expect(AssetId.parse('README').extension, '');
      expect(AssetId.parse('config/.gitignore').extension, '');
      expect(AssetId.parse('notes.').extension, '');
      // A dot in a folder name is not the file's extension.
      expect(AssetId.parse('v1.2/README').extension, '');
    });

    test('tryParse gives null wherever parse would throw', () {
      expect(AssetId.tryParse('models/robot.glb'), isNotNull);
      expect(AssetId.tryParse('models//robot.glb'), isNull);
    });
  });

  group('an asset id is refused when', () {
    void refused(String text, String saying) {
      expect(
        () => AssetId.parse(text),
        throwsA(
          isA<FormatException>().having(
            (error) => error.message,
            'message',
            contains(saying),
          ),
        ),
      );
      expect(AssetId.tryParse(text), isNull);
    }

    test('it is empty', () {
      refused('', 'empty');
    });

    test('it uses a backslash', () {
      refused(r'models\robot.glb', 'backslash');
    });

    test('it has a scheme or a drive letter in it', () {
      refused('https://example.com/robot.glb', '":"');
      refused('C:/models/robot.glb', '":"');
      refused('asset:robot.glb', '":"');
    });

    test('it starts with a slash', () {
      refused('/models/robot.glb', 'starts with "/"');
    });

    test('it has an empty folder name, from a doubled or trailing slash', () {
      refused('models//robot.glb', 'empty folder name');
      refused('models/', 'empty folder name');
    });

    test('it has a "." in place of a folder', () {
      refused('./models/robot.glb', '"." folder');
      refused('models/./robot.glb', '"." folder');
      refused('.', '"." folder');
    });

    test('it has a ".." in place of a folder', () {
      refused('models/../robot.glb', '".."');
      refused('../robot.glb', '".."');
      refused('..', '".."');
    });

    test('the exception points at where the problem is', () {
      try {
        AssetId.parse('models/../robot.glb');
        fail('should have thrown');
      } on FormatException catch (error) {
        expect(error.source, 'models/../robot.glb');
        expect(error.offset, 7);
      }
    });

    test('dots inside a name are not mistaken for "." or ".."', () {
      expect(AssetId.tryParse('models/...glb'), isNotNull);
      expect(AssetId.tryParse('models/.hidden/robot.glb'), isNotNull);
    });
  });

  group('resolving a reference from inside an asset', () {
    final robot = AssetId.parse('models/robots/robot.gltf');

    test('a plain name is a sibling', () {
      expect(
        robot.resolve('robot.bin'),
        AssetId.parse('models/robots/robot.bin'),
      );
    });

    test('a relative path goes down from the directory', () {
      expect(
        robot.resolve('textures/metal.png'),
        AssetId.parse('models/robots/textures/metal.png'),
      );
    });

    test('percent escapes are decoded', () {
      expect(
        robot.resolve('metal%20plate.png'),
        AssetId.parse('models/robots/metal plate.png'),
      );
    });

    test('"." and ".." are collapsed', () {
      expect(
        robot.resolve('./textures/../metal.png'),
        AssetId.parse('models/robots/metal.png'),
      );
      expect(
        robot.resolve('../../shared/metal.png'),
        AssetId.parse('shared/metal.png'),
      );
    });

    test('a ".." that climbs past the root is refused', () {
      expect(robot.resolve('../../../secret.png'), isNull);
      expect(AssetId.parse('robot.gltf').resolve('../robot.bin'), isNull);
    });

    test('an escaped ".." is refused just the same', () {
      expect(robot.resolve('%2E%2E/%2E%2E/%2E%2E/secret.png'), isNull);
    });

    test('something resolving to the root itself is refused', () {
      expect(robot.resolve('../..'), isNull);
    });

    test('absolute paths and URIs with a scheme are refused', () {
      expect(robot.resolve('/etc/passwd'), isNull);
      expect(robot.resolve('https://example.com/metal.png'), isNull);
      expect(
        robot.resolve('data:application/octet-stream;base64,AAAA'),
        isNull,
      );
    });

    test(
      'references that decode to something that is not an id are refused',
      () {
        expect(robot.resolve(''), isNull);
        expect(robot.resolve('metal%zz.png'), isNull);
        expect(robot.resolve('metal%2'), isNull);
        expect(robot.resolve(r'textures%5Cmetal.png'), isNull);
        expect(robot.resolve('textures//metal.png'), isNull);
      },
    );
  });
}

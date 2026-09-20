import 'package:orblit_asset/orblit_asset.dart';
import 'package:test/test.dart';

void main() {
  AssetOrigin origin({FetchPolicy? policy}) => AssetOrigin.parse(
    'https://assets.example.com/game/v3',
    policy: policy ?? const FetchPolicy(),
  );

  String refusal(void Function() run) {
    try {
      run();
      fail('that should have been refused');
    } on FetchRefused catch (refused) {
      return refused.reason;
    }
  }

  group('where a base may be', () {
    test('a base is a directory whether or not it says so', () {
      expect(
        AssetOrigin.parse('https://e.example.com/a').base.toString(),
        'https://e.example.com/a/',
      );
      expect(
        AssetOrigin.parse('https://e.example.com/a/').base.toString(),
        'https://e.example.com/a/',
      );
    });

    test('plain HTTP is refused, and says how to allow it', () {
      final why = refusal(() => AssetOrigin.parse('http://e.example.com/a'));
      expect(why, contains('plain HTTP'));
      expect(why, contains('allowInsecure'));
    });

    test('a local test server can be allowed on purpose', () {
      expect(
        AssetOrigin.parse(
          'http://localhost:8080/assets',
          policy: const FetchPolicy(allowInsecure: true),
        ).base.host,
        'localhost',
      );
    });

    test('a scheme that is not the web at all is refused', () {
      expect(
        refusal(() => AssetOrigin.parse('file:///etc/passwd')),
        contains('file'),
      );
      expect(
        refusal(() => AssetOrigin.parse('/just/a/path')),
        contains('no scheme'),
      );
    });
  });

  group('where an asset may be', () {
    test('an id lands under the base', () {
      expect(
        origin().urlOf(AssetId.parse('models/robot.glb')).toString(),
        'https://assets.example.com/game/v3/models/robot.glb',
      );
    });

    test('a name with a space or a hash in it stays one name', () {
      final url = origin().urlOf(AssetId.parse('t/metal plate#2.png'));
      expect(url.toString(), endsWith('/t/metal%20plate%232.png'));
      // Not a fragment: the server is being asked for a file with a hash in
      // its name, and a bare # would ask for a different file entirely.
      expect(url.fragment, isEmpty);
      expect(url.pathSegments.last, 'metal plate#2.png');
    });
  });

  group('what a downloaded file may ask for', () {
    final model = Uri.parse(
      'https://assets.example.com/game/v3/models/robot.gltf',
    );

    test('a relative reference resolves beside the file that made it', () {
      expect(
        origin().beside(model, '../textures/metal.png').toString(),
        'https://assets.example.com/game/v3/textures/metal.png',
      );
    });

    test('a reference cannot climb out of the base', () {
      expect(
        refusal(() => origin().beside(model, '../../../secrets/keys.json')),
        contains('outside /game/v3/'),
      );
    });

    test('a reference cannot reach the local machine', () {
      expect(
        refusal(() => origin().beside(model, 'file:///etc/passwd')),
        contains('file'),
      );
    });

    test('a reference cannot wander to another host', () {
      expect(
        refusal(() => origin().beside(model, 'https://evil.example.net/x.png')),
        contains('evil.example.net'),
      );
    });

    test('a host that merely ends with an allowed one is not allowed', () {
      final listed = origin(
        policy: const FetchPolicy(hosts: {'cdn.example.com'}),
      );
      expect(listed.allows(Uri.parse('https://cdn.example.com/x.png')), isTrue);
      expect(
        listed.allows(Uri.parse('https://evil.cdn.example.com/x.png')),
        isFalse,
      );
      expect(
        listed.allows(Uri.parse('https://cdn.example.com.evil.net/x.png')),
        isFalse,
      );
    });

    test(
      'a listed host is allowed anywhere on it, being nobody else\'s base',
      () {
        // The base's own host is confined to the base's path, because a CDN
        // serves more than one project. A separately listed host was named on
        // purpose and has no path to be inside of.
        final listed = origin(
          policy: const FetchPolicy(hosts: {'cdn.example.com'}),
        );
        expect(
          listed
              .beside(model, 'https://cdn.example.com/anywhere/x.png')
              .toString(),
          'https://cdn.example.com/anywhere/x.png',
        );
      },
    );

    test('a scheme-relative reference is still held to the rules', () {
      expect(
        refusal(() => origin().beside(model, '//evil.example.net/x.png')),
        contains('evil.example.net'),
      );
    });
  });

  test('allowing adds hosts without losing the other limits', () {
    const strict = FetchPolicy(maxBytes: 5, maxPixels: 7, allowInsecure: true);
    final more = strict.allowing(['a.example.com']);
    expect(more.hosts, {'a.example.com'});
    expect(more.maxBytes, 5);
    expect(more.maxPixels, 7);
    expect(more.allowInsecure, isTrue);
  });
}

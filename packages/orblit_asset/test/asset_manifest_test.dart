import 'dart:convert';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:test/test.dart';

const emptyHash =
    'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855';
const abcHash =
    'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad';

void main() {
  final robot = AssetId.parse('models/robot.glb');
  final metal = AssetId.parse('textures/metal.png');

  AssetManifest sample() => AssetManifest(
    entries: {
      // Deliberately not in id order, so the sort is what is tested.
      metal: AssetEntry(ContentHash.parse(emptyHash), 0),
      robot: AssetEntry(ContentHash.of(utf8.encode('abc')), 3),
    },
  );

  group('a manifest survives a trip to text and back', () {
    test('every entry comes back the same', () {
      final before = sample();
      final load = AssetManifest.read(before.encode());
      expect(load.hasProblems, isFalse);
      expect(load.manifest.entries, before.entries);
    });

    test('an empty manifest is a valid file, not an error', () {
      final load = AssetManifest.read(AssetManifest().encode());
      expect(load.manifest.entries, isEmpty);
      expect(load.problems, isEmpty);
    });

    test('writing the same manifest twice gives the same bytes', () {
      // A file that reorders itself between writes turns every commit into a
      // diff nobody can review.
      expect(sample().encode(), sample().encode());
    });

    test('the order entries were added in makes no difference', () {
      final reversed = AssetManifest(
        entries: {
          robot: sample().entries[robot]!,
          metal: sample().entries[metal]!,
        },
      );
      expect(reversed.encode(), sample().encode());
    });

    test(
      'it is written sorted, indented by two spaces and ending in a newline',
      () {
        expect(sample().encode(), '''
{
  "formatVersion": 1,
  "assets": {
    "models/robot.glb": {
      "bytes": 3,
      "hash": "$abcHash"
    },
    "textures/metal.png": {
      "bytes": 0,
      "hash": "$emptyHash"
    }
  }
}
''');
      },
    );

    test('the entries cannot be changed behind its back', () {
      final manifest = sample();
      expect(
        () => manifest.entries[AssetId.parse('extra.bin')] = AssetEntry(
          ContentHash.parse(emptyHash),
          0,
        ),
        throwsUnsupportedError,
      );
    });
  });

  group('a manifest that cannot be read at all is refused', () {
    Matcher refusedSaying(String words) => throwsA(
      isA<FormatException>().having(
        (error) => error.message,
        'message',
        contains(words),
      ),
    );

    test('when it is not JSON', () {
      expect(
        () => AssetManifest.read('{"formatVersion": 1,'),
        refusedSaying('not JSON'),
      );
    });

    test('when it is not an object', () {
      expect(() => AssetManifest.read('[]'), refusedSaying('JSON object'));
    });

    test('when it does not say what version it is', () {
      expect(
        () => AssetManifest.read('{"assets": {}}'),
        refusedSaying('format version'),
      );
      expect(
        () => AssetManifest.read('{"formatVersion": "1", "assets": {}}'),
        refusedSaying('format version'),
      );
    });

    test('when a newer Orblit wrote it', () {
      // Refused rather than half-read: a newer file may mean something
      // different by the same keys.
      expect(
        () => AssetManifest.read('{"formatVersion": 2, "assets": {}}'),
        refusedSaying('newer'),
      );
    });

    test('when its version is one nothing has written', () {
      expect(
        () => AssetManifest.read('{"formatVersion": 0, "assets": {}}'),
        throwsFormatException,
      );
    });

    test('when it has no assets in it, rather than reading as empty', () {
      // Another Orblit file opened by mistake should say so, not look like a
      // project with nothing in it.
      expect(
        () => AssetManifest.read('{"formatVersion": 1, "objects": []}'),
        refusedSaying('"assets"'),
      );
    });
  });

  group('a bad entry is dropped and named, and the rest still read', () {
    String withEntries(Map<String, Object?> assets) =>
        jsonEncode({'formatVersion': 1, 'assets': assets});

    final good = {'bytes': 3, 'hash': abcHash};

    AssetManifestLoad readWith(String key, Object? entry) =>
        AssetManifest.read(withEntries({'models/robot.glb': good, key: entry}));

    void keepsTheGoodOne(AssetManifestLoad load) {
      expect(load.manifest.entries.keys, [robot]);
      expect(
        load.manifest.entries[robot],
        AssetEntry(ContentHash.parse(abcHash), 3),
      );
    }

    test('an id that is not one', () {
      final load = readWith(r'models\crate.glb', good);
      keepsTheGoodOne(load);
      expect(load.problems, hasLength(1));
      expect(load.problems.single, contains(r'models\crate.glb'));
      expect(load.problems.single, contains('left out'));
    });

    test('a hash that is not one', () {
      final load = readWith('models/crate.glb', {'bytes': 3, 'hash': 'abc'});
      keepsTheGoodOne(load);
      expect(load.problems.single, contains('"models/crate.glb"'));
      expect(load.problems.single, contains('not a content hash'));
    });

    test('no hash at all', () {
      final load = readWith('models/crate.glb', {'bytes': 3});
      keepsTheGoodOne(load);
      expect(load.problems.single, contains('"models/crate.glb"'));
      expect(load.problems.single, contains('no hash'));
    });

    test('a negative size', () {
      final load = readWith('models/crate.glb', {'bytes': -3, 'hash': abcHash});
      keepsTheGoodOne(load);
      expect(load.problems.single, contains('"models/crate.glb"'));
      expect(load.problems.single, contains('negative'));
    });

    test('a size that is not a whole number', () {
      final load = readWith('models/crate.glb', {
        'bytes': 3.5,
        'hash': abcHash,
      });
      keepsTheGoodOne(load);
      expect(load.problems.single, contains('"models/crate.glb"'));
    });

    test('an entry that is not an object', () {
      final load = readWith('models/crate.glb', 'crate');
      keepsTheGoodOne(load);
      expect(load.problems.single, contains('"models/crate.glb"'));
      expect(load.problems.single, contains('not an object'));
    });

    test('every bad entry gets its own sentence', () {
      final load = AssetManifest.read(
        withEntries({
          'models/robot.glb': good,
          '/absolute.glb': good,
          'models/crate.glb': {'bytes': -1, 'hash': abcHash},
        }),
      );
      keepsTheGoodOne(load);
      expect(load.problems, hasLength(2));
    });
  });
}

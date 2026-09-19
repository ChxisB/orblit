@TestOn('vm')
library;

import 'dart:convert';
import 'dart:io';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:test/test.dart';

ContentHash _hash(String text) => ContentHash.of(utf8.encode(text));

CookKey _key({
  String importer = 'texture',
  int version = 1,
  String source = 'wall.png',
  Map<String, Object?> settings = const {},
  Map<String, Object?> target = const {},
}) => CookKey(
  importer: importer,
  importerVersion: version,
  source: _hash(source),
  settings: settings,
  target: target,
);

/// What every cook cache has to do, whatever it keeps its bytes in.
void behavesLikeACookCache(CookCache Function() make) {
  test('a key nothing was cooked for is a miss', () async {
    expect(await make().lookUp(_key()), isNull);
  });

  test('what was stored comes back under the same key', () async {
    final cache = make();
    await cache.store(_key(), {'wall.ktx2': utf8.encode('cooked')});

    final found = await cache.lookUp(_key());
    expect(found, isNotNull);
    expect(found!.outputs.single.name, 'wall.ktx2');
    expect(await cache.read(found.outputs.single.hash), utf8.encode('cooked'));
  });

  test('a rebuild with nothing changed cooks nothing', () async {
    // The phase's whole reason for existing, said as a test.
    final cache = make();
    var cooks = 0;

    Future<void> build() async {
      for (final name in ['wall.png', 'floor.png', 'roof.png']) {
        final key = _key(source: name);
        if (await cache.lookUp(key) != null) continue;
        cooks++;
        await cache.store(key, {'$name.ktx2': utf8.encode('cooked $name')});
      }
    }

    await build();
    expect(cooks, 3);
    await build();
    expect(cooks, 3, reason: 'the second build should have cooked nothing');
  });

  test('changing one texture recooks only that texture', () async {
    final cache = make();
    for (final name in ['wall.png', 'floor.png']) {
      await cache.store(_key(source: name), {'out': utf8.encode(name)});
    }

    // wall.png was edited; floor.png was not.
    expect(await cache.lookUp(_key(source: 'wall.png edited')), isNull);
    expect(await cache.lookUp(_key(source: 'floor.png')), isNotNull);
  });

  test('every output of one cook travels with the others', () async {
    final cache = make();
    await cache.store(_key(), {
      'wall.astc.ktx2': utf8.encode('astc'),
      'wall.bc.ktx2': utf8.encode('bc'),
      'wall.etc2.ktx2': utf8.encode('etc2'),
    });

    final found = (await cache.lookUp(_key()))!;
    expect(found.outputs.map((output) => output.name), [
      'wall.astc.ktx2',
      'wall.bc.ktx2',
      'wall.etc2.ktx2',
    ]);
    expect(found['wall.bc.ktx2'], isNotNull);
    expect(found['wall.pvrtc.ktx2'], isNull);
  });

  test('outputs come back in name order however they were cooked', () async {
    final cache = make();
    final stored = await cache.store(_key(), {
      'z': utf8.encode('z'),
      'a': utf8.encode('a'),
    });
    expect(stored.outputs.map((output) => output.name), ['a', 'z']);
  });

  test('an output knows how big it is without being read', () async {
    final cache = make();
    final stored = await cache.store(_key(), {
      'out': utf8.encode('twelve bytes'),
    });
    expect(stored.outputs.single.bytes, 12);
    expect(stored.bytes, 12);
  });

  test('storing the same key twice is not an error', () async {
    // Two builds started at once, or an entry evicted between the look up and
    // the cook. Neither should fail on the way out.
    final cache = make();
    await cache.store(_key(), {'out': utf8.encode('first')});
    await cache.store(_key(), {'out': utf8.encode('second')});
    final found = (await cache.lookUp(_key()))!;
    expect(await cache.read(found.outputs.single.hash), utf8.encode('second'));
  });

  test('two cooks that agree share one copy of the bytes', () async {
    final cache = make();
    final one = await cache.store(_key(target: {'os': 'macos'}), {
      'out': utf8.encode('the same cooked bytes'),
    });
    final two = await cache.store(_key(target: {'os': 'ios'}), {
      'out': utf8.encode('the same cooked bytes'),
    });
    expect(one.outputs.single.hash, two.outputs.single.hash);
  });

  test('reading bytes nothing stored gives nothing', () async {
    expect(await make().read(_hash('never stored')), isNull);
  });

  test('a cook with no outputs at all is still a hit', () async {
    // An importer can legitimately produce nothing — a scene with no textures
    // in it. Re-running that every build is the bug.
    final cache = make();
    await cache.store(_key(), {});
    expect(await cache.lookUp(_key()), isNotNull);
    expect((await cache.lookUp(_key()))!.outputs, isEmpty);
  });

  group('remembering what would not cook', () {
    test('a key nothing failed on has nothing against it', () async {
      expect(await make().lookUpFailure(_key()), isNull);
    });

    test('what was recorded comes back, with why', () async {
      final cache = make();
      await cache.recordFailure(_key(), 'not a png at all');

      final found = await cache.lookUpFailure(_key());
      expect(found, isNotNull);
      expect(found!.reason, 'not a png at all');
    });

    test('it is remembered against that key and no other', () async {
      final cache = make();
      await cache.recordFailure(_key(target: {'os': 'macos'}), 'no good');
      expect(
        await cache.lookUpFailure(_key(target: {'os': 'ios'})),
        isNull,
        reason: 'a different target is different work',
      );
      expect(await cache.lookUpFailure(_key(source: 'other.png')), isNull);
    });

    test('recording one does not make it a hit', () async {
      final cache = make();
      await cache.recordFailure(_key(), 'no good');
      expect(
        await cache.lookUp(_key()),
        isNull,
        reason: 'nothing was cooked, so there is nothing to hand back',
      );
    });

    test('cooking it clears the failure', () async {
      final cache = make();
      await cache.recordFailure(_key(), 'the encoder was not installed');
      await cache.store(_key(), {'out': utf8.encode('cooked')});
      expect(await cache.lookUpFailure(_key()), isNull);
    });

    test('recording twice keeps the newer reason', () async {
      final cache = make();
      await cache.recordFailure(_key(), 'first');
      await cache.recordFailure(_key(), 'second');
      expect((await cache.lookUpFailure(_key()))!.reason, 'second');
    });
  });
}

void main() {
  group('in memory', () {
    behavesLikeACookCache(MemoryCookCache.new);
  });

  group('in a directory', () {
    late Directory temp;
    late String root;

    setUp(() async {
      temp = await Directory.systemTemp.createTemp('orblit_cook_cache_');
      root = '${temp.path}${Platform.pathSeparator}cache';
    });

    tearDown(() async {
      if (await temp.exists()) await temp.delete(recursive: true);
    });

    behavesLikeACookCache(
      () => DirectoryCookCache(
        '${Directory.systemTemp.createTempSync('orblit_cook_shared_').path}'
        '${Platform.pathSeparator}cache',
      ),
    );

    test('what one cache stored, another in the same place finds', () async {
      // The build after this one is a different process.
      await DirectoryCookCache(
        root,
      ).store(_key(), {'out': utf8.encode('cooked')});
      final found = await DirectoryCookCache(root).lookUp(_key());
      expect(found, isNotNull);
      expect(
        await DirectoryCookCache(root).read(found!.outputs.single.hash),
        utf8.encode('cooked'),
      );
    });

    test('the directory is made when it is first written to', () async {
      expect(await Directory(root).exists(), isFalse);
      await DirectoryCookCache(root).store(_key(), {'out': utf8.encode('x')});
      expect(await Directory(root).exists(), isTrue);
    });

    test('a look up in a directory that does not exist is a miss', () async {
      expect(await DirectoryCookCache(root).lookUp(_key()), isNull);
    });

    test(
      'an index nobody can read is an empty cache, not a broken one',
      () async {
        final cache = DirectoryCookCache(root);
        await cache.store(_key(), {'out': utf8.encode('cooked')});

        await File(
          '$root${Platform.pathSeparator}index.json',
        ).writeAsString('{ this is not json');

        expect(await DirectoryCookCache(root).lookUp(_key()), isNull);
        // And it recovers: the next store writes a readable index again.
        await DirectoryCookCache(root).store(_key(), {'out': utf8.encode('c')});
        expect(await DirectoryCookCache(root).lookUp(_key()), isNotNull);
      },
    );

    test('an index from a newer build is an empty cache', () async {
      await File(
        '$root${Platform.pathSeparator}index.json',
      ).create(recursive: true);
      await File('$root${Platform.pathSeparator}index.json').writeAsString(
        jsonEncode({'formatVersion': 99, 'entries': <String, Object?>{}}),
      );
      expect(await DirectoryCookCache(root).lookUp(_key()), isNull);
    });

    test('one unreadable entry does not cost the others', () async {
      final cache = DirectoryCookCache(root);
      await cache.store(_key(source: 'a'), {'out': utf8.encode('a')});
      await cache.store(_key(source: 'b'), {'out': utf8.encode('b')});

      final path = '$root${Platform.pathSeparator}index.json';
      final index =
          jsonDecode(await File(path).readAsString()) as Map<String, Object?>;
      (index['entries']! as Map<String, Object?>)[_key(source: 'a').hash.hex] =
          {'usedAt': 'not a time', 'outputs': <Object?>[]};
      await File(path).writeAsString(jsonEncode(index));

      final reopened = DirectoryCookCache(root);
      expect(await reopened.lookUp(_key(source: 'a')), isNull);
      expect(await reopened.lookUp(_key(source: 'b')), isNotNull);
    });

    test(
      'an entry whose bytes have gone is a miss, not a broken hit',
      () async {
        final cache = DirectoryCookCache(root);
        final stored = await cache.store(_key(), {
          'out': utf8.encode('cooked'),
        });

        final hash = stored.outputs.single.hash;
        await File(
          [root, 'content', hash.shard, hash.hex].join(Platform.pathSeparator),
        ).delete();

        expect(await cache.lookUp(_key()), isNull);
      },
    );

    test('the index is written whole or not at all', () async {
      final cache = DirectoryCookCache(root);
      await cache.store(_key(), {'out': utf8.encode('cooked')});
      final text = await File(
        '$root${Platform.pathSeparator}index.json',
      ).readAsString();
      expect(() => jsonDecode(text), returnsNormally);
      expect(text, endsWith('\n'));
      expect(
        await Directory(
          root,
        ).list().where((entry) => entry.path.endsWith('.incoming')).isEmpty,
        isTrue,
        reason: 'the temporary file should have been renamed away',
      );
    });

    test(
      'writers in the same directory do not lose each other\'s entries',
      () async {
        // Whether they are two objects here or two processes, the index is
        // read and written under one lock.
        final caches = [for (var i = 0; i < 4; i++) DirectoryCookCache(root)];
        await Future.wait([
          for (var i = 0; i < 4; i++)
            caches[i].store(_key(source: 'file$i'), {
              'out': utf8.encode('cooked $i'),
            }),
        ]);
        for (var i = 0; i < 4; i++) {
          expect(
            await DirectoryCookCache(root).lookUp(_key(source: 'file$i')),
            isNotNull,
            reason: 'file$i was written and then lost',
          );
        }
      },
    );

    test('two builds cooking at once do not lose each other', () async {
      // Really two processes, because that is the claim the file lock makes
      // and the one the in-process queue cannot stand in for.
      final names = ['first', 'second', 'third'];
      final runs = await Future.wait([
        for (final name in names)
          Process.run(Platform.resolvedExecutable, [
            'run',
            'test/src/store_one.dart',
            root,
            name,
          ]),
      ]);
      for (final run in runs) {
        expect(run.exitCode, 0, reason: '${run.stderr}');
      }
      for (final name in names) {
        expect(
          await DirectoryCookCache(root).lookUp(_key(source: name)),
          isNotNull,
          reason: '$name was cooked by another process and then lost',
        );
      }
    }, timeout: const Timeout(Duration(minutes: 2)));

    group('eviction', () {
      late DateTime now;
      DirectoryCookCache cacheOf(int limit) =>
          DirectoryCookCache(root, limitBytes: limit, clock: () => now);

      setUp(() => now = DateTime.utc(2026, 9, 19, 12));

      Future<void> storeOf(DirectoryCookCache cache, String name, int size) =>
          cache.store(_key(source: name), {
            'out': utf8.encode(name.padRight(size, '.').substring(0, size)),
          });

      test('the oldest entry goes first', () async {
        final cache = cacheOf(250);
        for (final name in ['first', 'second', 'third']) {
          await storeOf(cache, name, 100);
          now = now.add(const Duration(days: 1));
        }
        expect(await cache.lookUp(_key(source: 'first')), isNull);
        expect(await cache.lookUp(_key(source: 'second')), isNotNull);
        expect(await cache.lookUp(_key(source: 'third')), isNotNull);
      });

      test('an entry used recently outlives an older one', () async {
        final cache = cacheOf(250);
        await storeOf(cache, 'first', 100);
        now = now.add(const Duration(days: 1));
        await storeOf(cache, 'second', 100);

        // A build asks for the first one again, which is what keeps it.
        now = now.add(const Duration(days: 1));
        expect(await cache.lookUp(_key(source: 'first')), isNotNull);

        now = now.add(const Duration(days: 1));
        await storeOf(cache, 'third', 100);
        expect(await cache.lookUp(_key(source: 'second')), isNull);
        expect(await cache.lookUp(_key(source: 'first')), isNotNull);
      });

      test('the entry just cooked is never the one evicted', () async {
        // A single cook larger than the whole limit is still usable by the
        // build that just made it.
        final cache = cacheOf(50);
        await storeOf(cache, 'enormous', 500);
        expect(await cache.lookUp(_key(source: 'enormous')), isNotNull);
      });

      test('evicted bytes are taken off the disk', () async {
        final cache = cacheOf(150);
        final first = await cache.store(_key(source: 'first'), {
          'out': utf8.encode('x' * 100),
        });
        now = now.add(const Duration(days: 1));
        await cache.store(_key(source: 'second'), {
          'out': utf8.encode('y' * 100),
        });

        final hash = first.outputs.single.hash;
        expect(
          await File(
            [
              root,
              'content',
              hash.shard,
              hash.hex,
            ].join(Platform.pathSeparator),
          ).exists(),
          isFalse,
        );
      });

      test('bytes another entry still points at are kept', () async {
        // Two targets cooked to the same bytes. Evicting one must not take
        // the file out from under the other, which would turn its entry into
        // a hit that reads back nothing.
        final cache = cacheOf(150);
        final shared = utf8.encode('z' * 100);
        await cache.store(_key(source: 'first', target: {'os': 'macos'}), {
          'out': shared,
        });
        now = now.add(const Duration(days: 1));
        await cache.store(_key(source: 'first', target: {'os': 'ios'}), {
          'out': shared,
        });

        final kept = await cache.lookUp(
          _key(source: 'first', target: {'os': 'ios'}),
        );
        expect(kept, isNotNull);
        expect(await cache.read(kept!.outputs.single.hash), shared);
      });

      test('shared bytes are counted once, not once per entry', () async {
        // Both entries fit in the limit only because they are one file on
        // disk. Counting the bytes twice would evict a cache never over it.
        final cache = cacheOf(150);
        final shared = utf8.encode('z' * 100);
        await cache.store(_key(source: 'first'), {'out': shared});
        now = now.add(const Duration(days: 1));
        await cache.store(_key(source: 'second'), {'out': shared});
        expect(await cache.lookUp(_key(source: 'first')), isNotNull);
        expect(await cache.lookUp(_key(source: 'second')), isNotNull);
      });

      test('nothing is evicted without a limit', () async {
        final cache = DirectoryCookCache(root, clock: () => now);
        for (var i = 0; i < 20; i++) {
          await storeOf(cache, 'file$i', 1000);
          now = now.add(const Duration(days: 1));
        }
        expect(await cache.lookUp(_key(source: 'file0')), isNotNull);
      });

      test('a limit of nothing is refused', () {
        expect(
          () => DirectoryCookCache(root, limitBytes: 0),
          throwsArgumentError,
        );
        expect(
          () => DirectoryCookCache(root, limitBytes: -1),
          throwsArgumentError,
        );
      });
    });
  });
}

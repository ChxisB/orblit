import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:test/test.dart';

import 'net_ktx2_fixture.dart';

void main() {
  group('reading a mip chain', () {
    test('reads the header and every level', () {
      final chain = Ktx2Chain.read(mippedKtx2())!;
      expect(chain.width, 256);
      expect(chain.height, 256);
      expect(chain.depth, 0);
      expect(chain.levels, hasLength(9));
      // Smallest first in the file, largest first in the index.
      expect(chain.levels.first.offset, greaterThan(chain.levels.last.offset));
      expect(chain.levels.first.length, greaterThan(chain.levels.last.length));
      expect(chain.levels.last.end, lessThan(chain.levels.first.offset));
    });

    test('a single-level file has nothing smaller to offer', () {
      expect(Ktx2Chain.read(mippedKtx2(levels: 1)), isNull);
    });

    test('a file stored raw is left alone', () {
      // Every level of an uncompressed file has to stay aligned to its texel
      // block, and shortening the index moves all of them.
      expect(Ktx2Chain.read(mippedKtx2(supercompression: 0)), isNull);
    });

    test('anything that is not a KTX2 is not one', () {
      final png = Uint8List(2048)..setRange(0, 4, [0x89, 0x50, 0x4E, 0x47]);
      expect(Ktx2Chain.read(png), isNull);
    });

    test('a header that has not all arrived yet says so', () {
      final whole = mippedKtx2();
      expect(Ktx2Chain.read(whole.sublist(0, 200)), isNull);
      expect(Ktx2Chain.read(whole.sublist(0, 80 + 9 * 24)), isNotNull);
    });

    test('a file with its description after the levels is refused', () {
      // The prefix has to carry the format description with it, which only
      // works when the description comes first.
      expect(Ktx2Chain.read(mippedKtx2(metadataLast: true)), isNull);
    });
  });

  group('choosing a level', () {
    test('takes the coarsest level still at least the size asked for', () {
      final chain = Ktx2Chain.read(mippedKtx2())!;
      // 256, 128, 64, 32 ... so 64 is level 2.
      expect(chain.levelAtLeast(64), 2);
      expect(chain.levelAtLeast(60), 2);
      expect(chain.levelAtLeast(32), 3);
    });

    test('never the whole picture and never the last level', () {
      final chain = Ktx2Chain.read(mippedKtx2())!;
      // Only level 0 is 256 across, and level 0 is the whole download.
      expect(chain.levelAtLeast(256), isNull);
      expect(chain.levelAtLeast(4096), isNull);
      // The 1x1 level is not worth a stage of its own either, so the
      // smallest answer is the one above it.
      expect(chain.levelAtLeast(1), 7);
      expect(
        Ktx2Chain.read(mippedKtx2(width: 512, height: 512))!.levelAtLeast(256),
        1,
      );
    });

    test('the bytes needed are the end of that level', () {
      final chain = Ktx2Chain.read(mippedKtx2())!;
      expect(chain.bytesFor(2), chain.levels[2].end);
      expect(chain.bytesFor(2), lessThan(chain.levels[0].end));
    });
  });

  group('building a smaller file out of the front of a bigger one', () {
    test('says how big it is now, and how many levels it has', () {
      final whole = mippedKtx2();
      final chain = Ktx2Chain.read(whole)!;
      final small = chain.prefix(whole, 3)!;

      final smaller = Ktx2Chain.read(small)!;
      expect(smaller.width, 32);
      expect(smaller.height, 32);
      expect(smaller.levels, hasLength(6));
      expect(small.length, lessThan(whole.length));
    });

    test('the levels it kept are byte for byte the ones it had', () {
      final whole = mippedKtx2();
      final chain = Ktx2Chain.read(whole)!;
      final small = chain.prefix(whole, 3)!;
      final smaller = Ktx2Chain.read(small)!;

      for (var i = 0; i < smaller.levels.length; i++) {
        final was = chain.levels[3 + i];
        final now = smaller.levels[i];
        expect(now.length, was.length);
        expect(now.uncompressedLength, was.uncompressedLength);
        expect(
          small.sublist(now.offset, now.end),
          whole.sublist(was.offset, was.end),
        );
      }
    });

    test('the description and the key-value data travel with it', () {
      final whole = mippedKtx2();
      final small = Ktx2Chain.read(whole)!.prefix(whole, 2)!;
      final view = ByteData.sublistView(small);
      final dfdAt = view.getUint32(48, Endian.little);
      final kvdAt = view.getUint32(56, Endian.little);

      expect(view.getUint32(52, Endian.little), 44);
      expect(view.getUint32(60, Endian.little), 20);
      expect(small.sublist(dfdAt, dfdAt + 44), everyElement(0xD0));
      expect(small.sublist(kvdAt, kvdAt + 20), everyElement(0xCE));
      // Where nothing was, nothing is: an absent part keeps its nought
      // rather than being shifted into pointing at something.
      expect(view.getUint64(64, Endian.little), 0);
    });

    test('nothing overlaps and nothing points past the end', () {
      final whole = mippedKtx2();
      final small = Ktx2Chain.read(whole)!.prefix(whole, 1)!;
      final smaller = Ktx2Chain.read(small)!;
      expect(smaller.levels.first.end, small.length);
      for (final level in smaller.levels) {
        expect(level.offset, greaterThanOrEqualTo(smaller.metadataEnd));
        expect(level.end, lessThanOrEqualTo(small.length));
      }
    });

    test('a prefix that has not all arrived yet builds nothing', () {
      final whole = mippedKtx2();
      final chain = Ktx2Chain.read(whole)!;
      final short = whole.sublist(0, chain.bytesFor(2) - 1);
      expect(chain.prefix(short, 2), isNull);
      expect(chain.prefix(whole.sublist(0, chain.bytesFor(2)), 2), isNotNull);
    });

    test('level nought is the whole file and is not a prefix of it', () {
      final whole = mippedKtx2();
      final chain = Ktx2Chain.read(whole)!;
      expect(chain.prefix(whole, 0), isNull);
      expect(chain.prefix(whole, 9), isNull);
    });
  });
}

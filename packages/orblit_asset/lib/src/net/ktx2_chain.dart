import 'dart:typed_data';

/// Where one mip level's bytes are inside a KTX2 file.
class Ktx2Level {
  const Ktx2Level(this.offset, this.length, this.uncompressedLength);

  /// From the start of the file.
  final int offset;

  /// As stored, which for a supercompressed file is the compressed length.
  final int length;

  final int uncompressedLength;

  /// The byte after this level's last.
  int get end => offset + length;
}

/// A KTX2 file's mip chain, read from the front of the file.
///
/// The point of reading it is that **KTX2 stores levels smallest first**.
/// Level 0 — the full-size picture, and almost all of the bytes — sits at the
/// far end of the file, and the 1×1 level sits at the front. A prefix of the
/// file is therefore a complete set of small mip levels, which is exactly what
/// there is to draw while the rest of it arrives. A 1254×1254 texture cooked
/// here is 782 kB, and its first 216 kB hold every level down to 627×627.
///
/// Nothing here decodes a pixel. It reads the header and the level index —
/// the first few hundred bytes — and rewrites them, so the cost is the same
/// whatever the texture's size.
class Ktx2Chain {
  const Ktx2Chain._({
    required this.width,
    required this.height,
    required this.depth,
    required this.levels,
    required this.supercompression,
    required this.metadataEnd,
  });

  /// Level 0's size.
  final int width;
  final int height;

  /// Nought for a 2D texture, which is nearly all of them.
  final int depth;

  /// Largest first, the order the level index is written in.
  final List<Ktx2Level> levels;

  final int supercompression;

  /// The byte after the last of the header, the level index, the format
  /// description and the key-value data — where the level bytes begin.
  final int metadataEnd;

  static const List<int> _identifier = [
    0xAB,
    0x4B,
    0x54,
    0x58,
    0x20,
    0x32,
    0x30,
    0xBB,
    0x0D,
    0x0A,
    0x1A,
    0x0A,
  ];

  /// The header, the four offsets and two lengths after it, and then the
  /// level index.
  static const int _indexStart = 80;
  static const int _entryLength = 24;

  /// Reads the chain out of the front of a file, or answers null.
  ///
  /// Null is the ordinary answer, not a failure: it means there is no shorter
  /// version of this picture worth waiting for — the bytes are not a KTX2,
  /// not enough of them have arrived yet to say, there is only one level, or
  /// the file is laid out in a way this cannot safely shorten.
  static Ktx2Chain? read(Uint8List bytes) {
    if (bytes.length < _indexStart) return null;
    for (var i = 0; i < _identifier.length; i++) {
      if (bytes[i] != _identifier[i]) return null;
    }

    final view = ByteData.sublistView(bytes);
    int u32(int at) => view.getUint32(at, Endian.little);
    int u64(int at) => view.getUint64(at, Endian.little);

    final width = u32(20);
    final height = u32(24);
    final depth = u32(28);
    final count = u32(40);
    final supercompression = u32(44);

    // One level is the whole picture, and nought means the levels are built
    // on the device: either way there is nothing smaller in the file.
    if (count < 2) return null;

    // A file whose levels are stored raw must keep each one aligned to its
    // texel block, and shortening the index moves every level. Everything
    // this project cooks is supercompressed, where the required alignment is
    // one byte, so the shift is always safe; a raw file is left alone rather
    // than guessed at.
    if (supercompression == 0) return null;

    final indexEnd = _indexStart + count * _entryLength;
    if (bytes.length < indexEnd) return null;

    final levels = <Ktx2Level>[];
    for (var i = 0; i < count; i++) {
      final at = _indexStart + i * _entryLength;
      final level = Ktx2Level(u64(at), u64(at + 8), u64(at + 16));
      if (level.offset < indexEnd || level.length == 0) return null;
      levels.add(level);
    }

    // The format description, the key-value data and any supercompression
    // global data all sit between the index and the first level's bytes. They
    // have to travel with the prefix, so the prefix is only useful if they
    // come first — which the format requires, and which is checked rather
    // than assumed.
    var metadataEnd = indexEnd;
    for (final part in [
      [u32(48), u32(52)],
      [u32(56), u32(60)],
      [u64(64), u64(72)],
    ]) {
      final offset = part[0], length = part[1];
      if (length == 0) continue;
      if (offset < indexEnd) return null;
      final end = offset + length;
      if (end > metadataEnd) metadataEnd = end;
    }
    for (final level in levels) {
      if (level.offset < metadataEnd) return null;
    }

    return Ktx2Chain._(
      width: width,
      height: height,
      depth: depth,
      levels: levels,
      supercompression: supercompression,
      metadataEnd: metadataEnd,
    );
  }

  /// The coarsest level whose longest side is still at least [size], or null
  /// if there is no level worth stopping at.
  ///
  /// Counted from level 0, so 0 is the full-size picture and the answer is
  /// how many levels would be skipped. Never the last level — a 1×1 stand-in
  /// is not worth a stage — and never level 0, which is the whole download.
  int? levelAtLeast(int size) {
    for (var level = levels.length - 2; level >= 1; level--) {
      if (_shrink(width, level) >= size || _shrink(height, level) >= size) {
        return level;
      }
    }
    return null;
  }

  /// How many bytes of the file are needed to hold every level from [level]
  /// down to the smallest.
  ///
  /// The largest of them decides it. Taken as a maximum over the levels kept
  /// rather than from [level] alone, so a file written in some other order
  /// gives an honest answer — a useless one, being most of the file, which is
  /// then simply never worth fetching early.
  int bytesFor(int level) {
    var end = metadataEnd;
    for (var i = level; i < levels.length; i++) {
      if (levels[i].end > end) end = levels[i].end;
    }
    return end;
  }

  /// Builds a whole, valid KTX2 holding levels [level] and smaller, out of
  /// the first [bytesFor] bytes of the file.
  ///
  /// The prefix is not a KTX2 by itself: its header still claims levels that
  /// are not there. This writes the same bytes with a header and a level
  /// index that describe what actually arrived. Dropping the entries for the
  /// levels left behind moves everything after them, and every offset in the
  /// file moves by that same amount — which keeps the four-byte and
  /// eight-byte alignments the format requires, since an entry is
  /// twenty-four bytes.
  ///
  /// Returns null if [bytes] is shorter than the prefix needs to be.
  Uint8List? prefix(Uint8List bytes, int level) {
    if (level < 1 || level >= levels.length) return null;
    final end = bytesFor(level);
    if (bytes.length < end) return null;

    final shift = level * _entryLength;
    final kept = levels.length - level;
    final out = Uint8List(end - shift);
    final view = ByteData.sublistView(out);

    // The header and the six numbers after it, then patched.
    out.setRange(0, _indexStart, bytes);
    view.setUint32(20, _shrink(width, level), Endian.little);
    view.setUint32(24, _shrink(height, level), Endian.little);
    view.setUint32(28, _shrink(depth, level), Endian.little);
    view.setUint32(40, kept, Endian.little);

    final from = ByteData.sublistView(bytes);
    void move32(int at) {
      final was = from.getUint32(at, Endian.little);
      view.setUint32(at, was == 0 ? 0 : was - shift, Endian.little);
    }

    void move64(int at) {
      final was = from.getUint64(at, Endian.little);
      view.setUint64(at, was == 0 ? 0 : was - shift, Endian.little);
    }

    move32(48); // The format description's offset.
    move32(56); // The key-value data's.
    move64(64); // The supercompression global data's.

    for (var i = 0; i < kept; i++) {
      final was = levels[level + i];
      final at = _indexStart + i * _entryLength;
      view.setUint64(at, was.offset - shift, Endian.little);
      view.setUint64(at + 8, was.length, Endian.little);
      view.setUint64(at + 16, was.uncompressedLength, Endian.little);
    }

    // Everything from the old index's end to the prefix's end, verbatim: the
    // format description, the key-value data and the level bytes themselves,
    // none of which cares where in the file it sits.
    final oldIndexEnd = _indexStart + levels.length * _entryLength;
    out.setRange(
      _indexStart + kept * _entryLength,
      out.length,
      bytes,
      oldIndexEnd,
    );
    return out;
  }

  /// A dimension at [level], where nought stays nought: a height of nought
  /// means a 1D texture and a depth of nought means a 2D one, and neither
  /// becomes one-deep by being made smaller.
  static int _shrink(int size, int level) {
    if (size == 0) return 0;
    final smaller = size >> level;
    return smaller < 1 ? 1 : smaller;
  }
}

import 'dart:typed_data';

/// A KTX2 file with a mip chain, laid out the way the cooker lays one out.
///
/// Small enough to build in a test and shaped like the real thing: the
/// smallest level at the front of the data, the largest at the back, the
/// format description and key-value data ahead of both.
Uint8List mippedKtx2({
  int width = 256,
  int height = 256,
  int levels = 9,
  int supercompression = 2,
  int describe = 44,
  int keyValues = 20,
  bool metadataLast = false,
}) {
  int sizeOf(int level) {
    final w = (width >> level) < 1 ? 1 : width >> level;
    final h = (height >> level) < 1 ? 1 : height >> level;
    final bytes = w * h ~/ 8;
    return bytes < 8 ? 8 : bytes;
  }

  final indexEnd = 80 + levels * 24;
  var at = indexEnd;
  final dfdAt = metadataLast ? 0 : at;
  if (!metadataLast) at += describe;
  final kvdAt = metadataLast ? 0 : at;
  if (!metadataLast) at += keyValues;

  // Smallest first, which is what makes the front of the file worth fetching.
  final offsets = List<int>.filled(levels, 0);
  final lengths = List<int>.filled(levels, 0);
  for (var level = levels - 1; level >= 0; level--) {
    lengths[level] = sizeOf(level);
    offsets[level] = at;
    at += lengths[level];
  }
  final lateDfdAt = metadataLast ? at : 0;
  if (metadataLast) at += describe;

  final bytes = Uint8List(at);
  final view = ByteData.sublistView(bytes);
  bytes.setRange(0, 12, const [
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
  ]);
  view.setUint32(12, 0, Endian.little); // vkFormat: Basis Universal.
  view.setUint32(16, 1, Endian.little); // typeSize.
  view.setUint32(20, width, Endian.little);
  view.setUint32(24, height, Endian.little);
  view.setUint32(28, 0, Endian.little); // Not a 3D texture.
  view.setUint32(32, 0, Endian.little); // Not an array.
  view.setUint32(36, 1, Endian.little); // One face.
  view.setUint32(40, levels, Endian.little);
  view.setUint32(44, supercompression, Endian.little);

  view.setUint32(48, metadataLast ? lateDfdAt : dfdAt, Endian.little);
  view.setUint32(52, describe, Endian.little);
  view.setUint32(56, metadataLast ? 0 : kvdAt, Endian.little);
  view.setUint32(60, metadataLast ? 0 : keyValues, Endian.little);
  view.setUint64(64, 0, Endian.little); // No supercompression global data.
  view.setUint64(72, 0, Endian.little);

  for (var level = 0; level < levels; level++) {
    final entry = 80 + level * 24;
    view.setUint64(entry, offsets[level], Endian.little);
    view.setUint64(entry + 8, lengths[level], Endian.little);
    view.setUint64(entry + 16, lengths[level] * 4, Endian.little);
    // One byte value per level, so a prefix can be checked byte for byte.
    for (var i = 0; i < lengths[level]; i++) {
      bytes[offsets[level] + i] = (level + 1) & 0xFF;
    }
  }
  for (var i = 0; i < describe; i++) {
    bytes[(metadataLast ? lateDfdAt : dfdAt) + i] = 0xD0;
  }
  if (!metadataLast) {
    for (var i = 0; i < keyValues; i++) {
      bytes[kvdAt + i] = 0xCE;
    }
  }
  return bytes;
}

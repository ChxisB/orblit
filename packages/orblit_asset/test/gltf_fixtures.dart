import 'dart:convert';
import 'dart:typed_data';

/// A GLB holding `json` and `binary`, for tests that need a real container
/// rather than a mock of one.
Uint8List glb(Map<String, Object?> json, Uint8List binary) {
  final text = Uint8List.fromList(utf8.encode(jsonEncode(json)));
  final jsonLength = text.length + (4 - text.length % 4) % 4;
  final binLength = binary.length + (4 - binary.length % 4) % 4;
  final length = 12 + 8 + jsonLength + 8 + binLength;

  final out = Uint8List(length);
  final data = ByteData.sublistView(out);
  data.setUint32(0, 0x46546C67, Endian.little);
  data.setUint32(4, 2, Endian.little);
  data.setUint32(8, length, Endian.little);
  data.setUint32(12, jsonLength, Endian.little);
  data.setUint32(16, 0x4E4F534A, Endian.little);
  out.fillRange(20, 20 + jsonLength, 0x20);
  out.setRange(20, 20 + text.length, text);
  final at = 20 + jsonLength;
  data.setUint32(at, binLength, Endian.little);
  data.setUint32(at + 4, 0x004E4942, Endian.little);
  out.setRange(at + 8, at + 8 + binary.length, binary);
  return out;
}

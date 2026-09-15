import 'package:crypto/crypto.dart';

/// What some bytes are, as the SHA-256 of them.
///
/// The other half of an asset's identity. An [AssetId] says what a project
/// calls something and stays the same while the file changes; a hash says
/// exactly which bytes it is and changes when one of them does. That is what
/// makes it safe to key a cache or a download on: two things with the same
/// hash are the same thing, wherever they came from.
///
/// SHA-256 rather than something faster, because the hash is trusted rather
/// than merely compared. A cache that serves the wrong bytes for a hash is
/// broken in a way no test run will find, and a hash that can be forged is a
/// hash a server cannot stand behind.
class ContentHash {
  const ContentHash._(this.hex);

  /// The hash of [bytes].
  factory ContentHash.of(List<int> bytes) =>
      ContentHash._(sha256.convert(bytes).toString());

  /// The hash of everything [chunks] delivers, without holding it all at once.
  ///
  /// For a file too big to want in memory just to name it: the digest is
  /// built a chunk at a time, and the answer is the same as [ContentHash.of]
  /// on the whole thing however the chunks happen to be cut.
  static Future<ContentHash> ofStream(Stream<List<int>> chunks) async {
    final digest = await sha256.bind(chunks).single;
    return ContentHash._(digest.toString());
  }

  /// Reads a hash written as 64 hexadecimal digits, throwing a
  /// [FormatException] that says what is wrong when it is not one.
  ///
  /// Upper-case digits are accepted and lower-cased. Every tool on macOS and
  /// Linux prints lower-case, but PowerShell's `Get-FileHash` prints upper,
  /// and a hash pasted from one should not be a different hash from the same
  /// one pasted from the other — nor, on a case-sensitive disk, a different
  /// file in a store.
  factory ContentHash.parse(String text) {
    if (text.length != 64) {
      throw FormatException(
        '"$text" is not a content hash, because it is ${text.length} '
        'characters long. A SHA-256 hash is 64 hexadecimal digits.',
        text,
      );
    }
    for (var i = 0; i < text.length; i++) {
      if (!_isHexDigit(text.codeUnitAt(i))) {
        throw FormatException(
          '"$text" is not a content hash, because "${text[i]}" is not a '
          'hexadecimal digit.',
          text,
          i,
        );
      }
    }
    return ContentHash._(text.toLowerCase());
  }

  /// The hash as 64 lower-case hexadecimal digits.
  final String hex;

  /// The first two digits, which a store uses as a folder name.
  ///
  /// A folder of a hundred thousand files is slow to list, and slower still on
  /// some file systems to add to. Splitting on the first two digits makes 256
  /// folders of roughly equal size, because a hash spreads evenly — the same
  /// thing git does with its objects, for the same reason.
  String get shard => hex.substring(0, 2);

  @override
  bool operator ==(Object other) => other is ContentHash && other.hex == hex;

  @override
  int get hashCode => hex.hashCode;

  @override
  String toString() => hex;
}

bool _isHexDigit(int unit) =>
    (unit >= 0x30 && unit <= 0x39) || // 0-9
    (unit >= 0x41 && unit <= 0x46) || // A-F
    (unit >= 0x61 && unit <= 0x66); // a-f

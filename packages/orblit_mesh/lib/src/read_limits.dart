/// What [readObj], [readStl] and [readPly] take before they refuse.
///
/// A model file is bytes from anywhere, and a line or a header in it can ask
/// for as much memory as it has digits. The defaults are far above any model
/// somebody blocks out by hand and far below what would stall an editor.
final class ReadLimits {
  const ReadLimits({
    this.bytes = 128 << 20,
    this.vertices = 4000000,
    this.faces = 4000000,
    this.corners = 1024,
    this.indices = 16000000,
    this.names = 100000,
    this.line = 1 << 20,
  });

  /// The most bytes the file, or one `.mtl`, may hold.
  final int bytes;

  /// The most points, texture coordinates and other rows read.
  final int vertices;

  /// The most faces read.
  final int faces;

  /// The most corners one face may have. An OBJ drops a longer face, and the
  /// other readers refuse the file.
  ///
  /// shortcut: triangulating a face costs the square of its corners, so a
  /// file of the largest faces takes seconds. Read an untrusted file off the
  /// drawing isolate, or lower this, until a convex face is cut as a fan.
  final int corners;

  /// The most corners in all the faces together. [faces] and [corners] each
  /// allow a file that holds their product.
  final int indices;

  /// The most groups and materials an OBJ names.
  final int names;

  /// The most characters in a line of text, once a backslash has joined it to
  /// the next.
  final int line;
}

/// The lines of [text], each cut out as it is asked for, so a file that is
/// all line ends costs no more than the file. Throws [FormatException] for a
/// line over [ReadLimits.line].
Iterable<String> linesOf(String text, ReadLimits limits) sync* {
  var from = 0;
  while (from <= text.length) {
    var end = text.indexOf('\n', from);
    if (end < 0) end = text.length;
    if (end - from > limits.line) {
      throw FormatException(
        'A line over ${limits.line} characters is not read.',
      );
    }
    yield text.substring(from, end);
    from = end + 1;
  }
}

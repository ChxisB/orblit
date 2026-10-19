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
  });

  /// The most bytes the file, or one `.mtl`, may hold.
  final int bytes;

  /// The most points, texture coordinates and other rows read.
  final int vertices;

  /// The most faces read.
  final int faces;

  /// The most corners one face may have. An OBJ drops a longer face, and the
  /// other readers refuse the file.
  final int corners;
}

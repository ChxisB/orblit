/// Where a line of the cooked material text came from.
final class SurfaceOrigin {
  const SurfaceOrigin(this.file, this.line);

  /// The project path of the file the author wrote.
  final String file;

  /// One-based.
  final int line;

  @override
  String toString() => '$file:$line';
}

/// Something in a surface source that the translator refuses, said at the
/// line the author wrote it on.
final class SurfaceError implements Exception {
  const SurfaceError(this.origin, this.message);

  final SurfaceOrigin origin;
  final String message;

  @override
  String toString() => '$origin: $message';
}

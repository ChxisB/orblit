/// What a project calls one of its assets: `models/robot.glb`.
///
/// A logical name rather than a file path. The same id names the same asset
/// whether it is read from a directory on a development machine, from a
/// Flutter bundle on a phone or, later, from a server — which is the point of
/// it: a scene that stores `C:\Users\chris\robot.glb` opens on exactly one
/// machine.
///
/// There is only one way to spell an id. Anything that could be spelt two ways
/// — `models//robot.glb`, `./models/robot.glb`, `models\robot.glb` — is
/// refused rather than tidied up, because an id is a key: in a manifest, in a
/// cache, in somebody's scene file. Two spellings of one asset become two
/// entries that drift apart. And a doubled slash is far more often a folder
/// name that came out empty while a string was being built than something
/// anybody meant, so refusing it says so where it happened instead of three
/// layers later.
class AssetId {
  const AssetId._(this._path);

  /// Reads an id, throwing a [FormatException] that says what is wrong with
  /// it when it is not one.
  factory AssetId.parse(String text) {
    final problem = _problemWith(text);
    if (problem != null) throw FormatException(problem.$1, text, problem.$2);
    return AssetId._(text);
  }

  /// Reads an id, or gives null when it is not one.
  static AssetId? tryParse(String text) =>
      _problemWith(text) == null ? AssetId._(text) : null;

  final String _path;

  /// The last part of the path: `robot.glb`.
  String get name => _path.substring(_path.lastIndexOf('/') + 1);

  /// Everything before the name, without a trailing slash: `models`.
  ///
  /// Empty for an asset at the root of the project, rather than null, so that
  /// joining against it needs no special case.
  String get directory {
    final slash = _path.lastIndexOf('/');
    return slash < 0 ? '' : _path.substring(0, slash);
  }

  /// What kind of file the name says it is, lower-case and without the dot:
  /// `glb`.
  ///
  /// Lower-cased because `ROBOT.GLB` out of a Windows exporter is the same
  /// kind of file as `robot.glb`, and code choosing a loader should not have
  /// to know that. Only the last dot counts, so `scene.tar.gz` is `gz`; and a
  /// name that only starts with a dot, like `.gitignore`, has none — that dot
  /// makes a hidden file rather than naming a type.
  String get extension {
    final name = this.name;
    final dot = name.lastIndexOf('.');
    return dot <= 0 ? '' : name.substring(dot + 1).toLowerCase();
  }

  /// The asset a glTF file, or anything like one, means by [relative].
  ///
  /// glTF names its buffers and images with URIs relative to itself, so
  /// `robot.glb` in `models` asking for `textures/metal%20plate.png` means
  /// `models/textures/metal plate.png`. The URI is percent-decoded first, then
  /// joined against [directory], then `.` and `..` are collapsed.
  ///
  /// Null, rather than an exception, for anything that does not land on a
  /// valid id inside the project: a `..` that climbs past the root, an
  /// absolute path, a `data:` or `https:` URI, a malformed escape. A model
  /// file is somebody else's output, and one bad reference in it should cost
  /// that one texture rather than the whole model. Decoding comes before the
  /// check, not after, so `%2E%2E` cannot be used to climb out where `..`
  /// could not.
  AssetId? resolve(String relative) {
    final String decoded;
    try {
      decoded = Uri.decodeComponent(relative);
    } on ArgumentError {
      return null;
    } on FormatException {
      return null;
    }
    if (decoded.isEmpty || decoded.startsWith('/')) return null;

    final segments = [if (directory.isNotEmpty) ...directory.split('/')];
    for (final segment in decoded.split('/')) {
      if (segment == '.') continue;
      if (segment == '..') {
        if (segments.isEmpty) return null;
        segments.removeLast();
      } else {
        segments.add(segment);
      }
    }
    return tryParse(segments.join('/'));
  }

  @override
  bool operator ==(Object other) => other is AssetId && other._path == _path;

  @override
  int get hashCode => _path.hashCode;

  /// The id as it is written: `models/robot.glb`.
  @override
  String toString() => _path;

  /// What is wrong with [text] as an id, and where, or null when nothing is.
  static (String, int?)? _problemWith(String text) {
    if (text.isEmpty) return ('An empty string is not an asset id.', null);

    final backslash = text.indexOf(r'\');
    if (backslash >= 0) {
      return (
        '"$text" is not an asset id, because it has a backslash in it. Asset '
            'ids separate folders with forward slashes on every platform.',
        backslash,
      );
    }

    final colon = text.indexOf(':');
    if (colon >= 0) {
      return (
        '"$text" is not an asset id, because it has a ":" in it, which makes '
            'it a URL or a drive rather than a name inside the project.',
        colon,
      );
    }

    if (text.startsWith('/')) {
      return (
        '"$text" is not an asset id, because it starts with "/". An asset id '
            'is relative to the root of the project.',
        0,
      );
    }

    var offset = 0;
    for (final segment in text.split('/')) {
      if (segment.isEmpty) {
        return (
          '"$text" is not an asset id, because it has an empty folder name '
              'in it — a doubled slash, or one at the end.',
          offset,
        );
      }
      if (segment == '.') {
        return (
          '"$text" is not an asset id, because it has a "." folder in it, '
              'which is only another way of spelling the same name.',
          offset,
        );
      }
      if (segment == '..') {
        return (
          '"$text" is not an asset id, because it has a ".." in it. An id '
              'names a file directly, not a route to it.',
          offset,
        );
      }
      offset += segment.length + 1;
    }
    return null;
  }
}

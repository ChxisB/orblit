// The web half of the conditional export in native_importers.dart.
//
// The same names as native_importers_io.dart, so that code naming them
// compiles everywhere, but with nothing behind them: cooking means starting a
// program and writing files, and a browser does neither.
//
// A browser is not left without assets. It loads what was cooked for it, by a
// cook that ran on a build machine with `--target web`. That is the whole
// point of the target being part of the key.
import '../asset_id.dart';
import '../import_settings.dart';
import '../importer.dart';

/// No importer that drives a native program is available here.
///
/// An empty list rather than an error, because the sensible thing for shared
/// code to do is register what the platform has: on a build machine that is
/// these four, and in a browser it is none of them, and neither case is
/// exceptional.
List<Importer> get nativeImporters => const [];

/// A native program, which this platform cannot start.
class NativeTool {
  NativeTool(this.name, {String? path, this.environmentVariable}) {
    throw const CookingNotSupported('native tools');
  }

  final String name;
  final String? environmentVariable;

  static List<String> searchPath = [];

  String? locate() => throw const CookingNotSupported('native tools');

  Future<void> run(List<String> arguments, {required AssetId on}) =>
      throw const CookingNotSupported('native tools');
}

/// Cooking was asked for somewhere that cannot cook.
class CookingNotSupported implements Exception {
  const CookingNotSupported(this.importer);

  final String importer;

  @override
  String toString() =>
      'The $importer importer has to start a program and write files, and a '
      'browser can do neither. Cook on a build machine — '
      '`dart run orblit_asset:cook --target web` — and load the result.';
}

/// The shape every stub importer shares: it exists so that code naming it
/// compiles, and says so clearly the moment anyone builds one.
abstract class _Unavailable extends Importer {
  _Unavailable(String what) {
    throw CookingNotSupported(what);
  }

  @override
  int get version => 0;

  @override
  Map<String, Object?> resolveSettings(ImportSettings settings) =>
      throw CookingNotSupported(name);

  @override
  Future<ImportResult> import(ImportRequest request) =>
      throw CookingNotSupported(name);
}

/// PNG and JPEG to KTX2, which needs `orblit_texture_cook`.
class TextureImporter extends _Unavailable {
  TextureImporter({NativeTool? tool}) : super('texture');

  @override
  String get name => 'texture';

  @override
  Set<String> get extensions => const {'png', 'jpg', 'jpeg', 'ktx2'};
}

/// FBX and OBJ to GLB, which needs `orblit_import`.
class ModelImporter extends _Unavailable {
  ModelImporter({NativeTool? tool}) : super('model');

  @override
  String get name => 'model';

  @override
  Set<String> get extensions => const {'fbx', 'obj'};
}

/// Splat captures to `.osplat`, which needs `orblit_splat_cook`.
class SplatImporter extends _Unavailable {
  SplatImporter({NativeTool? tool}) : super('splats');

  @override
  String get name => 'splats';

  @override
  Set<String> get extensions => const {'ply', 'spz', 'splat', 'osplat'};
}

/// HDR and EXR to environment lighting, which needs Filament's `cmgen`.
class EnvironmentImporter extends _Unavailable {
  EnvironmentImporter({NativeTool? tool}) : super('environment');

  @override
  String get name => 'environment';

  @override
  Set<String> get extensions => const {'hdr', 'exr'};
}

/// A path to [name] inside the directory at [path].
///
/// Pure string work, so it is the same on both halves — but it lives here too
/// rather than in a shared file, because its io twin is written in terms of
/// `Platform.pathSeparator` and there is no platform here.
String beside(String path, String name) => '$path/$name';

/// Present so that code naming it compiles; there is no directory to be
/// inside of here.
String inside(Object directory, String name) =>
    throw const CookingNotSupported('native tools');

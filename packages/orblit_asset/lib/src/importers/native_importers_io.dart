import '../importer.dart';
import 'model_importer.dart';
import 'texture_importer.dart';

export 'model_importer.dart'
    show EnvironmentImporter, ModelImporter, SplatImporter;
export 'native_tool.dart' show NativeTool, beside, inside;
export 'texture_importer.dart' show TextureImporter;

/// Every importer that drives a native program, ready to register.
///
/// A list rather than a registry, so a caller can put its own importers ahead
/// of these — the registry tries them in order, and a project that wants its
/// own `.png` handling should get it.
List<Importer> get nativeImporters => [
  TextureImporter(),
  ModelImporter(),
  SplatImporter(),
  EnvironmentImporter(),
];

/// Cooking is supported here; this exists so that code written against the
/// stub still compiles.
class CookingNotSupported implements Exception {
  const CookingNotSupported(this.importer);

  final String importer;

  @override
  String toString() => 'The $importer importer is available here.';
}

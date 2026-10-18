import 'package:orblit_filament/orblit_filament.dart';

/// The width of each type word a `params.json` can hold. A picture has none.
const Map<String, int> _widthOfType = {
  'bool': 1,
  'int': 1,
  'float': 1,
  'vec2': 2,
  'vec3': 3,
  'vec4': 4,
  'sampler2D': 0,
};

/// An author-written surface after the cook: where its package is, and what it
/// declares.
///
/// The package carries no defaults, so the stage sends them. An object may
/// state only what is declared here, which keeps a value left in a scene for a
/// uniform the author has since removed from reaching the renderer.
final class CookedSurface {
  const CookedSurface({
    required this.package,
    this.widths = const {},
    this.defaults = const {},
    this.pictures = const {},
  });

  /// Reads the `params.json` the cook writes beside the package. Anything it
  /// cannot read is left out, so a file from a newer cook still loads.
  factory CookedSurface.fromParams(String package, Object? params) {
    final widths = <String, int>{};
    final defaults = <String, List<double>>{};
    final pictures = <String, bool>{};
    for (final entry in params is List ? params : const <Object?>[]) {
      if (entry is! Map || entry['name'] is! String) continue;
      final name = entry['name']! as String;
      final width = _widthOfType[entry['type']];
      if (width == null) continue;
      if (width == 0) {
        pictures[name] = switch (entry['hints']) {
          final Map<Object?, Object?> hints => hints.containsKey(
            'source_color',
          ),
          _ => false,
        };
        continue;
      }
      widths[name] = width;
      final given = _numbersOf(entry['default']);
      if (given != null && given.length == width) defaults[name] = given;
    }
    return CookedSurface(
      package: package,
      widths: widths,
      defaults: defaults,
      pictures: pictures,
    );
  }

  /// The compiled package, relative to the project.
  final String package;

  /// How many numbers each number, flag and vector takes, by name.
  final Map<String, int> widths;

  /// What the author wrote after each uniform, by name.
  final Map<String, List<double>> defaults;

  /// Every picture the surface reads, by name, and whether it holds a colour.
  final Map<String, bool> pictures;

  /// The surface one object draws: the defaults, then what [given] states.
  ///
  /// Null when the package cannot be placed. A value of the wrong width, or
  /// for a name nothing declares, is left out and the default stands.
  OrblitSurface? surfaceFor(
    Map<String, Object> given, {
    required String? Function(String path) locate,
  }) {
    final path = locate(package);
    if (path == null) return null;
    final values = {...defaults};
    final textures = <String, OrblitTexture>{};
    for (final MapEntry(:key, :value) in given.entries) {
      final colour = pictures[key];
      if (colour != null) {
        final found = value is String ? locate(value) : null;
        if (found != null) textures[key] = OrblitTexture(found, srgb: colour);
        continue;
      }
      final numbers = _numbersOf(value);
      if (numbers != null && numbers.length == widths[key]) {
        values[key] = numbers;
      }
    }
    return OrblitSurface(package: path, values: values, textures: textures);
  }
}

/// A number, a flag or a list of numbers as the doubles a renderer is sent.
List<double>? _numbersOf(Object? raw) => switch (raw) {
  final num n => [n.toDouble()],
  final bool b => [b ? 1.0 : 0.0],
  final List<Object?> list when list.every((n) => n is num) => [
    for (final n in list) (n as num).toDouble(),
  ],
  _ => null,
};

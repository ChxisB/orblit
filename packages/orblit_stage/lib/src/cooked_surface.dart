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

/// One uniform an author declared, as an inspector needs to show it.
final class SurfaceParam {
  const SurfaceParam({
    required this.name,
    required this.type,
    this.instance = false,
    this.hints = const {},
  });

  final String name;

  /// `bool`, `int`, `float`, `vec2`, `vec3`, `vec4` or `sampler2D`.
  final String type;

  /// Whether each object may state its own. The renderer takes a value for
  /// any declared name, so this only says which ones an editor should offer.
  final bool instance;

  /// Each hint the author wrote, by name, with its arguments as written.
  final Map<String, List<String>> hints;

  /// A colour the author marked `source_color`.
  bool get isColour => hints.containsKey('source_color');

  /// The slider bounds from `hint_range(min, max, step)`, or null. The step is
  /// zero when the author left it out.
  ({double min, double max, double step})? get range {
    final args = hints['hint_range'];
    if (args == null || args.length < 2) return null;
    final min = double.tryParse(args[0]);
    final max = double.tryParse(args[1]);
    if (min == null || max == null || min >= max) return null;
    return (
      min: min,
      max: max,
      step: args.length > 2 ? double.tryParse(args[2]) ?? 0 : 0,
    );
  }
}

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
    this.params = const [],
  });

  /// Reads the `params.json` the cook writes beside the package. Anything it
  /// cannot read is left out, so a file from a newer cook still loads.
  factory CookedSurface.fromParams(String package, Object? params) {
    final widths = <String, int>{};
    final defaults = <String, List<double>>{};
    final pictures = <String, bool>{};
    final declared = <SurfaceParam>[];
    for (final entry in params is List ? params : const <Object?>[]) {
      if (entry is! Map || entry['name'] is! String) continue;
      final name = entry['name']! as String;
      final width = _widthOfType[entry['type']];
      if (width == null) continue;
      declared.add(
        SurfaceParam(
          name: name,
          type: entry['type']! as String,
          instance: entry['instance'] == true,
          hints: _hintsOf(entry['hints']),
        ),
      );
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
      params: declared,
    );
  }

  /// Every uniform the author declared, in the order they wrote them.
  final List<SurfaceParam> params;

  /// The compiled package, relative to the project.
  final String package;

  /// How many numbers each number, flag and vector takes, by name.
  final Map<String, int> widths;

  /// What the author wrote after each uniform, by name.
  final Map<String, List<double>> defaults;

  /// Every picture the surface reads, by name, and whether it holds a colour.
  final Map<String, bool> pictures;

  /// The numbers the surface draws [name] with for an object that states
  /// [given]: its own when they fit the width, else the default, else zeros.
  List<double> valueOf(String name, Map<String, Object> given) {
    final numbers = _numbersOf(given[name]);
    if (numbers != null && numbers.length == widths[name]) return numbers;
    return defaults[name] ?? List.filled(widths[name] ?? 1, 0.0);
  }

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

/// The hints of one declaration: each name with its arguments as text.
Map<String, List<String>> _hintsOf(Object? raw) => {
  if (raw is Map)
    for (final MapEntry(:key, :value) in raw.entries)
      if (key is String)
        key: [
          if (value is List)
            for (final arg in value) '$arg',
        ],
};

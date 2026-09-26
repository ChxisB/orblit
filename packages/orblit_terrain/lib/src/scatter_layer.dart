import 'cover.dart';

/// One kind of thing scattered over the ground: a grass, a stone, a tree.
///
/// Rules, not places. Where each one stands is worked out from these and the
/// ground by a [ScatterPlacer], so the rules are a line of the terrain's
/// settings file however many thousand things they put down, and an edit to
/// the ground moves what stands on it without anybody placing it again.
///
/// A layer with no [mesh] is a block: a box [size] across, standing on its
/// bottom face, and drawn by the renderer's built-in cube, which is what lets
/// a hundred thousand blades of grass be one draw. A layer that names a
/// [mesh] stands on the model's origin.
class ScatterLayer {
  const ScatterLayer({
    required this.name,
    this.seed = 0,
    this.density = 1,
    this.sets = const [],
    this.minSlope = 0,
    this.maxSlope = 90,
    this.minHeight = double.negativeInfinity,
    this.maxHeight = double.infinity,
    this.size = (1, 1, 1),
    this.minScale = 1,
    this.maxScale = 1,
    this.lean = 0,
    this.turn = true,
    this.lift = 0,
    this.colour = 0xFFFFFF,
    this.colourVariation = 0,
    this.groundTint = 0,
    this.range = 0,
    this.castShadows = false,
    this.mesh,
    this.material,
  }) : assert(density > 0 && density <= maxDensity);

  /// The most a layer can put down per square metre. Eight centimetres
  /// apart: past this a layer is a carpet, not a scatter, and a region of it
  /// would be millions.
  static const double maxDensity = 100;

  /// What the layer is called where people choose it: "grass", "boulders".
  final String name;

  /// Which pattern it is laid in. Layers with the same seed and the same
  /// [density] land in the same places and take the same turns and sizes,
  /// which is how a trunk and its crown are two layers; give every other
  /// layer a seed of its own, or two of them will stand in each other.
  final int seed;

  /// How many are put down per square metre where the ground is all of the
  /// layer's [sets] and inside its slope and height. Fewer where the sets
  /// share the ground with others, none where they are absent.
  final double density;

  /// The terrain sets it grows on, by index. Empty grows on any ground.
  ///
  /// Where a texel blends two sets, the layer has the share its sets hold:
  /// grass on a texel that is a quarter scree grows three-quarters as thick.
  /// Automatic ground counts as the flat and steep sets it chooses between.
  final List<int> sets;

  /// The gentlest and the steepest ground it stands on, in degrees from
  /// level. Trees stop on cliffs; scree starts on them.
  final double minSlope;
  final double maxSlope;

  /// The lowest and the highest ground it stands on, in metres. Unbounded
  /// unless set.
  final double minHeight;
  final double maxHeight;

  /// At full size: a block's width, height and depth in metres, or how much a
  /// model is scaled along its own x, y and z.
  final (double, double, double) size;

  /// The least and the most of [size] each one is, chosen at random between.
  final double minScale;
  final double maxScale;

  /// How far each leans with the ground under it, 0 to 1. At 0 it stands
  /// straight up whatever the slope, as a tree does; at 1 it lies square to
  /// the ground, as a stone does.
  final double lean;

  /// Whether each is turned a random way about its own up, so a thousand of
  /// one model do not all face north.
  final bool turn;

  /// How far above the ground each one's base is, in metres at full size.
  /// Negative sinks it in, so a stone on a slope does not show its underside
  /// and an upright trunk does not stand on one edge.
  final double lift;

  /// The colour, `0xRRGGBB`, sRGB. A block is drawn in it; a model is drawn
  /// in its [material] and ignores it.
  final int colour;

  /// How much brighter or darker each one is at random, 0 to 1: at 0.2, any
  /// shade from 80% to 120% of [colour].
  final double colourVariation;

  /// How much the ground's colour map tints each one, 0 to 1, so grass
  /// painted yellow grows yellow.
  final double groundTint;

  /// How far off they are still drawn, in metres. Zero draws every one. Grass
  /// is gone a stone's throw away; a forest is seen from the next valley.
  final double range;

  /// Whether they cast shadows. Off by default: a shadow per blade of grass
  /// costs a great deal to show very little.
  final bool castShadows;

  /// The model each one is, a path within the project, or null for a block.
  final String? mesh;

  /// What a [mesh] is made of, an `.omat` path within the project. Without
  /// one the model wears its file's own materials, and each copy is drawn on
  /// its own rather than all of them in one go.
  final String? material;

  /// Whether the set at [index] is one this layer grows on.
  bool growsOn(int index) => sets.isEmpty || sets.contains(index);

  @override
  bool operator ==(Object other) =>
      other is ScatterLayer &&
      other.name == name &&
      other.seed == seed &&
      other.density == density &&
      _sameSets(other.sets, sets) &&
      other.minSlope == minSlope &&
      other.maxSlope == maxSlope &&
      other.minHeight == minHeight &&
      other.maxHeight == maxHeight &&
      other.size == size &&
      other.minScale == minScale &&
      other.maxScale == maxScale &&
      other.lean == lean &&
      other.turn == turn &&
      other.lift == lift &&
      other.colour == colour &&
      other.colourVariation == colourVariation &&
      other.groundTint == groundTint &&
      other.range == range &&
      other.castShadows == castShadows &&
      other.mesh == mesh &&
      other.material == material;

  @override
  int get hashCode => Object.hashAll([
    name,
    seed,
    density,
    Object.hashAll(sets),
    minSlope,
    maxSlope,
    minHeight,
    maxHeight,
    size,
    minScale,
    maxScale,
    lean,
    turn,
    lift,
    colour,
    colourVariation,
    groundTint,
    range,
    castShadows,
    mesh,
    material,
  ]);

  static bool _sameSets(List<int> a, List<int> b) {
    if (a.length != b.length) return false;
    for (var i = 0; i < a.length; i++) {
      if (a[i] != b[i]) return false;
    }
    return true;
  }

  /// The layer as one line of the settings file: what is at its default is
  /// left out, so the line says what is particular about this layer.
  Map<String, Object?> toJson() => {
    'name': name,
    if (seed != 0) 'seed': seed,
    'density': density,
    if (sets.isNotEmpty) 'sets': sets,
    if (minSlope != 0) 'minSlope': minSlope,
    if (maxSlope != 90) 'maxSlope': maxSlope,
    if (minHeight.isFinite) 'minHeight': minHeight,
    if (maxHeight.isFinite) 'maxHeight': maxHeight,
    'size': [size.$1, size.$2, size.$3],
    if (minScale != 1) 'minScale': minScale,
    if (maxScale != 1) 'maxScale': maxScale,
    if (lean != 0) 'lean': lean,
    if (!turn) 'turn': false,
    if (lift != 0) 'lift': lift,
    'colour': '#${(colour & 0xFFFFFF).toRadixString(16).padLeft(6, '0')}',
    if (colourVariation != 0) 'colourVariation': colourVariation,
    if (groundTint != 0) 'groundTint': groundTint,
    if (range != 0) 'range': range,
    if (castShadows) 'castShadows': true,
    'mesh': ?mesh,
    'material': ?material,
  };

  /// A layer out of a line of the settings file, or null with a note in
  /// [problems] when it has no name. Anything out of range is brought into
  /// it, with a note saying so.
  static ScatterLayer? fromJson(Object? raw, List<String> problems) {
    if (raw is! Map<String, Object?> || raw['name'] is! String) {
      problems.add('A scatter layer with no name was left out.');
      return null;
    }
    final name = raw['name']! as String;
    const fallback = ScatterLayer(name: '');

    double within(String key, double low, double high, double otherwise) {
      final value = _number(raw, key);
      if (value == null) return otherwise;
      if (value < low || value > high) {
        final kept = value.clamp(low, high);
        problems.add('The "$name" layer\'s $key was $value; it is $kept now.');
        return kept;
      }
      return value;
    }

    var density = _number(raw, 'density') ?? fallback.density;
    if (!(density > 0) || density > maxDensity) {
      final kept = density > maxDensity ? maxDensity : fallback.density;
      problems.add(
        'The "$name" layer put down $density per square metre; it puts down '
        '$kept now.',
      );
      density = kept;
    }

    final sets = <int>[];
    final rawSets = raw['sets'];
    for (final set in rawSets is List ? rawSets : const <Object?>[]) {
      if (set is int && set >= 0 && set < Cover.setCount) {
        if (!sets.contains(set)) sets.add(set);
      } else {
        problems.add('The "$name" layer named a set that is not one: $set.');
      }
    }

    var minSlope = within('minSlope', 0, 90, fallback.minSlope);
    var maxSlope = within('maxSlope', 0, 90, fallback.maxSlope);
    if (minSlope > maxSlope) (minSlope, maxSlope) = (maxSlope, minSlope);
    var minHeight = _number(raw, 'minHeight') ?? fallback.minHeight;
    var maxHeight = _number(raw, 'maxHeight') ?? fallback.maxHeight;
    if (minHeight > maxHeight) (minHeight, maxHeight) = (maxHeight, minHeight);

    var size = fallback.size;
    final rawSize = raw['size'];
    if (rawSize is List &&
        rawSize.length == 3 &&
        rawSize.every((part) => part is num && part.isFinite && part > 0)) {
      size = (
        (rawSize[0] as num).toDouble(),
        (rawSize[1] as num).toDouble(),
        (rawSize[2] as num).toDouble(),
      );
    } else if (rawSize != null) {
      problems.add(
        'The "$name" layer\'s size was not three lengths; it is '
        '1 m each way now.',
      );
    }

    var minScale = within('minScale', 0.001, 1000, fallback.minScale);
    var maxScale = within('maxScale', 0.001, 1000, fallback.maxScale);
    if (minScale > maxScale) (minScale, maxScale) = (maxScale, minScale);

    return ScatterLayer(
      name: name,
      seed: _integer(raw, 'seed') ?? fallback.seed,
      density: density,
      sets: List.unmodifiable(sets),
      minSlope: minSlope,
      maxSlope: maxSlope,
      minHeight: minHeight,
      maxHeight: maxHeight,
      size: size,
      minScale: minScale,
      maxScale: maxScale,
      lean: within('lean', 0, 1, fallback.lean),
      turn: raw['turn'] != false,
      lift: _number(raw, 'lift') ?? fallback.lift,
      colour: _colour(raw['colour']) ?? fallback.colour,
      colourVariation: within(
        'colourVariation',
        0,
        1,
        fallback.colourVariation,
      ),
      groundTint: within('groundTint', 0, 1, fallback.groundTint),
      range: within('range', 0, double.infinity, fallback.range),
      castShadows: raw['castShadows'] == true,
      mesh: _text(raw, 'mesh'),
      material: _text(raw, 'material'),
    );
  }

  static int? _colour(Object? raw) {
    if (raw is! String || !raw.startsWith('#') || raw.length != 7) return null;
    return int.tryParse(raw.substring(1), radix: 16);
  }
}

double? _number(Map<String, Object?> json, String key) {
  final value = json[key];
  return value is num && value.isFinite ? value.toDouble() : null;
}

int? _integer(Map<String, Object?> json, String key) {
  final value = json[key];
  if (value is int) return value;
  if (value is double && value.isFinite && value == value.roundToDouble()) {
    return value.toInt();
  }
  return null;
}

String? _text(Map<String, Object?> json, String key) {
  final value = json[key];
  return value is String && value.isNotEmpty ? value : null;
}

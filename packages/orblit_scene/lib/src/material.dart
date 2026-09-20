import 'values.dart';

/// What a material file is called.
const String materialExtension = '.omat';

/// The sort of value a material parameter holds, and therefore how it is read
/// out of a file and how two of them merge.
///
/// Merging is by whole value, never by part: a parent that sets `baseColour`
/// to a warm white and a child that sets it to red gives red, not some blend
/// of the two. Anything else and a material's colour would depend on how many
/// ancestors it happened to have.
enum MaterialKind {
  /// One number.
  number,

  /// True or false.
  flag,

  /// Four numbers: red, green, blue and alpha, each nought to one, linear.
  rgba,

  /// Three numbers, linear. Colours that have no alpha because the thing they
  /// describe cannot be partly present — a glow, a sheen.
  rgb,

  /// Two numbers. A scale, an offset, anything that comes in u and v.
  pair,

  /// One of a fixed set of names. Spelt out in the file rather than numbered,
  /// because a material is something people read and merge by hand.
  choice,
}

/// One parameter a material may set.
///
/// The table below is the only place these names exist. A renderer that grows
/// a parameter adds a row here and one line where the resolved material is
/// turned into the renderer's own; a file naming anything not in the table is
/// kept out of the resolved values and reported, so a typo is visible rather
/// than silently doing nothing.
final class MaterialField {
  const MaterialField(this.name, this.kind, {this.choices = const []});

  /// As it is spelt in the file.
  final String name;

  final MaterialKind kind;

  /// For [MaterialKind.choice], the names allowed. Empty otherwise.
  final List<String> choices;

  @override
  String toString() => '$name (${kind.name})';
}

/// Every parameter a material file may set, and every map it may name.
///
/// Two tables rather than one, because a texture is an asset reference and the
/// cook has to follow it. Keeping maps in their own object means anything that
/// needs a material's dependencies — the importer, the bundler — reads one
/// key and is done, without having to know what any parameter means.
abstract final class MaterialFields {
  /// The parameters, in the order an inspector would sensibly show them.
  static const List<MaterialField> all = [
    MaterialField(
      'shading',
      MaterialKind.choice,
      choices: ['lit', 'unlit', 'video', 'shadowCatcher'],
    ),
    MaterialField(
      'blend',
      MaterialKind.choice,
      choices: ['opaque', 'transparent', 'fade', 'masked', 'add'],
    ),
    MaterialField(
      'culling',
      MaterialKind.choice,
      choices: ['back', 'front', 'none'],
    ),
    MaterialField('doubleSided', MaterialKind.flag),

    MaterialField('baseColour', MaterialKind.rgba),
    MaterialField('metallic', MaterialKind.number),
    MaterialField('roughness', MaterialKind.number),
    MaterialField('reflectance', MaterialKind.number),
    MaterialField('clearCoat', MaterialKind.number),
    MaterialField('clearCoatRoughness', MaterialKind.number),
    MaterialField('anisotropy', MaterialKind.number),
    MaterialField('sheenColour', MaterialKind.rgb),
    MaterialField('sheenRoughness', MaterialKind.number),

    MaterialField('emissive', MaterialKind.rgb),
    MaterialField('emissiveIntensity', MaterialKind.number),
    MaterialField('ambientOcclusion', MaterialKind.number),
    MaterialField('normalScale', MaterialKind.number),

    MaterialField('tiling', MaterialKind.pair),
    MaterialField('offset', MaterialKind.pair),
    MaterialField(
      'wrap',
      MaterialKind.choice,
      choices: ['repeat', 'clamp', 'mirror'],
    ),
    MaterialField('filter', MaterialKind.choice, choices: ['smooth', 'sharp']),

    MaterialField('maskThreshold', MaterialKind.number),
    MaterialField('depthWrite', MaterialKind.flag),
    MaterialField('depthBias', MaterialKind.number),
    MaterialField('screenMapped', MaterialKind.flag),

    MaterialField(
      'blendMode',
      MaterialKind.choice,
      choices: ['none', 'linear', 'masked', 'maskedDepth'],
    ),
    MaterialField('blendAmount', MaterialKind.number),
    MaterialField('blendSharpness', MaterialKind.number),
    MaterialField('blendTiling', MaterialKind.pair),
    MaterialField('blendOffset', MaterialKind.pair),

    MaterialField('windBearing', MaterialKind.number),
    MaterialField('windSpeed', MaterialKind.number),
    MaterialField('windStrength', MaterialKind.number),
  ];

  /// The maps a material may name, by the slot each fills.
  static const List<String> maps = [
    'baseColour',
    'normal',
    'metallicRoughness',
    'occlusion',
    'emissive',
    'blendBaseColour',
    'blendMask',
  ];

  static final Map<String, MaterialField> byName = {
    for (final field in all) field.name: field,
  };

  /// Whether [name] is a parameter this build understands.
  static bool has(String name) => byName.containsKey(name);
}

/// A material as it sits in a file: what it sets, and what it inherits from.
///
/// Deliberately not a resolved material. A document says only what *this* file
/// states, so that saving it back writes the same file it read — a child that
/// inherited roughness from its parent must not acquire a roughness of its own
/// the first time somebody opens and saves it. Resolving is [MaterialLibrary]'s
/// job and happens once, in Dart, before a scene is ever sent to a renderer.
class MaterialDocument {
  const MaterialDocument({
    this.parent,
    this.group,
    this.values = const {},
    this.maps = const {},
  });

  /// Reads one, keeping what it can and saying what it could not.
  ///
  /// A material with one unreadable parameter is still worth having: the
  /// alternative is a scene that loses a wall because somebody typed
  /// `roughness: "half"`.
  static MaterialLoad fromJson(Map<String, Object?> json) {
    final problems = <String>[];
    final values = <String, Object?>{};
    final maps = <String, String>{};

    for (final entry in Values.object(json['values']).entries) {
      final field = MaterialFields.byName[entry.key];
      if (field == null) {
        problems.add('no parameter called ${entry.key}');
        continue;
      }
      final value = _read(field, entry.value);
      if (value == null) {
        problems.add('${entry.key} is not ${_shape(field)}');
        continue;
      }
      values[entry.key] = value;
    }

    for (final entry in Values.object(json['maps']).entries) {
      if (!MaterialFields.maps.contains(entry.key)) {
        problems.add('no map slot called ${entry.key}');
        continue;
      }
      final path = entry.value;
      if (path is! String || path.isEmpty) {
        problems.add('the ${entry.key} map is not a path');
        continue;
      }
      maps[entry.key] = path;
    }

    return MaterialLoad(
      document: MaterialDocument(
        parent: Values.text(json, 'parent'),
        group: Values.text(json, 'group'),
        values: values,
        maps: maps,
      ),
      problems: problems,
    );
  }

  /// The material this one starts from, as a project path, or null for none.
  final String? parent;

  /// The group whose overrides are laid over this material, or null.
  final String? group;

  /// The parameters this file states, already read as the kind each is.
  final Map<String, Object?> values;

  /// The maps this file names, by slot, as project paths.
  final Map<String, String> maps;

  Map<String, Object?> toJson() => Values.pruned({
    'parent': parent,
    'group': group,
    'values': values.isEmpty ? null : values,
    'maps': maps.isEmpty ? null : maps,
  });

  /// One value read as the kind its field says, or null when the file has
  /// something else there.
  static Object? _read(MaterialField field, Object? raw) =>
      switch (field.kind) {
        MaterialKind.number => raw is num ? raw.toDouble() : null,
        MaterialKind.flag => raw is bool ? raw : null,
        MaterialKind.rgba => _numbers(raw, 4),
        MaterialKind.rgb => _numbers(raw, 3),
        MaterialKind.pair => _numbers(raw, 2),
        MaterialKind.choice =>
          raw is String && field.choices.contains(raw) ? raw : null,
      };

  static List<double>? _numbers(Object? raw, int count) {
    if (raw is! List || raw.length != count) return null;
    final read = <double>[];
    for (final value in raw) {
      if (value is! num) return null;
      read.add(value.toDouble());
    }
    return read;
  }

  static String _shape(MaterialField field) => switch (field.kind) {
    MaterialKind.number => 'a number',
    MaterialKind.flag => 'true or false',
    MaterialKind.rgba => 'four numbers',
    MaterialKind.rgb => 'three numbers',
    MaterialKind.pair => 'two numbers',
    MaterialKind.choice => 'one of ${field.choices.join(', ')}',
  };
}

/// A material read back off disk, with anything that could not be read.
class MaterialLoad {
  const MaterialLoad({required this.document, this.problems = const []});

  final MaterialDocument document;
  final List<String> problems;
}

/// A material with nothing left to look up: every parameter its ancestors and
/// its group between them decided, and every map resolved to a path.
///
/// This is what a renderer is handed. There is no parent here and no group,
/// because both have already been spent.
class ResolvedMaterial {
  const ResolvedMaterial({
    this.values = const {},
    this.maps = const {},
    this.problems = const [],
  });

  final Map<String, Object?> values;
  final Map<String, String> maps;

  /// Anything that went wrong on the way — a parent that does not exist, a
  /// chain that loops. Reported rather than thrown, because a material that
  /// lost its parent still draws, in the colours it states itself.
  final List<String> problems;

  /// A parameter, or null when nothing in the chain set it. The caller's own
  /// default stands in — which is the renderer's, so a material that says
  /// nothing looks the same whether it was resolved through ten ancestors or
  /// none.
  Object? operator [](String name) => values[name];

  double? number(String name) {
    final value = values[name];
    return value is double ? value : null;
  }

  bool? flag(String name) {
    final value = values[name];
    return value is bool ? value : null;
  }

  List<double>? numbers(String name) {
    final value = values[name];
    return value is List<double> ? value : null;
  }

  String? choice(String name) {
    final value = values[name];
    return value is String ? value : null;
  }
}

/// Every material a project has, and the one place a parent chain is walked.
///
/// Resolution happens here and is cached, so a scene with four hundred objects
/// wearing eleven materials walks eleven chains rather than four hundred. The
/// cost is paid once at load and never again per frame, which is the whole
/// reason inheritance is allowed to exist at all: a renderer that had to ask
/// "and what did its parent say" while drawing would be doing it sixty times
/// a second for something that cannot change between frames.
class MaterialLibrary {
  MaterialLibrary({
    Map<String, MaterialDocument> materials = const {},
    Map<String, MaterialDocument> groups = const {},
  }) : _materials = Map.of(materials),
       _groups = Map.of(groups);

  final Map<String, MaterialDocument> _materials;
  final Map<String, MaterialDocument> _groups;
  final Map<String, ResolvedMaterial> _resolved = {};

  /// The materials, by project path.
  Map<String, MaterialDocument> get materials => Map.unmodifiable(_materials);

  /// The groups, by name.
  Map<String, MaterialDocument> get groups => Map.unmodifiable(_groups);

  /// Adds or replaces one material, forgetting whatever was resolved from it.
  ///
  /// The whole cache goes, not just this material's entry: anything that named
  /// it as a parent resolved through it, and finding those would cost more
  /// than resolving the handful of materials a project has again.
  void put(String path, MaterialDocument material) {
    _materials[path] = material;
    _resolved.clear();
  }

  /// Adds or replaces one group, on the same terms.
  void putGroup(String name, MaterialDocument group) {
    _groups[name] = group;
    _resolved.clear();
  }

  /// What [path] finally says, with its ancestors and its group spent.
  ///
  /// Order is: the eldest ancestor first, then each descendant over the top,
  /// then the group. The group wins, and deliberately — a group exists to
  /// override a whole set of materials from outside, and one that lost to
  /// every material that had bothered to state a value could override almost
  /// nothing.
  ResolvedMaterial resolve(String path) {
    final cached = _resolved[path];
    if (cached != null) return cached;

    final problems = <String>[];
    final chain = <MaterialDocument>[];
    final walked = <String>{};

    var at = path;
    while (true) {
      if (!walked.add(at)) {
        problems.add(
          '$at inherits from itself, through ${walked.join(' -> ')}',
        );
        break;
      }
      final material = _materials[at];
      if (material == null) {
        problems.add('no material at $at');
        break;
      }
      chain.add(material);
      final parent = material.parent;
      if (parent == null) break;
      at = parent;
    }

    final values = <String, Object?>{};
    final maps = <String, String>{};
    // Eldest first: the chain was collected downwards, so it is walked back.
    for (final material in chain.reversed) {
      values.addAll(material.values);
      maps.addAll(material.maps);
    }

    // The group named nearest the bottom of the chain, because that is the one
    // this material chose. An ancestor's group still applies to the ancestor's
    // own descendants, but a child that names its own is not overruled by it.
    final named = chain
        .map((material) => material.group)
        .firstWhere((group) => group != null, orElse: () => null);
    if (named != null) {
      final group = _groups[named];
      if (group == null) {
        problems.add('no group called $named');
      } else {
        values.addAll(group.values);
        maps.addAll(group.maps);
      }
    }

    return _resolved[path] = ResolvedMaterial(
      values: values,
      maps: maps,
      problems: problems,
    );
  }
}

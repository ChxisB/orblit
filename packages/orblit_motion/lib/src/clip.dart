import 'dart:convert';

import 'package:orblit_scene/orblit_scene.dart' show Values;
import 'package:orblit_sequence/orblit_sequence.dart';
import 'package:vector_math/vector_math_64.dart';

import 'frame.dart';
import 'kind.dart';
import 'root.dart';

/// The extension a clip file carries.
const String clipExtension = '.oclip';

/// A file that is not a clip, or is one from a newer Orblit.
class ClipFormatException implements Exception {
  const ClipFormatException(this.message);

  final String message;

  @override
  String toString() => 'ClipFormatException: $message';
}

/// A clip read back off disk, with anything that could not be read.
class ClipLoad {
  const ClipLoad({required this.clip, this.problems = const []});

  final ClipDocument clip;

  final List<String> problems;
}

/// One thing a clip moves, and the keys it moves it through.
///
/// What it moves is named three ways at once:
///
/// - [target] is an entity, by its path from whoever plays the clip. Empty
///   is that entity itself. The path is a scene's id with the player's own
///   part taken off, so the same clip plays on every lamp of a street, and
///   a reparent inside the lamp does not orphan it.
/// - [bone] is one of the target's bones, for a model with a skeleton, or
///   null for the entity itself.
/// - [property] is what about it: `transform.position`, a component and one
///   of its fields, on an entity; `position`, `rotation` or `scale` on a
///   bone.
///
/// A bone is not an entity because a skeleton is the model's, not the
/// scene's: a character from a file has sixty joints and the outliner should
/// not.
class ClipChannel<T extends Object> {
  ClipChannel({
    required this.target,
    this.bone,
    required this.property,
    required this.kind,
    required List<Key<T>> keys,
  }) : keys = List<Key<T>>.unmodifiable(_inOrder(keys)) {
    if (keys.isEmpty) {
      throw ArgumentError.value(keys, 'keys', 'A channel needs a key.');
    }
  }

  /// Bone properties, and the kinds they carry.
  static const Map<String, ChannelKind<Object>> boneProperties = {
    'position': ChannelKind.vector,
    'rotation': ChannelKind.rotation,
    'scale': ChannelKind.vector,
  };

  final String target;
  final String? bone;
  final String property;
  final ChannelKind<T> kind;

  /// In order of time. Two keys at one moment are a cut, and the later one
  /// in the list is what the channel says from then on.
  final List<Key<T>> keys;

  late final Channel<T> _curve = Channel<T>(keys, kind.mixer);

  /// The curve through [keys], for anything that wants the slopes too.
  Channel<T> get curve => _curve;

  double get start => keys.first.at;
  double get end => keys.last.at;

  /// The value at [at] seconds into the clip.
  T valueAt(double at) => _curve.at(at);

  /// The component an entity property belongs to, `transform` of
  /// `transform.position`, or null for a bone's.
  String? get component {
    if (bone != null) return null;
    final dot = property.indexOf('.');
    return dot <= 0 ? null : property.substring(0, dot);
  }

  /// The field within [component], or the whole property on a bone.
  String get field {
    if (bone != null) return property;
    return property.substring(property.indexOf('.') + 1);
  }

  /// Whether [other] moves the same thing. A clip has one channel for each.
  bool sameAddress(ClipChannel<Object> other) =>
      target == other.target &&
      bone == other.bone &&
      property == other.property;

  ClipChannel<T> withKeys(List<Key<T>> keys) => ClipChannel<T>(
    target: target,
    bone: bone,
    property: property,
    kind: kind,
    keys: keys,
  );

  /// [keys] as this channel's, whatever they are typed as on the way in.
  ///
  /// For a caller holding the channel as a `ClipChannel<Object>`, which is
  /// how a list of channels of every kind is held: [withKeys] wants keys of
  /// what the channel really carries, and that caller has no name for it.
  /// Throws if a value is not one.
  ClipChannel<T> rekeyed(Iterable<Key<Object>> keys) =>
      withKeys(_typed<T>(keys));

  /// A channel of [kind], with keys typed as anything.
  ///
  /// The other half of [rekeyed]: made from a kind chosen at run time, the
  /// way a channel keyed from an editor's field is.
  static ClipChannel<Object> ofKind({
    required String target,
    String? bone,
    required String property,
    required ChannelKind<Object> kind,
    required Iterable<Key<Object>> keys,
  }) {
    ClipChannel<V> make<V extends Object>(ChannelKind<V> kind) =>
        ClipChannel<V>(
          target: target,
          bone: bone,
          property: property,
          kind: kind,
          keys: _typed<V>(keys),
        );
    return switch (kind) {
      final ChannelKind<double> kind => make<double>(kind),
      final ChannelKind<Vector3> kind => make<Vector3>(kind),
      final ChannelKind<Quaternion> kind => make<Quaternion>(kind),
      final ChannelKind<bool> kind => make<bool>(kind),
    };
  }

  static List<Key<V>> _typed<V extends Object>(Iterable<Key<Object>> keys) => [
    for (final key in keys)
      Key<V>(
        key.at,
        key.value as V,
        hold: key.hold,
        shape: key.shape,
        slopeIn: key.slopeIn as V?,
        slopeOut: key.slopeOut as V?,
      ),
  ];

  Map<String, Object?> toJson() {
    // The hold most keys use is said once, on the channel, and only the
    // keys that differ say their own: an imported clip is a thousand linear
    // keys, and a thousand `"hold": "linear"`s are noise in every diff.
    final counts = <Hold, int>{};
    for (final key in keys) {
      counts[key.hold] = (counts[key.hold] ?? 0) + 1;
    }
    final usual = counts.entries
        .reduce((a, b) => b.value > a.value ? b : a)
        .key;

    return Values.pruned({
      'target': target,
      'bone': bone,
      'property': property,
      'kind': kind.name,
      'hold': usual == Hold.smooth ? null : usual.name,
      'keys': [
        for (final key in keys)
          Values.pruned({
            'at': key.at,
            'value': kind.write(key.value),
            'hold': key.hold == usual ? null : key.hold.name,
            'shape': key.shape?.name,
            'in': switch (key.slopeIn) {
              final T slope => kind.write(slope),
              null => null,
            },
            'out': switch (key.slopeOut) {
              final T slope => kind.write(slope),
              null => null,
            },
          }),
      ],
    });
  }

  /// A channel out of a file, or null with a note when it cannot be one.
  static ClipChannel<Object>? fromJson(Object? raw, List<String> problems) {
    if (raw is! Map<String, Object?>) return null;
    final target = Values.text(raw, 'target') ?? '';
    final bone = Values.text(raw, 'bone');
    final property = Values.text(raw, 'property');
    final kind = ChannelKind.named(raw['kind']);
    final where = [if (target.isNotEmpty) target, ?bone, ?property].join(' › ');

    if (property == null || property.isEmpty) {
      problems.add('A channel on "$where" says nothing about what it moves.');
      return null;
    }
    if (kind == null) {
      problems.add(
        'The channel "$where" has a kind this Orblit does not know.',
      );
      return null;
    }
    if (bone != null && boneProperties[property] != kind) {
      problems.add(
        'The channel "$where" is not something a bone has; bones move by '
        'position, rotation and scale.',
      );
      return null;
    }
    if (bone == null && property.indexOf('.') <= 0) {
      problems.add(
        'The channel "$where" names no component; a property on an entity '
        'is written as component.field.',
      );
      return null;
    }

    final address = (target: target, bone: bone, property: property);
    return switch (kind) {
      final ChannelKind<double> kind => _read<double>(
        raw,
        address,
        kind,
        problems,
      ),
      final ChannelKind<Vector3> kind => _read<Vector3>(
        raw,
        address,
        kind,
        problems,
      ),
      final ChannelKind<Quaternion> kind => _read<Quaternion>(
        raw,
        address,
        kind,
        problems,
      ),
      final ChannelKind<bool> kind => _read<bool>(raw, address, kind, problems),
    };
  }

  static ClipChannel<T>? _read<T extends Object>(
    Map<String, Object?> raw,
    ({String target, String? bone, String property}) address,
    ChannelKind<T> kind,
    List<String> problems,
  ) {
    final where = [
      if (address.target.isNotEmpty) address.target,
      ?address.bone,
      address.property,
    ].join(' › ');
    final usual = Values.named(Hold.values, raw['hold']) ?? Hold.smooth;
    final keys = <Key<T>>[];
    var dropped = 0;
    final rawKeys = raw['keys'];
    for (final entry in rawKeys is List ? rawKeys : const []) {
      if (entry is! Map<String, Object?>) {
        dropped++;
        continue;
      }
      final at = Values.maybeNumber(entry, 'at');
      final value = kind.read(entry['value']);
      if (at == null || !at.isFinite || value == null) {
        dropped++;
        continue;
      }
      keys.add(
        Key<T>(
          at,
          value,
          hold: Values.named(Hold.values, entry['hold']) ?? usual,
          shape: Values.named(Easing.values, entry['shape']),
          slopeIn: kind.readSlope(entry['in']),
          slopeOut: kind.readSlope(entry['out']),
        ),
      );
    }
    if (dropped > 0) {
      problems.add(
        '$dropped key${dropped == 1 ? '' : 's'} on "$where" could not be '
        'read and ${dropped == 1 ? 'was' : 'were'} left out.',
      );
    }
    if (keys.isEmpty) {
      problems.add('The channel "$where" has no keys, so it was left out.');
      return null;
    }
    return ClipChannel<T>(
      target: address.target,
      bone: address.bone,
      property: address.property,
      kind: kind,
      keys: keys,
    );
  }

  /// [keys] in order of time, keeping the order of any at the same moment.
  ///
  /// Sorted here, once, because a channel is sampled constantly and assumes
  /// its keys are in order, and a file edited by hand is not always.
  static List<Key<T>> _inOrder<T>(List<Key<T>> keys) {
    for (var i = 1; i < keys.length; i++) {
      if (keys[i].at < keys[i - 1].at) {
        final numbered = [for (var j = 0; j < keys.length; j++) (j, keys[j])]
          ..sort((a, b) {
            final by = a.$2.at.compareTo(b.$2.at);
            return by != 0 ? by : a.$1.compareTo(b.$1);
          });
        return [for (final (_, key) in numbered) key];
      }
    }
    return keys;
  }
}

/// Which channel moves the character, rather than the model.
///
/// A walk authored in place looks like walking on a treadmill; one authored
/// moving walks the model off its own origin while the character it belongs
/// to stays behind. Root motion is the second kind played as the first: the
/// named thing is held where it started, and how far it would have gone
/// comes out of the player as a step for whoever moves the character to take.
///
/// What is taken is chosen the way a character wants it, not all or nothing:
///
/// - **Across the ground** always: that is the walk.
/// - **Up and down** only when [rises]. A walk's bob stays in the pose,
///   because a body that bobs a capsule up and down is a body that skips; a
///   climb or a jump takes it, because there the body really goes up.
/// - **Turning** only when [turns], and then only about [up]. A turn on the
///   spot hands the character the heading; a lean or a sway stays in the
///   pose.
class RootMotion {
  RootMotion({
    this.target = '',
    this.bone,
    this.turns = false,
    this.rises = false,
    Vector3? up,
  }) : up = up == null || up.length2 < 1e-12
           ? Vector3(0, 1, 0)
           : up.normalized();

  static RootMotion? fromJson(Object? raw) {
    if (raw is! Map<String, Object?>) return null;
    final up = raw['up'];
    return RootMotion(
      target: Values.text(raw, 'target') ?? '',
      bone: Values.text(raw, 'bone'),
      turns: Values.flag(raw, 'turns', fallback: false),
      rises: Values.flag(raw, 'rises', fallback: false),
      up: up is List ? Values.vector(up) : null,
    );
  }

  /// The entity, as a channel's [ClipChannel.target] names it.
  final String target;

  /// Its bone, for a skeleton's root, or null for the entity.
  final String? bone;

  /// Whether turning goes to the character.
  final bool turns;

  /// Whether rising and falling goes to the character.
  final bool rises;

  /// Which way is up, in the space the root's own channels are in.
  ///
  /// A model's root bone hangs from the model, which is Y-up when the file
  /// is glTF. One exported Z-up and stood upright by a node above the
  /// skeleton has its root's channels in the space before that turn, and
  /// says so here.
  final Vector3 up;

  /// Whether [channel] is this root's.
  bool owns(ClipChannel<Object> channel) =>
      channel.target == target && channel.bone == bone;

  Map<String, Object?> toJson() => Values.pruned({
    'target': target,
    'bone': bone,
    'turns': turns ? true : null,
    'rises': rises ? true : null,
    'up': up.y == 1 ? null : [up.x, up.y, up.z],
  });
}

/// An animation: what moves, through which keys, over how long.
///
/// A clip is an asset in the way a sequence is: it names what it moves by
/// path, never by object, so it can be written before the thing it animates
/// exists and played on every copy of it afterwards. Sampling it is
/// stateless, so a scrub backwards shows exactly what playing forwards to the
/// same moment would.
class ClipDocument {
  ClipDocument({
    required this.name,
    required this.duration,
    this.whenDone = WhenDone.hold,
    this.rate = 30,
    List<ClipChannel<Object>> channels = const [],
    List<Mark> marks = const [],
    this.rootMotion,
  }) : channels = List<ClipChannel<Object>>.unmodifiable(channels),
       marks = List<Mark>.unmodifiable(
         <Mark>[...marks]..sort((a, b) => a.at.compareTo(b.at)),
       );

  static const String marker = 'orblit.clip';

  /// The shape of the file. Bumped with a migration whenever it changes.
  static const int formatVersion = 1;

  /// Every step from an older clip file to this one, oldest first. None yet:
  /// this is the first format. A step is decoded JSON in and out, like a
  /// scene's.
  static const List<ClipMigration> migrations = [];

  final String name;

  /// How long it is, in seconds. Not always the last key: a clip can hold
  /// still at the end, and a loop's last key is often short of the length so
  /// that the first key is where it lands.
  final double duration;

  /// What playing does at the end, until whoever plays it says otherwise.
  final WhenDone whenDone;

  /// Frames a second it was authored at, which is what a timeline snaps to.
  /// A clip's own, because a clip made at 24 snapped to 30 lands between its
  /// own keys.
  final double rate;

  final List<ClipChannel<Object>> channels;

  /// Moments something happens, a footstep or a swing landing. In order.
  final List<Mark> marks;

  /// Which channel moves the character, or null when none does.
  final RootMotion? rootMotion;

  /// The channel moving [property] of [target], or of its [bone].
  ClipChannel<Object>? channelFor(
    String target,
    String property, {
    String? bone,
  }) {
    for (final channel in channels) {
      if (channel.target == target &&
          channel.bone == bone &&
          channel.property == property) {
        return channel;
      }
    }
    return null;
  }

  late final RootTrack? _root = RootTrack.of(this);

  /// What the clip says at [at] seconds in.
  ///
  /// As authored, unless [inPlace]: then the part of the root that goes to
  /// the character is taken out, and the model walks on the spot while
  /// [rootStep] says how far. A player asks for it in place; a timeline
  /// scrubbing a walk wants to see it walk.
  ClipFrame sampleAt(double at, {bool inPlace = false}) {
    final frame = ClipFrame.sample(this, at);
    if (inPlace) _root?.hold(frame);
    return frame;
  }

  /// How far [rootMotion] carries the character from [from] seconds in to
  /// [to]. Nowhere, when the clip has no root motion.
  RootStep rootStep(double from, double to) =>
      _root?.step(from, to) ?? RootStep();

  /// Every mark after [from] and up to and including [to].
  ///
  /// Half-open that way round so a mark on a frame boundary fires exactly
  /// once, in the step that reaches it.
  Iterable<Mark> marksBetween(double from, double to) =>
      marks.where((mark) => mark.at > from && mark.at <= to);

  ClipDocument copyWith({
    String? name,
    double? duration,
    WhenDone? whenDone,
    double? rate,
    List<ClipChannel<Object>>? channels,
    List<Mark>? marks,
    RootMotion? rootMotion,
    bool clearRootMotion = false,
  }) => ClipDocument(
    name: name ?? this.name,
    duration: duration ?? this.duration,
    whenDone: whenDone ?? this.whenDone,
    rate: rate ?? this.rate,
    channels: channels ?? this.channels,
    marks: marks ?? this.marks,
    rootMotion: clearRootMotion ? null : rootMotion ?? this.rootMotion,
  );

  Map<String, Object?> toJson() => Values.pruned({
    'kind': marker,
    'formatVersion': formatVersion,
    'name': name,
    'duration': duration,
    'rate': rate,
    'whenDone': whenDone.name,
    'rootMotion': rootMotion?.toJson(),
    'channels': [for (final channel in channels) channel.toJson()],
    'marks': [
      for (final mark in marks)
        Values.pruned({
          'at': mark.at,
          'name': mark.name,
          'payload': mark.payload,
        }),
    ],
  });

  /// The file's text: indented like a scene, but with one key to a line.
  ///
  /// A scene's writer would put every number of every key on a line of its
  /// own, and a two-second walk would be ten thousand lines that nobody
  /// could review. A key to a line makes the diff of two takes a diff of
  /// the keys that changed.
  String encode() => '${_write(toJson(), '')}\n';

  /// A clip out of a file's text, with whatever could not be read.
  ///
  /// Lenient about the parts, strict about the whole: a key that cannot be
  /// read is dropped with a note, and a file that is not a clip, or is one
  /// from a newer Orblit, throws [ClipFormatException].
  static ClipLoad decode(String text) {
    final Object? parsed;
    try {
      parsed = jsonDecode(text);
    } on FormatException catch (error) {
      throw ClipFormatException('This is not a clip file: ${error.message}');
    }
    if (parsed is! Map<String, Object?> || parsed['kind'] != marker) {
      throw const ClipFormatException('This is not a clip file.');
    }
    final problems = <String>[];
    final version = parsed['formatVersion'];
    if (version is int && version > formatVersion) {
      throw const ClipFormatException(
        'This clip was written by a newer Orblit.',
      );
    }
    var json = parsed;
    for (final step in migrations) {
      if (version is int && step.from >= version) {
        json = step.apply(json, problems);
      }
    }

    final channels = <ClipChannel<Object>>[];
    final rawChannels = json['channels'];
    for (final raw in rawChannels is List ? rawChannels : const []) {
      final channel = ClipChannel.fromJson(raw, problems);
      if (channel == null) continue;
      if (channels.any(channel.sameAddress)) {
        problems.add(
          'Two channels move ${channel.property} on the same thing; the '
          'second was left out.',
        );
        continue;
      }
      channels.add(channel);
    }

    final marks = <Mark>[];
    final rawMarks = json['marks'];
    for (final raw in rawMarks is List ? rawMarks : const []) {
      if (raw is! Map<String, Object?>) continue;
      final at = Values.maybeNumber(raw, 'at');
      final name = Values.text(raw, 'name');
      if (at == null || name == null) {
        problems.add('A mark with no time or no name was left out.');
        continue;
      }
      marks.add(Mark(at, name, payload: raw['payload']));
    }

    var last = 0.0;
    for (final channel in channels) {
      if (channel.end > last) last = channel.end;
    }
    for (final mark in marks) {
      if (mark.at > last) last = mark.at;
    }
    final stated = Values.maybeNumber(json, 'duration');
    final rate = Values.maybeNumber(json, 'rate');

    return ClipLoad(
      clip: ClipDocument(
        name: Values.text(json, 'name') ?? 'Clip',
        // A length that is missing, or shorter than nothing, is the last
        // thing the clip does. One shorter than its keys is kept: a loop
        // cut before its last key is a thing people do on purpose.
        duration: stated != null && stated >= 0 ? stated : last,
        whenDone:
            Values.named(WhenDone.values, json['whenDone']) ?? WhenDone.hold,
        rate: rate != null && rate > 0 ? rate : 30,
        channels: channels,
        marks: marks,
        rootMotion: RootMotion.fromJson(json['rootMotion']),
      ),
      problems: problems,
    );
  }
}

/// One step between clip formats: decoded JSON at [from] in, at [to] out.
abstract class ClipMigration {
  const ClipMigration();

  int get from;
  int get to => from + 1;

  Map<String, Object?> apply(Map<String, Object?> json, List<String> notes);
}

/// Decoded JSON as text, one key or mark to a line.
String _write(Object? value, String indent, {bool flat = false}) {
  if (value is Map<String, Object?>) {
    if (value.isEmpty) return '{}';
    final inner = '$indent  ';
    final lines = [
      for (final entry in value.entries)
        '$inner${jsonEncode(entry.key)}: '
            '${_write(entry.value, inner, flat: _flatLists.contains(entry.key))}',
    ];
    return '{\n${lines.join(',\n')}\n$indent}';
  }
  if (value is List) {
    if (value.isEmpty) return '[]';
    if (value.every((one) => one is! Map && one is! List)) {
      return jsonEncode(value);
    }
    final inner = '$indent  ';
    final lines = [
      for (final one in value)
        '$inner${flat ? jsonEncode(one) : _write(one, inner)}',
    ];
    return '[\n${lines.join(',\n')}\n$indent]';
  }
  return jsonEncode(value);
}

/// The lists written an item to a line.
const Set<String> _flatLists = {'keys', 'marks'};

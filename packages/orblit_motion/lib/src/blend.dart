import 'dart:convert';
import 'dart:math' as math;

import 'package:orblit_scene/orblit_scene.dart' show Values;
import 'package:orblit_sequence/orblit_sequence.dart';

import 'clip.dart';
import 'condition.dart';
import 'frame.dart';
import 'pass.dart';
import 'place.dart';
import 'root.dart';
import 'source.dart';

/// The extension a blend file carries.
const String blendExtension = '.oblend';

/// A file that is not a blend, or is one from a newer Orblit.
class BlendFormatException implements Exception {
  const BlendFormatException(this.message);

  final String message;

  @override
  String toString() => 'BlendFormatException: $message';
}

/// A blend read back off disk, with anything that could not be read.
class BlendLoad {
  const BlendLoad({required this.blend, this.problems = const []});

  final BlendDocument blend;

  final List<String> problems;
}

/// One state of a blend: what plays while the blend is in it.
class BlendState {
  BlendState(this.name, {required this.plays, this.speed = 1, this.whenDone}) {
    if (name.isEmpty) {
      throw ArgumentError.value(name, 'name', 'A state needs a name.');
    }
    if (!speed.isFinite || speed < 0) {
      throw ArgumentError.value(
        speed,
        'speed',
        'A state plays forwards, or not at all.',
      );
    }
  }

  /// What changes name it by, one to a blend.
  final String name;

  final BlendSource plays;

  /// How fast it plays. Forwards only: a walk backwards is a clip of a walk
  /// backwards, because a walk played in reverse puts its weight on the
  /// wrong foot.
  final double speed;

  /// What its clips do at their ends, or null for each clip's own.
  final WhenDone? whenDone;

  Map<String, Object?> toJson() => Values.pruned({
    'name': name,
    ...plays.toJson(),
    'speed': speed == 1 ? null : speed,
    'whenDone': whenDone?.name,
  });
}

/// A way from one state to another: when it is taken, and how long the
/// fade between the two takes.
///
/// A change with no [from] is from anywhere, which is how an interruption
/// is written: a jump from whatever the character was doing. It is never
/// taken into the state already being played, so it does not restart it
/// every frame its condition holds; a change from a state to itself, named,
/// is how a state restarts on purpose.
class BlendChange {
  const BlendChange({
    this.from,
    required this.to,
    this.when = BlendCondition.always,
    this.fade = 0,
    this.shape = Easing.smooth,
    this.inStep = false,
  });

  /// The state it leaves, or null for any.
  final String? from;

  /// The state it enters.
  final String to;

  final BlendCondition when;

  /// Seconds the fade takes. Nought is a cut.
  final double fade;

  /// How the fade eases. Smoothly unless told otherwise, because a fade is
  /// there not to be noticed.
  final Easing shape;

  /// Whether the state entered starts as far through its lap as the one
  /// left is through its own, so a walk turning into a run keeps its feet.
  final bool inStep;

  Map<String, Object?> toJson() {
    final when = this.when.toJson();
    return Values.pruned({
      'from': from,
      'to': to,
      'when': when.isEmpty ? null : when,
      'fade': fade == 0 ? null : fade,
      'shape': shape == Easing.smooth ? null : shape.name,
      'inStep': inStep ? true : null,
    });
  }
}

/// How much say one clip has at a place, and how far through it is.
class ClipWeight {
  const ClipWeight({
    required this.state,
    required this.clip,
    required this.weight,
    required this.lap,
  });

  /// The state playing it.
  final String state;

  final String clip;

  /// Its share of the whole. Every share at a place adds to one.
  final double weight;

  /// How far through its state is.
  final double lap;

  @override
  String toString() => 'ClipWeight($state: $clip at $weight, lap $lap)';
}

/// One step of a blend: where it now is, and what happened getting there.
class BlendStep {
  BlendStep({
    required this.place,
    required this.frame,
    this.marks = const [],
    RootStep? moved,
    this.change,
  }) : moved = moved ?? RootStep();

  final BlendPlace place;

  /// The clips where the blend now is, mixed, with root motion held in
  /// place.
  final ClipFrame frame;

  /// Every mark the state being played passed, in the order it passed them.
  final List<Mark> marks;

  /// How far root motion carried the character, mixed as the clips are.
  final RootStep moved;

  /// The change taken at the end of the step, or null.
  final BlendChange? change;
}

/// Which clips play, and how much each has to say: a graph of states, each
/// playing a clip or a mix of clips, and changes between them, each with
/// the condition that takes it and how long its fade is.
///
/// A blend is an asset, like a clip, and like a clip it never changes while
/// it plays. Where it is playing is a [BlendPlace], a value on its own, and
/// what drives it is inputs, named numbers that gameplay sets: a speed, a
/// direction, a flag for being on the ground. Everything here is a question
/// asked of a place and the inputs, so any answer can be had without
/// playing up to it: which change comes next, which clips have what say,
/// and, given the clips, the pose.
///
/// A clip is named rather than held, and the names are whatever the caller
/// finds the clips by: project-relative paths, the way a scene's motion
/// component lists them, or anything else, so long as it is the same names
/// handed back with the clips.
class BlendDocument {
  BlendDocument({
    required this.name,
    Map<String, double> inputs = const {},
    required List<BlendState> states,
    List<BlendChange> changes = const [],
    String? start,
  }) : inputs = Map<String, double>.unmodifiable(inputs),
       states = List<BlendState>.unmodifiable(states),
       changes = List<BlendChange>.unmodifiable(changes),
       start = start ?? (states.isEmpty ? '' : states.first.name) {
    if (states.isEmpty) {
      throw ArgumentError.value(states, 'states', 'A blend needs a state.');
    }
    if (_byName.length != states.length) {
      throw ArgumentError.value(states, 'states', 'Two states have one name.');
    }
    if (stateNamed(this.start) == null) {
      throw ArgumentError.value(start, 'start', 'Not one of the states.');
    }
    for (final change in changes) {
      final from = change.from;
      if (stateNamed(change.to) == null ||
          (from != null && stateNamed(from) == null)) {
        throw ArgumentError.value(
          '${from ?? 'anywhere'} to ${change.to}',
          'changes',
          'A change is between two of the states.',
        );
      }
    }
  }

  static const String marker = 'orblit.blend';

  /// The shape of the file. Bumped with a migration whenever it changes.
  static const int formatVersion = 1;

  /// Every step from an older blend file to this one, oldest first. None
  /// yet: this is the first format.
  static const List<BlendMigration> migrations = [];

  final String name;

  /// The inputs it reads, and what each is until something sets it. An
  /// input read but not here is nought until set.
  final Map<String, double> inputs;

  /// In the order they were written, which is what [BlendPlace.toNumbers]
  /// counts by.
  final List<BlendState> states;

  /// In order of precedence: when two could be taken, the first is.
  final List<BlendChange> changes;

  /// The state a character starts in.
  final String start;

  late final Map<String, int> _byName = {
    for (var i = 0; i < states.length; i++) states[i].name: i,
  };

  BlendState? stateNamed(String name) {
    final index = _byName[name];
    return index == null ? null : states[index];
  }

  /// Where [state] is in [states], or -1.
  int indexOf(String state) => _byName[state] ?? -1;

  /// Every clip it names, once each, for whoever loads them.
  Iterable<String> get clipNames => {
    for (final state in states) ...state.plays.clips,
  };

  /// Every input it reads, declared or not.
  Iterable<String> get inputsRead => {
    for (final state in states) ...state.plays.inputs,
    for (final change in changes) ...change.when.inputs,
  };

  /// [values] read the way the blend reads them: an input not given, or not
  /// a number, is as [inputs] has it, or nought.
  double Function(String input) _reader(Map<String, double> values) => (input) {
    final value = values[input];
    if (value != null && value.isFinite) return value;
    return inputs[input] ?? 0;
  };

  /// The change that would be taken at [place] with [values] for inputs, or
  /// null when none would.
  BlendChange? changeFor(BlendPlace place, Map<String, double> values) {
    final read = _reader(values);
    for (final change in changes) {
      final from = change.from;
      if (from == null ? change.to == place.state : from != place.state) {
        continue;
      }
      if (change.when.holds(read, place.lap)) return change;
    }
    return null;
  }

  /// Every clip with a say at [place] with [values] for inputs, and its
  /// share, newest state first. Needs no clips: it is the blend's answer,
  /// whatever clips are handed to it.
  List<ClipWeight> weightsAt(BlendPlace place, Map<String, double> values) {
    final read = _reader(values);
    return [
      for (final (layer, share) in place.shares)
        if (share > 0)
          if (stateNamed(layer.state) case final state?)
            for (final MapEntry(key: clip, value: weight)
                in state.plays.weigh(read).entries)
              ClipWeight(
                state: layer.state,
                clip: clip,
                weight: share * weight,
                lap: layer.lap,
              ),
    ];
  }

  /// Seconds one time through [state] takes at its own speed of one, with
  /// [values] for inputs: the lengths of its clips, mixed as the clips are.
  /// Nought for a state with no clip that is there.
  double lengthOf(
    String state,
    Map<String, double> values, {
    required Map<String, ClipDocument> clips,
  }) {
    final found = stateNamed(state);
    return found == null ? 0 : _length(found, _reader(values), clips);
  }

  double _length(
    BlendState state,
    double Function(String input) read,
    Map<String, ClipDocument> clips,
  ) {
    var total = 0.0;
    var sum = 0.0;
    for (final MapEntry(key: name, value: weight)
        in state.plays.weigh(read).entries) {
      final clip = clips[name];
      if (clip == null) continue;
      sum += clip.duration * weight;
      total += weight;
    }
    return total > 0 ? sum / total : 0;
  }

  /// The pose at [place] with [values] for inputs: every clip with a say,
  /// sampled where its state is and mixed. Root motion is held in place, as
  /// a player holds it. A clip not in [clips] has no say, and the others
  /// share what it would have had.
  ClipFrame sampleAt(
    BlendPlace place,
    Map<String, double> values, {
    required Map<String, ClipDocument> clips,
  }) {
    final read = _reader(values);
    final frames = <ClipFrame>[];
    final weights = <double>[];
    var at = 0.0;
    var loudest = 0.0;
    for (final (layer, share) in place.shares) {
      if (!(share > 0)) continue;
      final state = stateNamed(layer.state);
      if (state == null) continue;
      for (final MapEntry(key: name, value: weight)
          in state.plays.weigh(read).entries) {
        final clip = clips[name];
        if (clip == null) continue;
        final time = timeAt(clip, state.whenDone ?? clip.whenDone, layer.lap);
        frames.add(clip.sampleAt(time, inPlace: true));
        weights.add(share * weight);
        if (share * weight > loudest) {
          loudest = share * weight;
          at = time;
        }
      }
    }
    return ClipFrame.mix(frames, weights, at: at);
  }

  /// Plays on from [place] by [seconds], with [values] for inputs.
  ///
  /// Every state being played moves on, the ones fading out as well, and
  /// every fade with them; a fade that finishes takes whatever it was fading
  /// from away. Then, if a change can be taken from where the state being
  /// played now is, the first that can be is, and the step ends at the
  /// start of it. One change a step, so a blend whose changes chase each
  /// other round cannot hang a frame.
  ///
  /// Marks come from the state being played, and from the clip in it with
  /// the most say, so a walk mixed with a run takes one footstep, not two.
  /// Root motion comes from every clip with a say, mixed by it. A step of
  /// nothing plays nothing but still takes a change, which is how a new
  /// input is acted on at once.
  BlendStep advance(
    BlendPlace place,
    Map<String, double> values,
    double seconds, {
    required Map<String, ClipDocument> clips,
  }) {
    final read = _reader(values);
    final step = seconds.isFinite && seconds > 0 ? seconds : 0.0;

    final moved = <({BlendPlace was, BlendState? state, double lap})>[];
    final faded = <double>[];
    for (BlendPlace? at = place; at != null; at = at.from) {
      final state = stateNamed(at.state);
      var lap = at.lap;
      if (state != null && step > 0 && state.speed > 0) {
        final length = _length(state, read, clips);
        // A state with no length is through as soon as it starts.
        lap = length > 0
            ? lap + step * state.speed / length
            : math.max(lap, 1.0);
      }
      moved.add((was: at, state: state, lap: lap));
      final fading = at.from != null && at.fade > 0;
      faded.add(fading ? math.min(at.faded + step, at.fade) : 0);
      // A fade that has finished has the whole say, and whatever it was
      // fading in over is finished with.
      if (!fading || faded.last >= at.fade) break;
    }

    BlendPlace? built;
    for (var i = moved.length - 1; i >= 0; i--) {
      final (:was, state: _, :lap) = moved[i];
      built = built == null
          ? BlendPlace(was.state, lap: lap)
          : BlendPlace(
              was.state,
              lap: lap,
              from: built,
              faded: faded[i],
              fade: was.fade,
              shape: was.shape,
            );
    }
    final now = built!;

    final marks = <Mark>[];
    final steps = <RootStep>[];
    final says = <double>[];
    final shares = now.shares;
    for (var i = 0; i < shares.length; i++) {
      final (:was, :state, :lap) = moved[i];
      if (state == null) continue;
      final share = shares[i].$2;
      ClipDocument? loudest;
      var most = 0.0;
      for (final MapEntry(key: name, value: weight)
          in state.plays.weigh(read).entries) {
        final clip = clips[name];
        if (clip == null) continue;
        if (share * weight > 0) {
          final pass = passOver(
            clip,
            state.whenDone ?? clip.whenDone,
            was.lap,
            lap,
            marks: false,
          );
          steps.add(pass.moved);
          says.add(share * weight);
        }
        if (weight > most) {
          most = weight;
          loudest = clip;
        }
      }
      if (i == 0 && loudest != null) {
        marks.addAll(
          passOver(
            loudest,
            state.whenDone ?? loudest.whenDone,
            was.lap,
            lap,
          ).marks,
        );
      }
    }

    final change = changeFor(now, values);
    final next = change == null
        ? now
        : now.enter(
            change.to,
            fade: change.fade,
            shape: change.shape,
            inStep: change.inStep,
          );
    return BlendStep(
      place: next,
      frame: sampleAt(next, values, clips: clips),
      marks: marks,
      moved: _mixed(steps, says),
      change: change,
    );
  }

  /// [steps] mixed by [says], the way the clips that took them are.
  static RootStep _mixed(List<RootStep> steps, List<double> says) {
    if (steps.isEmpty) return RootStep();
    if (steps.length == 1) return steps.first;
    return RootStep(
      position: vector3Mixer.mix([for (final one in steps) one.position], says),
      rotation: quaternionMixer.mix([
        for (final one in steps) one.rotation,
      ], says),
    );
  }

  Map<String, Object?> toJson() => {
    'kind': marker,
    'formatVersion': formatVersion,
    'name': name,
    'inputs': inputs,
    'start': start,
    'states': [for (final state in states) state.toJson()],
    'changes': [for (final change in changes) change.toJson()],
  };

  /// The file's text: indented, with anything short enough on a line of its
  /// own, so a state, a point and a change each read as one line.
  String encode() => '${_write(toJson(), '')}\n';

  /// A blend out of a file's text, with whatever could not be read.
  ///
  /// Lenient about the parts, strict about the whole, like a clip: a state
  /// or a change that cannot be read is left out with a note, and a file
  /// that is not a blend, is one from a newer Orblit, or has no state that
  /// can be read throws [BlendFormatException].
  static BlendLoad decode(String text) {
    final Object? parsed;
    try {
      parsed = jsonDecode(text);
    } on FormatException catch (error) {
      throw BlendFormatException('This is not a blend file: ${error.message}');
    }
    if (parsed is! Map<String, Object?> || parsed['kind'] != marker) {
      throw const BlendFormatException('This is not a blend file.');
    }
    final problems = <String>[];
    final version = parsed['formatVersion'];
    if (version is int && version > formatVersion) {
      throw const BlendFormatException(
        'This blend was written by a newer Orblit.',
      );
    }
    var json = parsed;
    for (final step in migrations) {
      if (version is int && step.from >= version) {
        json = step.apply(json, problems);
      }
    }

    final inputs = <String, double>{};
    for (final MapEntry(key: input, value: raw) in Values.object(
      json['inputs'],
    ).entries) {
      if (raw is num && raw.isFinite) {
        inputs[input] = raw.toDouble();
      } else {
        problems.add(
          'The input "$input" starts at something that is not a number, so '
          'it starts at nought.',
        );
        inputs[input] = 0;
      }
    }

    final states = <BlendState>[];
    final rawStates = json['states'];
    for (final raw in rawStates is List ? rawStates : const []) {
      if (raw is! Map<String, Object?>) continue;
      final name = Values.text(raw, 'name');
      if (name == null || name.isEmpty) {
        problems.add('A state with no name was left out.');
        continue;
      }
      if (states.any((state) => state.name == name)) {
        problems.add('Two states are called "$name"; the second was left out.');
        continue;
      }
      final plays = BlendSource.fromJson(raw, 'the state "$name"', problems);
      if (plays == null) continue;
      var speed = Values.maybeNumber(raw, 'speed') ?? 1;
      if (!speed.isFinite || speed < 0) {
        problems.add(
          'The state "$name" has a speed it cannot play at, so it plays '
          'at one.',
        );
        speed = 1;
      }
      states.add(
        BlendState(
          name,
          plays: plays,
          speed: speed,
          whenDone: Values.named(WhenDone.values, raw['whenDone']),
        ),
      );
    }
    if (states.isEmpty) {
      throw const BlendFormatException(
        'This blend has no state that could be read.',
      );
    }
    bool known(String? state) => states.any((one) => one.name == state);

    var start = Values.text(json, 'start') ?? states.first.name;
    if (!known(start)) {
      problems.add(
        'The blend starts in "$start", which is not one of its states, so '
        'it starts in "${states.first.name}".',
      );
      start = states.first.name;
    }

    final changes = <BlendChange>[];
    final rawChanges = json['changes'];
    for (final raw in rawChanges is List ? rawChanges : const []) {
      if (raw is! Map<String, Object?>) continue;
      final from = Values.text(raw, 'from');
      final to = Values.text(raw, 'to');
      final where =
          'The change from ${from == null ? 'anywhere' : '"$from"'} '
          'to "${to ?? ''}"';
      if (to == null || !known(to) || (from != null && !known(from))) {
        problems.add(
          '$where is not between two of the states, so it was left out.',
        );
        continue;
      }
      final when = raw.containsKey('when')
          ? BlendCondition.fromJson(raw['when'], where, problems)
          : BlendCondition.always;
      if (when == null) {
        problems.add(
          '$where was left out, since its condition could not be read.',
        );
        continue;
      }
      var fade = Values.maybeNumber(raw, 'fade') ?? 0;
      if (!fade.isFinite || fade < 0) {
        problems.add('$where fades for a time that is not one, so it cuts.');
        fade = 0;
      }
      changes.add(
        BlendChange(
          from: from,
          to: to,
          when: when,
          fade: fade,
          shape: Values.named(Easing.values, raw['shape']) ?? Easing.smooth,
          inStep: Values.flag(raw, 'inStep', fallback: false),
        ),
      );
    }

    final blend = BlendDocument(
      name: Values.text(json, 'name') ?? 'Blend',
      inputs: inputs,
      states: states,
      changes: changes,
      start: start,
    );
    for (final input in blend.inputsRead) {
      if (!inputs.containsKey(input)) {
        problems.add(
          'The blend reads "$input", which it does not declare, '
          'so it is nought until something sets it.',
        );
      }
    }
    return BlendLoad(blend: blend, problems: problems);
  }
}

/// One step between blend formats: decoded JSON at [from] in, at [to] out.
abstract class BlendMigration {
  const BlendMigration();

  int get from;
  int get to => from + 1;

  Map<String, Object?> apply(Map<String, Object?> json, List<String> notes);
}

/// Decoded JSON as text: anything that fits on a line on one, and the rest
/// indented.
String _write(Object? value, String indent) {
  final flat = jsonEncode(value);
  if (indent.isNotEmpty && indent.length + flat.length <= _lineLength) {
    return flat;
  }
  final inner = '$indent  ';
  if (value is Map<String, Object?>) {
    if (value.isEmpty) return '{}';
    final lines = [
      for (final entry in value.entries)
        '$inner${jsonEncode(entry.key)}: ${_write(entry.value, inner)}',
    ];
    return '{\n${lines.join(',\n')}\n$indent}';
  }
  if (value is List) {
    if (value.isEmpty) return '[]';
    final lines = [for (final one in value) '$inner${_write(one, inner)}'];
    return '[\n${lines.join(',\n')}\n$indent]';
  }
  return flat;
}

const int _lineLength = 80;

import 'dart:math' as math;

import 'package:orblit_scene/orblit_scene.dart' show Values;
import 'package:orblit_sequence/orblit_sequence.dart';

import 'clip.dart';
import 'format.dart';

/// The extension a cutscene file carries.
const String cutsceneExtension = '.ocutscene';

/// A file that is not a cutscene, or is one from a newer Orblit.
final class CutsceneFormatException implements Exception {
  const CutsceneFormatException(this.message);

  final String message;

  @override
  String toString() => 'CutsceneFormatException: $message';
}

/// A cutscene read back off disk, with anything that could not be read.
final class CutsceneLoad {
  const CutsceneLoad({required this.cutscene, this.problems = const []});

  final CutsceneDocument cutscene;

  final List<String> problems;
}

/// A stretch of a cutscene seen through one camera.
final class CutsceneShot {
  CutsceneShot({
    required this.camera,
    required this.start,
    required this.duration,
  }) {
    if (camera.isEmpty) {
      throw ArgumentError.value(camera, 'camera', 'A shot needs a camera.');
    }
    if (start < 0) {
      throw ArgumentError.value(start, 'start', 'A shot starts at 0 or on.');
    }
    if (!(duration > 0)) {
      throw ArgumentError.value(duration, 'duration', 'A shot needs a length.');
    }
  }

  /// The camera's id in the scene, which is its path: `street1/lamp3/eye`
  /// for one inside a placed prefab.
  final String camera;

  /// Seconds into the cutscene.
  final double start;

  final double duration;

  double get end => start + duration;

  CutsceneShot copyWith({String? camera, double? start, double? duration}) =>
      CutsceneShot(
        camera: camera ?? this.camera,
        start: start ?? this.start,
        duration: duration ?? this.duration,
      );

  Map<String, Object?> toJson() => {
    'camera': camera,
    'start': start,
    'duration': duration,
  };

  /// A shot out of decoded JSON, or null with a note in [problems].
  static CutsceneShot? fromJson(Object? raw, List<String> problems) {
    final json = Values.object(raw);
    final camera = Values.text(json, 'camera');
    final start = Values.maybeNumber(json, 'start') ?? 0;
    final duration = Values.maybeNumber(json, 'duration');
    if (camera == null ||
        camera.isEmpty ||
        start < 0 ||
        duration == null ||
        !(duration > 0)) {
      problems.add(
        'A shot that could not be read was left out. It needs a camera, a '
        'start and a length.',
      );
      return null;
    }
    return CutsceneShot(camera: camera, start: start, duration: duration);
  }
}

/// A sound a cutscene plays.
final class CutsceneSound {
  CutsceneSound({
    required this.sound,
    required this.start,
    required this.duration,
  }) {
    if (sound.isEmpty) {
      throw ArgumentError.value(sound, 'sound', 'A sound needs a file.');
    }
    if (start < 0) {
      throw ArgumentError.value(start, 'start', 'A sound starts at 0 or on.');
    }
    if (!(duration > 0)) {
      throw ArgumentError.value(
        duration,
        'duration',
        'A sound needs a length.',
      );
    }
  }

  /// The sound's file, relative to the project.
  final String sound;

  /// Seconds into the cutscene.
  final double start;

  /// How much of it plays. A cutscene cannot know how long a file is
  /// without opening it, and a sound cut short is often what was meant.
  final double duration;

  double get end => start + duration;

  Map<String, Object?> toJson() => {
    'sound': sound,
    'start': start,
    'duration': duration,
  };

  /// A sound out of decoded JSON, or null with a note in [problems].
  static CutsceneSound? fromJson(Object? raw, List<String> problems) {
    final json = Values.object(raw);
    final sound = Values.text(json, 'sound');
    final start = Values.maybeNumber(json, 'start') ?? 0;
    final duration = Values.maybeNumber(json, 'duration');
    if (sound == null ||
        sound.isEmpty ||
        start < 0 ||
        duration == null ||
        !(duration > 0)) {
      problems.add(
        'A sound that could not be read was left out. It needs a file, a '
        'start and a length.',
      );
      return null;
    }
    return CutsceneSound(sound: sound, start: start, duration: duration);
  }
}

/// A cutscene: the scene moving while it is watched through cameras that
/// take turns.
///
/// Its keys are a clip's, named by the scene's own ids rather than by a path
/// from whoever plays it, since the scene plays a cutscene and nothing in
/// it does. On top of those it has shots, which say which camera is looked
/// through when, and sounds. Two shots that overlap fade from one camera to
/// the other over the time both run.
///
/// Playing one is a [Director] over [sequence], which holds the shots,
/// sounds and marks. The keys are sampled from [motion] at the director's
/// time.
final class CutsceneDocument {
  CutsceneDocument({
    required this.motion,
    List<CutsceneShot> shots = const [],
    List<CutsceneSound> sounds = const [],
  }) : shots = List.unmodifiable(
         <CutsceneShot>[...shots]..sort((a, b) => a.start.compareTo(b.start)),
       ),
       sounds = List.unmodifiable(
         <CutsceneSound>[...sounds]..sort((a, b) => a.start.compareTo(b.start)),
       ) {
    if (motion.channels.any((channel) => channel.bone != null) ||
        motion.rootMotion != null) {
      throw ArgumentError.value(
        motion,
        'motion',
        'A cutscene moves what is in the scene, not the bones of a model.',
      );
    }
  }

  static const String marker = 'orblit.cutscene';

  /// The shape of the file. Bumped with a migration whenever it changes.
  static const int formatVersion = 1;

  /// Every step from an older cutscene file to this one, oldest first. None
  /// yet: this is the first format.
  static const List<CutsceneMigration> migrations = [];

  static const FileFormat _format = FileFormat(
    marker: marker,
    noun: 'cutscene',
    version: formatVersion,
    fail: CutsceneFormatException.new,
    steps: migrations,
  );

  /// Its keys and marks, its length, its frame rate and what it does at the
  /// end, as a clip whose targets are the scene's ids.
  ///
  /// A clip rather than property tracks of a sequence, because a clip's keys
  /// hold after the last one. A sequence's tracks are over at the moment
  /// they end, so a cutscene held on its last frame would lose where its
  /// last keys put things.
  final ClipDocument motion;

  /// In order of when they start.
  final List<CutsceneShot> shots;

  /// In order of when they start.
  final List<CutsceneSound> sounds;

  String get name => motion.name;

  double get duration => motion.duration;

  /// Its shots, sounds and marks, for a [Director] to play.
  ///
  /// Nothing is looked through before the first shot, after the last, or at
  /// the very end, where every shot is over.
  late final Sequence sequence = Sequence(
    name: name,
    rate: motion.rate,
    duration: duration,
    tracks: [
      ShotTrack(shots: _shotClips()),
      SoundTrack(
        clips: [
          for (final sound in sounds)
            (Clip(start: sound.start, duration: sound.duration), sound.sound),
        ],
      ),
      MarkTrack(marks: motion.marks),
    ],
  );

  /// The cameras looked through at [at] seconds in, each with how much it
  /// has to say. Two where shots overlap, and none where there is no shot.
  List<ShotAt> shotsAt(double at) => sequence.sampleAt(at).shots;

  /// Each shot as a clip of the shot track, faded in over its overlap with
  /// the shot before and out over its overlap with the shot after.
  ///
  /// Both fades are smoothstep, which is symmetric, so across an overlap
  /// the two weights always add up to one.
  List<(Clip, String)> _shotClips() => [
    for (var i = 0; i < shots.length; i++)
      (
        Clip(
          start: shots[i].start,
          duration: shots[i].duration,
          easeIn: i == 0 ? 0 : _overlap(shots[i - 1], shots[i]),
          easeOut: i == shots.length - 1 ? 0 : _overlap(shots[i], shots[i + 1]),
          shapeIn: Easing.smooth,
          shapeOut: Easing.smooth,
        ),
        shots[i].camera,
      ),
  ];

  static double _overlap(CutsceneShot first, CutsceneShot second) =>
      (first.end - second.start)
          .clamp(0, math.min(first.duration, second.duration))
          .toDouble();

  CutsceneDocument copyWith({
    ClipDocument? motion,
    List<CutsceneShot>? shots,
    List<CutsceneSound>? sounds,
  }) => CutsceneDocument(
    motion: motion ?? this.motion,
    shots: shots ?? this.shots,
    sounds: sounds ?? this.sounds,
  );

  Map<String, Object?> toJson() {
    final clip = motion.toJson();
    return Values.pruned({
      'kind': marker,
      'formatVersion': formatVersion,
      'name': clip['name'],
      'duration': clip['duration'],
      'rate': clip['rate'],
      'whenDone': clip['whenDone'],
      'shots': [for (final shot in shots) shot.toJson()],
      'sounds': [for (final sound in sounds) sound.toJson()],
      'channels': clip['channels'],
      'marks': clip['marks'],
    });
  }

  /// The file's text, written the way a clip's is: one key, mark, shot or
  /// sound to a line.
  String encode() => fileText(toJson());

  /// A cutscene out of a file's text, with whatever could not be read.
  ///
  /// Lenient about the parts, strict about the whole, like a clip: a shot
  /// that cannot be read is dropped with a note, and a file that is not a
  /// cutscene, or is one from a newer Orblit, throws
  /// [CutsceneFormatException].
  static CutsceneLoad decode(String text) {
    final problems = <String>[];
    final json = _format.open(text, problems);
    final shots = [
      for (final raw in _listOf(json['shots']))
        if (CutsceneShot.fromJson(raw, problems) case final shot?) shot,
    ];
    final sounds = [
      for (final raw in _listOf(json['sounds']))
        if (CutsceneSound.fromJson(raw, problems) case final sound?) sound,
    ];
    var motion = _sceneOnly(ClipDocument.fromJson(json, problems), problems);
    if (Values.text(json, 'name') == null) {
      motion = motion.copyWith(name: 'Cutscene');
    }
    // A length that is missing is the last thing the cutscene does, which
    // may be a shot or a sound rather than a key.
    final stated = Values.maybeNumber(json, 'duration');
    if (stated == null || stated < 0) {
      final ends = [...shots.map((s) => s.end), ...sounds.map((s) => s.end)];
      motion = motion.copyWith(
        duration: ends.fold<double>(motion.duration, math.max),
      );
    }
    return CutsceneLoad(
      cutscene: CutsceneDocument(motion: motion, shots: shots, sounds: sounds),
      problems: problems,
    );
  }

  static List<Object?> _listOf(Object? raw) => raw is List ? raw : const [];

  /// [motion] without the keys on bones and the root motion a clip can have
  /// and a cutscene cannot, with a note in [problems] when it had any.
  static ClipDocument _sceneOnly(ClipDocument motion, List<String> problems) {
    final kept = [
      for (final channel in motion.channels)
        if (channel.bone == null) channel,
    ];
    if (kept.length == motion.channels.length && motion.rootMotion == null) {
      return motion;
    }
    problems.add(
      'A cutscene moves what is in the scene, not the bones of a model. Its '
      'keys on bones were left out.',
    );
    return motion.copyWith(channels: kept, clearRootMotion: true);
  }
}

/// One step between cutscene formats: decoded JSON at [from] in, at [to]
/// out.
abstract class CutsceneMigration implements FormatStep {
  const CutsceneMigration();

  @override
  int get from;

  @override
  int get to => from + 1;

  @override
  Map<String, Object?> apply(Map<String, Object?> json, List<String> notes);
}

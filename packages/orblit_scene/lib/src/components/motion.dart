import '../component.dart';
import '../values.dart';

/// The clips an entity plays.
///
/// Each is a `.oclip` file, by its path in the project. A clip names what it
/// moves from where this entity stands: the entity itself, the parts of it,
/// the bones of the model it draws. So the walk made for one character plays
/// on every instance of it, and a clip is an asset rather than a recording of
/// one scene.
///
/// Which clip plays, how fast and blended with what is the game's to decide
/// while it runs. What the file says is only what this entity has to play, and
/// which of them, if any, starts on its own.
class MotionComponent extends SceneComponent {
  const MotionComponent({this.clips = const [], this.autoplay});

  static MotionComponent fromJson(Map<String, Object?> json) => MotionComponent(
    clips: Values.texts(json['clips']),
    autoplay: Values.text(json, 'autoplay'),
  );

  /// Project-relative, in the order somebody put them in.
  final List<String> clips;

  /// The clip that starts when the scene does, or null for none. One of
  /// [clips]; a path that is not is ignored rather than played, because a
  /// clip that was taken off the list was taken off for a reason.
  final String? autoplay;

  @override
  String get type => SceneComponents.motion;

  @override
  Map<String, Object?> toJson() =>
      Values.pruned({'clips': clips, 'autoplay': autoplay});
}

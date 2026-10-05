import 'package:orblit_sequence/orblit_sequence.dart';

import 'blend.dart';
import 'clip.dart';
import 'frame.dart';
import 'place.dart';
import 'snapshot.dart';

/// A blend being played on one character.
///
/// Holds the three things a blend needs and nothing more: the clips it
/// names, the inputs gameplay sets, and the [place], which is public and can
/// be set, because a place is the whole of where playing has got to. To
/// save a character, keep its place; to restore one, set it.
class BlendPlayer {
  BlendPlayer(
    this.blend, {
    required this.clips,
    BlendPlace? place,
    Map<String, double> inputs = const {},
    this.rest,
  }) : place = place ?? BlendPlace(blend.start),
       inputs = {...blend.inputs, ...inputs};

  final BlendDocument blend;

  /// The clips, by the names [blend] gives them.
  final Map<String, ClipDocument> clips;

  /// Fills unauthored channels and returns removed channels to rest in fades.
  final ClipFrame? rest;

  /// Where playing has got to.
  BlendPlace place;

  /// The inputs, set by gameplay. Starts as the blend says each starts.
  final Map<String, double> inputs;

  /// The state being played.
  String get state => place.state;

  /// Plays on by [seconds] and hands back what happened on the way.
  BlendStep advance(double seconds) {
    final step = blend.advance(
      place,
      inputs,
      seconds,
      clips: clips,
      rest: rest,
    );
    place = step.place;
    return step;
  }

  /// The pose where the blend now is, without moving.
  ClipFrame sample() => blend.sampleAt(place, inputs, clips: clips, rest: rest);

  /// Goes into [state] whatever the changes say: a cut, or a fade over
  /// [fade] seconds. For gameplay that knows better than any condition, a
  /// hit landing or a cutscene taking over.
  void enter(
    String state, {
    double fade = 0,
    Easing shape = Easing.smooth,
    bool inStep = false,
    bool fromPose = false,
  }) {
    if (blend.stateNamed(state) == null) {
      throw ArgumentError.value(state, 'state', 'Not a state of the blend.');
    }
    place = place.enter(
      blend.resolveState(state),
      fade: blend.fadeBetween(
        place,
        state,
        inputs,
        fallback: fade,
        clips: clips,
      ),
      shape: shape,
      inStep: inStep,
      frozen: fromPose ? PoseSnapshot(sample()) : null,
    );
  }

  /// Follows the shortest directed route, taking one edge per step.
  ///
  /// A requested route overrides conditions and waits for each fade to finish.
  /// Throws when the state is unknown or unreachable, leaving the place intact.
  void travel(String state) {
    final route = blend.routeTo(place.state, state);
    if (route == null) {
      throw ArgumentError.value(state, 'state', 'Unreachable state.');
    }
    place = place.withRoute(route);
  }
}

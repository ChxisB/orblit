/// Steps the view distance down when frames cost too much and back up when
/// there is room.
///
/// The distance is a whole number of steps from [floor] to [ceiling]. A game
/// reads [steps] as the clipmap's ring count and [fraction] as the share of
/// each scatter layer's range to keep. Cost is in whatever unit the game
/// measures, such as frame time in seconds, and [high] and [low] are in the
/// same unit.
///
/// It decides once every [settle] seconds, on the mean cost since the last
/// decision, so one slow frame moves nothing. It moves one step, down above
/// [high] and up below [low]. Between the two it holds. That gap is what stops
/// a cost that falls when the distance does from stepping it straight back.
///
/// Time is passed in, not read, so a test can script it. Nothing uses a policy
/// unless the game makes one.
final class DistancePolicy {
  DistancePolicy({
    required this.ceiling,
    required this.high,
    required this.low,
    this.floor = 1,
    this.settle = 2,
  }) : _steps = ceiling {
    if (floor < 1 || floor > ceiling) {
      throw ArgumentError.value(floor, 'floor', 'Must be 1 to $ceiling.');
    }
    if (!(low < high)) {
      throw ArgumentError.value(low, 'low', 'Must be below $high.');
    }
    if (!(settle > 0)) {
      throw ArgumentError.value(settle, 'settle', 'Must be above zero.');
    }
  }

  final int ceiling;
  final int floor;
  final double high;
  final double low;

  /// Seconds between decisions.
  final double settle;

  int _steps;
  double? _since;
  double _total = 0;
  int _frames = 0;

  /// How far the view reaches now, from [floor] to [ceiling].
  int get steps => _steps;

  /// [steps] as a share of [ceiling], 1 when nothing has been taken away.
  double get fraction => _steps / ceiling;

  /// Takes the cost of one frame, drawn at [seconds] on any clock that only
  /// goes forward.
  ///
  /// The first call starts the first interval, so a game's loading frames do
  /// not count against it.
  void update(double cost, double seconds) {
    final since = _since ??= seconds;
    _total += cost;
    _frames++;
    if (seconds - since < settle) return;

    final mean = _total / _frames;
    if (mean > high && _steps > floor) {
      _steps--;
    } else if (mean < low && _steps < ceiling) {
      _steps++;
    }
    _since = seconds;
    _total = 0;
    _frames = 0;
  }
}

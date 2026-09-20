part of 'runner.dart';

// The numbers the game is made of. They are here, together and named,
// rather than spelled out where they are used, because tuning the game
// means changing these and nothing else.

/// Three lanes, which is the number this kind of game has: two gives no
/// middle to go back to, four gives no obvious one.
const _lanes = [-2.6, 0.0, 2.6];
const _startSpeed = 14.0;
const _gravity = 30.0;

/// Up at nine and a half metres a second, which under this gravity is a
/// hop a metre and a half high and six-tenths of a second long: well over
/// a hurdle, and short enough that a jump is a decision rather than a way
/// of life.
const _jumpSpeed = 9.4;
const _slideTime = 0.7;

/// How far the runner reaches along the road either side of its middle,
/// and across it from its middle to an obstacle's.
const _halfDepth = 0.35;
const _reach = 1.55;

/// What clears what. The hurdle's top is at a metre; the bar's underside
/// is at 1.05, which a runner standing at 1.3 does not fit under and one
/// sliding at 0.8 does.
const _hurdleClear = 0.92;
const _barBottom = 1.05;
const _standing = 1.3;
const _sliding = 0.8;

// ---- the world ----

const _chunk = 40.0;
const _chunks = 11;
const _behind = 3;

/// How far is laid ahead: to the end of the chunks, where the haze has
/// all but finished.
const _ahead = (_chunks - _behind) * _chunk;
const _rebase = 1024.0;
const _bladesPerChunk = 1100;
const _grassRange = 60.0;
const _unlaid = -1 << 30;

/// How many of each kind of scenery one chunk may hold, which is what
/// keeps a chunk's keys apart from the next one's.
const _caps = [6, 5, 1, 1, 3, 1, 3, 1, 1];
final _sceneryMeshes = [
  RunnerArt.pine,
  RunnerArt.oak,
  RunnerArt.house,
  RunnerArt.darkHouse,
  RunnerArt.bush,
  RunnerArt.redBush,
  RunnerArt.rock,
  RunnerArt.billboard,
  RunnerArt.hill,
];

/// The clouds, where they are relative to the runner: across, up, how far
/// ahead, and how big.
const _clouds = [
  (x: -170.0, y: 58.0, ahead: 280.0, size: 17.0),
  (x: -60.0, y: 74.0, ahead: 320.0, size: 13.0),
  (x: 30.0, y: 46.0, ahead: 250.0, size: 11.0),
  (x: 120.0, y: 66.0, ahead: 300.0, size: 16.0),
  (x: 230.0, y: 50.0, ahead: 270.0, size: 14.0),
  (x: -280.0, y: 40.0, ahead: 260.0, size: 12.0),
  (x: 300.0, y: 80.0, ahead: 330.0, size: 15.0),
];

final _white = Vector3(1, 1, 1);

import 'package:orblit_parity_tool/fixture.dart';
import 'package:orblit_profile/orblit_profile.dart';

/// Frames of 30 to 60 ms with spans that come and go, a late span, gauges,
/// counts and a heap that climbs. About six seconds, so the heap is read
/// several times.
List<List<Object?>> _script() {
  var seed = 12345;
  int next(int below) {
    seed = (seed * 1103515245 + 12345) & 0x7fffffff;
    return seed % below;
  }

  final ops = <List<Object?>>[
    ['frame'],
  ];
  var heap = 100000000;
  for (var i = 0; i < 150; i++) {
    ops.add(['span', 'update', 1000 + next(9000)]);
    if (i.isEven) ops.add(['span', 'diff', next(4000)]);
    if (i >= 100) ops.add(['span', 'late', 500 + next(2000)]);
    ops.add(['span', 'update', next(3000)]);
    ops.add(['gauge', 'entities', 10 + next(90)]);
    ops.add(['count', 'mesh', 1 + next(3)]);
    ops.add(['advance', 30000 + next(30000)]);
    heap += next(200000);
    ops.add(['setHeap', heap]);
    ops.add(['frame']);
  }
  return ops;
}

Map<String, Object?> _run(
  List<List<Object?>> ops, {
  required int frames,
  required int heapSamples,
}) {
  var now = 0;
  var heap = 0;
  final profiler = Profiler(
    frames: frames,
    heapSamples: heapSamples,
    heap: () => heap,
    clock: () => now,
  );
  for (final op in ops) {
    switch (op) {
      case ['frame']:
        profiler.frame();
      case ['advance', final int micros]:
        now += micros;
      case ['span', final String name, final int micros]:
        final started = profiler.begin();
        now += micros;
        profiler.end(name, started);
      case ['gauge', final String name, final int value]:
        profiler.gauge(name, value.toDouble());
      case ['count', final String tag, final int by]:
        profiler.count(tag, by);
      case ['setHeap', final int bytes]:
        heap = bytes;
      default:
        throw StateError('unknown op $op');
    }
  }
  return profiler.snapshot();
}

/// Usage: `dart run bin/profile.dart <repository root>`.
void main(List<String> args) {
  final ops = _script();
  writeFixture(
    root: args.single,
    package: 'orblit_profile',
    name: 'profile',
    cases: [
      // A ring that wraps many times, and one that never fills.
      for (final (frames, heapSamples) in [(64, 5), (600, 600)])
        {
          'frames': frames,
          'heapSamples': heapSamples,
          'ops': ops,
          'expect': _run(ops, frames: frames, heapSamples: heapSamples),
        },
    ],
  );
}

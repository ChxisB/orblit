import 'package:orblit_input/orblit_input.dart';
import 'package:test/test.dart';

const south = 0x130;
const absX = 0x00;

// What a snapshot pad writes, decoded again, so the test reads events rather
// than bytes.
List<EvdevEvent> drained(SnapshotPad pad) => EvdevDecoder().decode(pad.drain());

void main() {
  late Set<int> held;
  late Map<int, int> axes;
  var alive = true;
  late SnapshotPad pad;

  setUp(() {
    held = {};
    axes = {};
    alive = true;
    pad = SnapshotPad(
      identity: const PadIdentity(name: 'Fake'),
      read: () => (buttons: Set.of(held), axes: Map.of(axes)),
      alive: () => alive,
    );
  });

  test('reports what changed since the last drain, then a report', () {
    held.add(south);
    axes[absX] = 100;

    expect(drained(pad), [
      const EvdevEvent(evKey, south, 1),
      const EvdevEvent(evAbs, absX, 100),
      const EvdevEvent(evSyn, synReport, 0),
    ]);
    expect(drained(pad), isEmpty, reason: 'nothing moved since');

    held.clear();
    axes[absX] = 100;
    expect(drained(pad), [
      const EvdevEvent(evKey, south, 0),
      const EvdevEvent(evSyn, synReport, 0),
    ]);
  });

  test('asking where it stands does not change what the next drain says', () {
    held.add(south);

    expect(pad.resync().buttons, {south});
    expect(drained(pad).first, const EvdevEvent(evKey, south, 1));
  });

  test('a pad that has gone says nothing', () {
    held.add(south);
    alive = false;

    expect(pad.drain(), isEmpty);
    expect(pad.resync().buttons, isEmpty);
  });
}

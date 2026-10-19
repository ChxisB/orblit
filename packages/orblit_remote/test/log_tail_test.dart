import 'package:orblit_remote/orblit_remote.dart';
import 'package:test/test.dart';

void main() {
  test('tail answers the last lines in the order they came', () {
    final log = LogTail()
      ..add('a')
      ..add('b')
      ..add('c');
    expect(log.tail(2), ['b', 'c']);
    expect(log.tail(10), ['a', 'b', 'c']);
    expect(log.tail(0), isEmpty);
  });

  test('the oldest line goes when the ring is full', () {
    final log = LogTail(capacity: 2)
      ..add('a')
      ..add('b')
      ..add('c');
    expect(log.tail(5), ['b', 'c']);
  });

  test('an empty ring or a negative count is refused', () {
    expect(() => LogTail(capacity: 0), throwsArgumentError);
    expect(() => LogTail().tail(-1), throwsArgumentError);
  });
}

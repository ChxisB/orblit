import 'package:orblit_particles/src/random.dart';
import 'package:test/test.dart';

void main() {
  test('the same three numbers give the same answer', () {
    expect(unitAt(7, 123456, 9), unitAt(7, 123456, 9));
    expect(unitAt(7, 123456, 9), isNot(unitAt(7, 123456, 10)));
    expect(unitAt(7, 123456, 9), isNot(unitAt(8, 123456, 9)));
  });

  // These were read off the VM. A web run must give the same digits, which is
  // what the 32-bit multiply is for.
  test('known values do not move', () {
    expect(unitAt(0, 0, 0), 0.12107708724215627);
    expect(unitAt(1, 2, 3), 0.16923378384672105);
    expect(unitAt(7, 123456, 9), 0.8150795300025493);
    expect(unitAt(-5, 0x7fffffff, 4), 0.7470536117907614);
    expect(unitAt(99, 0x40000000 + 65536 * 3 + 7, 1), 0.5054033196065575);
  });

  test('every value is from 0 up to, not including, 1', () {
    for (var i = 0; i < 5000; i++) {
      final v = unitAt(3, i, i % 10);
      expect(v, inInclusiveRange(0, 1));
      expect(v, lessThan(1));
    }
  });
}

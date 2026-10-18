import 'package:orblit_audio/orblit_audio.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

Emitter at(double x, [double z = 0, Falloff? falloff]) =>
    Emitter(position: Vector3(x, 0, z), falloff: falloff);

void main() {
  group('Falloff', () {
    test('linear is full to near, a straight line, and silent from far', () {
      final falloff = Falloff(near: 2, far: 12, curve: FalloffCurve.linear);

      expect(falloff.gainAt(0), 1);
      expect(falloff.gainAt(2), 1);
      expect(falloff.gainAt(7), closeTo(0.5, 1e-9));
      expect(falloff.gainAt(12), 0);
      expect(falloff.gainAt(40), 0);
    });

    test('inverse drops fast at first and still reaches zero at far', () {
      final falloff = Falloff(near: 1, far: 11);

      expect(falloff.gainAt(1), 1);
      expect(falloff.gainAt(2), closeTo((0.5 - 1 / 11) / (1 - 1 / 11), 1e-9));
      expect(falloff.gainAt(2), lessThan(0.5));
      expect(falloff.gainAt(11), 0);
      expect(falloff.gainAt(10.999), closeTo(0, 1e-3));
    });

    test('near must be above zero and far above near', () {
      expect(() => Falloff(near: 0), throwsArgumentError);
      expect(() => Falloff(near: 5, far: 5), throwsArgumentError);
      expect(() => Falloff(far: double.infinity), throwsArgumentError);
    });
  });

  group('Cone', () {
    final cone = Cone(inner: 0.4, outer: 1.2, outerGain: 0.2);

    test('is full inside, a line between and outerGain past the edge', () {
      expect(cone.gainAt(0), 1);
      expect(cone.gainAt(0.4), 1);
      expect(cone.gainAt(0.8), closeTo(0.6, 1e-9));
      expect(cone.gainAt(1.2), 0.2);
      expect(cone.gainAt(3), 0.2);
    });

    test('an edge with no width is a step, not a division by zero', () {
      final step = Cone(inner: 1, outer: 1);

      expect(step.gainAt(1), 1);
      expect(step.gainAt(1.01), 0);
    });

    test('the angles and the gain are checked', () {
      expect(() => Cone(inner: 2, outer: 1), throwsArgumentError);
      expect(() => Cone(inner: 0, outer: 4), throwsArgumentError);
      expect(() => Cone(inner: 0, outer: 1, outerGain: 2), throwsArgumentError);
    });
  });

  group('Motion', () {
    test('velocity is the move over the time it took', () {
      final listener = Listener()..teleport(Vector3(1, 0, 0));

      listener.moveTo(Vector3(4, 0, 0), 0.5);

      expect(listener.velocity, Vector3(6, 0, 0));
      expect(listener.position, Vector3(4, 0, 0));
    });

    test('a teleport is not a speed', () {
      final listener = Listener()..moveTo(Vector3(10, 0, 0), 1);

      listener.teleport(Vector3(500, 0, 0));

      expect(listener.velocity, Vector3.zero());
    });

    test('a frame of no time leaves no velocity', () {
      final listener = Listener()..moveTo(Vector3(10, 0, 0), 1);

      listener.moveTo(Vector3(12, 0, 0), 0);

      expect(listener.velocity, Vector3.zero());
    });

    test('a move keeps the position by value, not by reference', () {
      final spot = Vector3(1, 2, 3);
      final listener = Listener()..moveTo(spot, 1);

      spot.x = 99;

      expect(listener.position.x, 1);
    });

    test('the starting position is kept by value, not by reference', () {
      final spot = Vector3(1, 2, 3);
      final emitter = Emitter(position: spot);

      spot.x = 99;

      expect(emitter.position.x, 1);
    });
  });

  group('Emitter', () {
    test('gain follows the falloff by distance', () {
      final listener = Listener();
      final falloff = Falloff(near: 1, far: 11, curve: FalloffCurve.linear);

      expect(at(6, 0, falloff).heardBy(listener).gain, closeTo(0.5, 1e-9));
      expect(at(20, 0, falloff).heardBy(listener).gain, 0);
    });

    test('a sound on the right pans right and on the left pans left', () {
      final listener = Listener();

      expect(at(5).heardBy(listener).pan, closeTo(1, 1e-9));
      expect(at(-5).heardBy(listener).pan, closeTo(-1, 1e-9));
      expect(at(0, -5).heardBy(listener).pan, closeTo(0, 1e-9));
      expect(at(0, 5).heardBy(listener).pan, closeTo(0, 1e-9));
    });

    test('the pan eases to the middle inside near', () {
      final listener = Listener();

      expect(at(0.5).heardBy(listener).pan, closeTo(0.5, 1e-9));
      expect(at(-0.25).heardBy(listener).pan, closeTo(-0.25, 1e-9));
      expect(at(0).heardBy(listener).pan, 0);
      expect(
        at(1, 0, Falloff(near: 2)).heardBy(listener).pan,
        closeTo(0.5, 1e-9),
      );
    });

    test('an emitter keeps its own copy of the direction it faces', () {
      final facing = Vector3(0, 0, 1);
      final emitter = Emitter(position: Vector3.zero(), forward: facing);

      facing.setValues(1, 0, 0);

      expect(emitter.forward, Vector3(0, 0, 1));
    });

    test('turning the listener turns the pan', () {
      final listener = Listener()..forward = Vector3(1, 0, 0);

      // Facing +X with +Y up puts +Z on the right.
      expect(at(0, 5).heardBy(listener).pan, closeTo(1, 1e-9));
      expect(at(5).heardBy(listener).pan, closeTo(0, 1e-9));
    });

    test('a listener whose up is not a unit vector still pans within one', () {
      final listener = Listener()..up = Vector3(0, 3, 0);

      expect(at(5).heardBy(listener).pan, closeTo(1, 1e-9));
    });

    test('a listener facing along up has no right, so no pan', () {
      final listener = Listener()..forward = Vector3(0, 1, 0);

      expect(at(5).heardBy(listener).pan, 0);
    });

    test('a cone is loud in front and quiet behind', () {
      final cone = Cone(inner: 0.3, outer: 1.0, outerGain: 0.1);
      Emitter facing(Vector3 forward) => Emitter(
        position: Vector3(0, 0, -3),
        falloff: Falloff(near: 5, far: 50),
        cone: cone,
        forward: forward,
      );
      final listener = Listener();

      // The emitter is ahead of the listener. Facing +Z points it at them.
      expect(facing(Vector3(0, 0, 1)).heardBy(listener).gain, 1);
      expect(facing(Vector3(0, 0, -1)).heardBy(listener).gain, 0.1);
    });

    test('a cone reads an emitter facing in any length of vector', () {
      Emitter facing(double length) => Emitter(
        position: Vector3(0, 0, -3),
        falloff: Falloff(near: 5, far: 50),
        cone: Cone(inner: 0.1, outer: 0.2),
        forward: Vector3(0, 0, length),
      );

      expect(facing(40).heardBy(Listener()).gain, 1);
      expect(facing(0.5).heardBy(Listener()).gain, 1);
    });

    test('a sound on top of the listener is full gain with no direction', () {
      final level = at(0).heardBy(Listener());

      expect(level.gain, 1);
      expect(level.pitch, 1);
      expect(level.pan, 0);
    });
  });

  group('Doppler', () {
    Level heard({
      double emitterSpeed = 0,
      double listenerSpeed = 0,
      double doppler = 1,
    }) {
      // The emitter is 20 m ahead, down -Z. Positive speed is towards the
      // other one.
      final listener = Listener()..velocity = Vector3(0, 0, -listenerSpeed);
      final emitter = Emitter(
        position: Vector3(0, 0, -20),
        falloff: Falloff(near: 1, far: 100),
        doppler: doppler,
      )..velocity = Vector3(0, 0, emitterSpeed);
      return emitter.heardBy(listener);
    }

    test('a sound coming closer rises and one going away falls', () {
      expect(heard(emitterSpeed: 34.3).pitch, closeTo(1 / 0.9, 1e-9));
      expect(heard(emitterSpeed: -34.3).pitch, closeTo(1 / 1.1, 1e-9));
    });

    test('a listener moving at a sound hears what the sound moving does', () {
      expect(heard(listenerSpeed: 34.3).pitch, closeTo(1.1, 1e-9));
      expect(heard(listenerSpeed: -34.3).pitch, closeTo(0.9, 1e-9));
    });

    test('the amount scales the shift, and zero turns it off', () {
      final real = heard(emitterSpeed: 34.3).pitch;
      final half = heard(emitterSpeed: 34.3, doppler: 0.5).pitch;

      expect(half, closeTo(1 / 0.95, 1e-9));
      expect(half, lessThan(real));
      expect(heard(emitterSpeed: 34.3, doppler: 0).pitch, 1);
    });

    test('a speed past the speed of sound stays inside a factor of three', () {
      expect(heard(emitterSpeed: 5000).pitch, closeTo(2, 1e-9));
      expect(heard(emitterSpeed: -5000).pitch, closeTo(2 / 3, 1e-9));
      final both = heard(emitterSpeed: 5000, listenerSpeed: 5000);
      expect(both.pitch, closeTo(3, 1e-9));
      final apart = heard(emitterSpeed: -5000, listenerSpeed: -5000);
      expect(apart.pitch, closeTo(1 / 3, 1e-9));
      final scaled = heard(emitterSpeed: 5000, listenerSpeed: 5000, doppler: 4);
      expect(scaled.pitch, closeTo(3, 1e-9));
    });

    test('speed across the line to the listener makes no shift', () {
      final listener = Listener();
      final emitter = Emitter(position: Vector3(0, 0, -20))
        ..velocity = Vector3(30, 0, 0);

      expect(emitter.heardBy(listener).pitch, closeTo(1, 1e-9));
    });

    test('the amount must be zero or more', () {
      expect(
        () => Emitter(position: Vector3.zero(), doppler: -1),
        throwsArgumentError,
      );
    });
  });
}

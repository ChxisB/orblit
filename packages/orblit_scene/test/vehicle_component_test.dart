import 'package:orblit_scene/orblit_scene.dart';
import 'package:test/test.dart';

void main() {
  group('a vehicle component', () {
    test('is a small car when the file says nothing', () {
      final car = VehicleComponent.fromJson(const {});
      expect(car.maxSteer, 30);
      expect(car.torque, VehicleComponent.defaultTorque);
      expect(car.ratios, VehicleComponent.defaultRatios);
      expect(car.ground, 0xFFFFFFFF);
    });

    test('survives the round trip through a scene file', () {
      final car = VehicleComponent(
        maxSteer: 40,
        antiRoll: 8000,
        torque: [1000, 100, 6000, 180],
        ratios: [3, 1.5],
        lock: 200,
        ground: 3,
      );
      const wheel = WheelComponent(
        radius: 0.4,
        steers: true,
        drives: true,
        handbrake: true,
        grip: 1.2,
      );
      final document = SceneDocument(
        name: 'Scene',
        entities: [
          SceneEntity(
            id: 'car',
            name: 'Car',
            components: {SceneComponents.vehicle: car},
          ),
          const SceneEntity(
            id: 'wheel',
            name: 'Wheel',
            parent: 'car',
            components: {SceneComponents.wheel: wheel},
          ),
        ],
      );

      final back = SceneDocument.decode(document.encode()).document;
      expect(
        (back['car']![SceneComponents.vehicle]! as VehicleComponent).toJson(),
        car.toJson(),
      );
      expect(
        (back['wheel']![SceneComponents.wheel]! as WheelComponent).toJson(),
        wheel.toJson(),
      );
    });

    test('keeps a curve the file empties rather than inventing one', () {
      final car = VehicleComponent.fromJson(const {'torque': <double>[]});
      expect(car.torque, isEmpty);
    });

    test('is written after the zone and before the light', () {
      final order = SceneComponents.order;
      expect(order.indexOf('vehicle'), order.indexOf('zone') + 1);
      expect(order.indexOf('wheel'), order.indexOf('vehicle') + 1);
      expect(order.indexOf('light'), order.indexOf('wheel') + 1);
    });
  });

  group('a wheel component', () {
    test('has the numbers of a car wheel when the file says nothing', () {
      final wheel = WheelComponent.fromJson(const {});
      expect(wheel.radius, 0.35);
      expect(wheel.brake, 3000);
      expect(wheel.steers, isFalse);
      expect(wheel.travel, 0.3);
    });

    test('changes one field with copyWith', () {
      final wheel = const WheelComponent().copyWith(drives: true, grip: 0.8);
      expect(wheel.drives, isTrue);
      expect(wheel.grip, 0.8);
      expect(wheel.radius, 0.35);
    });
  });
}
